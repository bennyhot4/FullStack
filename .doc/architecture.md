# System Architecture

## Purpose
- A concise reference for component boundaries, ownership, and major flows. Details:
  `CLAUDE.md` (code-level) and `docs/PLAN.md` §2–§6 (design and deployment).

## Primary Components
- **API** — `backend/HourOffice.Api`: ASP.NET Core 8 minimal API, stateless, Docker container
  (port 8080, health check `/healthz` — not reachable on `*.run.app`, see Hosting). Owns every business rule.
- **Site** — `frontend/`: static vanilla HTML/JS SPA, deployed separately (Cloudflare Pages /
  Netlify). Knows only `apiBaseUrl` from `config.js`; everything else comes from
  `GET /api/settings/public`.
- **Database** — MongoDB (local `mongod` in development, Atlas in production). Collections:
  `users`, `offices`, `bookings`, `siteSettings`, `terms`, `refreshTokens`, `clients`
  (`bookings.clientId` links one optionally), `distributionLists`. Indexes and the
  initial terms are created by the API on startup.
- **File storage** — local `uploads/` in development, S3-compatible (Cloudflare R2) in production.

## Data Flow
- Browser → `fetch` with `Authorization: Bearer <access JWT>` → API → MongoDB.
- On 401 the site calls `POST /api/auth/refresh` once with the stored refresh token, then retries.
- Booking: the API computes the slot list and price, checks rules in `BookingRules`, and
  inserts; the partial unique index `uniq_confirmed_slot` (office + date + slot, confirmed only)
  rejects concurrent overlaps → `409 slot_taken`.
- Clients: `/api/clients` (CRUD) and `PUT /api/bookings/{id}/client` filter by the signed-in
  user inside every MongoDB query; someone else's id → 404. `GET /api/bookings/mine` adds
  `clientId` + `clientName` (`MyBookingDto`); admin and shared routes use `BookingDto`, which has
  no client fields. There is no admin client route.
- Admins' copies of users (`Services/AdminClientSync.cs`, plan 008): the first `PUT /api/me` with a
  phone inserts a `clients` document per active admin with `sourceUserId` (unique partial index
  `uniq_owner_source_user` on `userId + sourceUserId`); later profile changes `UpdateMany` those copies.
  Becoming an admin (role change, `Auth:AdminEmails`, invited admin's first sign-in) adds every user
  to that admin's list. A one-time startup pass (marker `admin_clients_filled` in `siteSettings`)
  fills existing admins. Deleting a user deletes the copies and pulls them from distribution lists.
- Distribution lists: `/api/distribution-lists` (CRUD, `RequireTerms()`), owner filter inside every
  query, members must be the owner's clients; deleting a client pulls it from the owner's lists.

## Auth
- Google Identity Services ID token → `POST /api/auth/google` → user upserted by lower-cased
  email → access JWT + refresh token (stored in `refreshTokens`). Dev-only email login exists
  outside Production.
- Every protected route group uses `RequireUser()` / `RequireAdmin()`, which reload the user
  from the DB on each request (roles are not trusted from the token).
- Gates: profile (phone) and current terms version are required before booking (server) and
  before most screens (client router). The terms gate is the `RequireTerms()` filter, also on
  `/api/clients` and `PUT /api/bookings/{id}/client`.

## Hosting (production, plan 006)
| Piece | Where | Name |
|---|---|---|
| API | Google Cloud Run, `europe-west1`, scale to zero, max 2 instances, startup CPU boost | project `houroffice-prod`, service `houroffice-api`, runtime account `houroffice-api@…` (only `secretAccessor` on its secrets) |
| Site | Cloudflare Pages, Direct Upload (no Git build) | project `houroffice` → `houroffice.pages.dev`; production `config.js` written at deploy time |
| Images | Cloudflare R2 via `S3FileStorage` | bucket `houroffice-uploads`, public `r2.dev` URL |
| Database | MongoDB Atlas M0, GCP Belgium | cluster `BookingDB`, db `houroffice`, user limited to `readWrite@houroffice` |
| Secrets | Google Secret Manager → Cloud Run `--set-secrets` | `mongo-connection-string`, `jwt-signing-key`, `r2-access-key-id`, `r2-secret-key` |
| Sign-in | Google Auth Platform, External, "In production" | Web client; origins `houroffice.pages.dev`, `localhost:5500` |

- Atlas network access is `0.0.0.0/0` (Cloud Run has no fixed outbound IP for free); the scoped DB user
  and a random password stored only in Secret Manager replace an IP allow-list.
- Smoke check is `GET /api/settings/public`: Google's front end reserves paths ending in `z` on `*.run.app`.
- Cost guard: ₪20/month budget alert, `max-instances 2`, Artifact Registry cleanup (keep newest 2).
- Runbook (deploy, secret rotation, rollback): README "פריסה". Plan 004 will automate it.

## External Dependencies
- Google Identity Services (sign-in), MongoDB Atlas, Cloudflare R2, a container host
  (Google Cloud Run recommended), a static host for the site.

## Operational Concerns
- Configuration only via options/env vars (`Mongo__*`, `Auth__*`, `Cors__*`, `Storage__*`, …).
- Rate limit on auth endpoints (`"auth"` policy, 30/min per IP); forwarded headers trusted for client IP.
- Errors: `{ code, message }` from endpoints, ProblemDetails for unhandled exceptions.

## Change Log
- 2026-11-09 — API (ASP.NET Core 8 + MongoDB) and vanilla JS frontend built; local dev on Windows with a MongoDB service.
- 2026-11-16 — Agent workflow files added (`AGENTS.md`, `.claude/`, `.doc/`, `.plan/`, `.orchestrate/`).
- 2026-11-16 — Private client list (`clients` collection, `/api/clients`, `bookings.clientId`), `RequireTerms()` filter (plan 002).
- 2026-11-17 — Public `/PrivacyPolicy` page (plan 007); first production go-live on Cloud Run + Cloudflare Pages + R2 + Atlas M0 (plan 006).
- 2026-11-18 — Users copied into admins' client lists (`clients.sourceUserId`, `users.marketingConsent`), `distributionLists` collection and `/api/distribution-lists` (plan 008).
