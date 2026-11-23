# System Architecture

## Purpose
- A concise reference for component boundaries, ownership, and major flows. Details:
  `CLAUDE.md` (code-level) and `docs/PLAN.md` §2–§6 (design and deployment).

## Primary Components
- **API** — `backend/HourOffice.Api`: ASP.NET Core 8 minimal API, stateless, Docker container
  (port 8080, health check `/healthz` — not reachable on `*.run.app`, see Hosting). Owns every business rule.
- **Site** — `frontend/`: React + TypeScript SPA built by Vite into static files (`dist/`), deployed separately (Cloudflare Pages /
  Netlify). Knows only `apiBaseUrl` from `config.js`; everything else comes from
  `GET /api/settings/public`.
- **Database** — MongoDB (local `mongod` in development, Atlas in production). Collections:
  `users`, `offices`, `bookings`, `siteSettings`, `terms`, `refreshTokens`, `clients`
  (`bookings.clientId` links one optionally), `distributionLists`; for AI assistants (plan 011)
  `oauthApplications`, `oauthAuthorizations`, `oauthScopes`, `oauthTokens` (OpenIddict's store, snake_case
  fields), `mcpConsents` (pending consent requests, TTL), `mcpAuditLog` (TTL 90/365 days) and
  `encryptionKeys` (the CSFLE key vault). Indexes and the
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

- Shared services (plan 011): the business rules live in `Services/BookingService.cs`, `OfficeService.cs`,
  `ClientService.cs`, `SettingsService.cs`, `AdminQueryService.cs` and `UserGates.cs`; the REST endpoints and the
  MCP tools both call them and return the same error codes (`Outcome<T>` / `ServiceError`).
- Field-level encryption (plan 011, E3): `users.phoneNumber`, `clients.phones`, `clients.emails`, `clients.notes`
  and `bookings.notes` use `EncryptedSerializer<T>` (MongoDB CSFLE, explicit mode, AEAD-AES-256 random). With
  `FieldEncryption:Mode=encrypt` they are stored as BinData subtype 6; reads accept plain and encrypted values, and
  `FieldEncryptionMigrator` converts existing documents in the background (marker `fields_encrypted`). The data key
  is wrapped by Cloud KMS (`local` key in development and tests). These fields are never used in query filters.

## AI assistants (MCP, plan 011)
- `POST /mcp`: MCP Streamable HTTP, **stateless** (Cloud Run, no sticky sessions), official C# SDK
  `ModelContextProtocol.AspNetCore`. Tools in `Mcp/UserTools.cs` (8) and `Mcp/AdminTools.cs` (14). `tools/list` and
  every call are filtered by the granted scopes and the role read from the DB (`[Authorize(Policy = "mcp:<scope>")]`
  plus `McpCaller.RequireAsync`).
- OAuth 2.1 authorization server: OpenIddict in the API (`Mcp/McpSetup.cs`, `Mcp/OAuthEndpoints.cs`). Authorization
  code + PKCE S256 only, exact redirect URIs, 60 s single-use codes, 10 min encrypted (JWE) access tokens for the
  audience `<issuer>/mcp` only, rotating refresh tokens (30 days, 7 with `admin:write` / `clients:full`) with reuse
  detection, token + authorization entry validation on every call (disconnect is immediate). Restricted dynamic client
  registration (`/oauth/register`). Discovery: `/.well-known/oauth-protected-resource` (RFC 9728, served by the SDK's
  MCP auth scheme) and `/.well-known/oauth-authorization-server` (RFC 8414).
- Consent: `/oauth/authorize` stores the validated request in `mcpConsents` and redirects to the site's
  `/McpConsent?request=<id>`; the page (site Bearer token) reads `/oauth/authorize/request/{id}` and posts
  `/oauth/authorize/decision`; on approval the browser returns to `/oauth/authorize?...&consent=<id>` and OpenIddict
  issues the code. Sensitive scopes need a Google sign-in by the same site session in the last 10 minutes (`auth_time`
  in the site token, kept across refreshes in `refreshTokens.authTime`) and end 7 days after consent.
- The person's view: `/api/me/connected-apps` (list, disconnect). Admin: `/api/admin/settings/mcp` (switch, all
  connections, `revoke-all`). Deleting a user revokes their MCP access.
- Guards before authentication (`UseHourOfficeMcpGate`): kill switch (`Mcp:Enabled` and the `mcp_enabled` site
  setting) → 503, `Origin` allow-list on `/mcp`, body limits (8 MB `/mcp`, 64 KB `/oauth`). Rate limits: 60 calls/min
  per token, 30/min per IP on `/oauth/*`, 10 registrations/hour per IP, plus per-person hourly write limits counted
  from `mcpAuditLog`. Each tool call writes one audit row (no arguments, no personal data).
- The MCP client's provider sees tool results (inherent to MCP); see the privacy policy and plan 011 "Encryption".

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
| Secrets | Google Secret Manager → Cloud Run `--set-secrets` | `mongo-connection-string`, `jwt-signing-key`, `r2-access-key-id`, `r2-secret-key`; plan 011: `mcp-signing-key`, `mcp-encryption-key` |
| Sign-in | Google Auth Platform, External, "In production" | Web client; origins `houroffice.pages.dev`, `localhost:5500` |
| Field encryption key (plan 011) | Google Cloud KMS, `europe-west1` | key ring `houroffice`, key `mongo-cmk`; runtime account has only `cryptoKeyEncrypterDecrypter` on it |

- Atlas network access is `0.0.0.0/0` (Cloud Run has no fixed outbound IP for free); the scoped DB user
  and a random password stored only in Secret Manager replace an IP allow-list.
- Smoke check is `GET /api/settings/public`: Google's front end reserves paths ending in `z` on `*.run.app`.
- Cost guard: ₪20/month budget alert, `max-instances 2`, Artifact Registry cleanup (keep newest 2).
- Runbook (deploy, secret rotation, rollback): `.doc/operations.md` "פריסה". Plan 004 will automate it.

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
- 2026-11-22 — MCP server for AI assistants (`/mcp`, OpenIddict OAuth 2.1, consent page, connected apps, audit log, kill switch), shared business services, field-level encryption of phones, client emails and notes (plan 011).
