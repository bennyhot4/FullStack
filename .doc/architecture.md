# System Architecture

## Purpose
- A concise reference for component boundaries, ownership, and major flows. Details:
  `CLAUDE.md` (code-level) and `docs/PLAN.md` §2–§6 (design and deployment).

## Primary Components
- **API** — `backend/HourOffice.Api`: ASP.NET Core 8 minimal API, stateless, Docker container
  (port 8080, health check `/healthz`). Owns every business rule.
- **Site** — `frontend/`: static vanilla HTML/JS SPA, deployed separately (Cloudflare Pages /
  Netlify). Knows only `apiBaseUrl` from `config.js`; everything else comes from
  `GET /api/settings/public`.
- **Database** — MongoDB (local `mongod` in development, Atlas in production). Collections:
  `users`, `offices`, `bookings`, `siteSettings`, `terms`, `refreshTokens`. Indexes and the
  initial terms are created by the API on startup.
- **File storage** — local `uploads/` in development, S3-compatible (Cloudflare R2) in production.

## Data Flow
- Browser → `fetch` with `Authorization: Bearer <access JWT>` → API → MongoDB.
- On 401 the site calls `POST /api/auth/refresh` once with the stored refresh token, then retries.
- Booking: the API computes the slot list and price, checks rules in `BookingRules`, and
  inserts; the partial unique index `uniq_confirmed_slot` (office + date + slot, confirmed only)
  rejects concurrent overlaps → `409 slot_taken`.

## Auth
- Google Identity Services ID token → `POST /api/auth/google` → user upserted by lower-cased
  email → access JWT + refresh token (stored in `refreshTokens`). Dev-only email login exists
  outside Production.
- Every protected route group uses `RequireUser()` / `RequireAdmin()`, which reload the user
  from the DB on each request (roles are not trusted from the token).
- Gates: profile (phone) and current terms version are required before booking (server) and
  before most screens (client router).

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
