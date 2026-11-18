# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

`AGENTS.md` holds the shared rules, guardrails, skills table and repo layout (also used by Cursor and
Copilot). It is imported here, together with the `.claude/rules/` files it imports:

@AGENTS.md

## Project

HourOffice: hourly office/room booking app, a rebuild of an app originally made on Base44. The full plan, data model and endpoint list are in `docs/PLAN.md` (Hebrew). Its **section 0 "החלטות" overrides everything else in the plan**:
- **Google sign-in only.** There are no passwords, OTP, or emails of any kind. To "invite" a user, an admin pre-creates the user record (email + role, `status: "invited"`). When that person signs in with Google they get that role.
- **Frontend and backend are fully separate deployments.** The only link between them is `apiBaseUrl` in `frontend/config.js` plus CORS on the API.
- **Currency:** every office/booking stores an ISO `currency` (default `ILS`). The display symbol per language (`₪` he / `$` en) comes from server settings.
- **Secrets:** the MongoDB Atlas connection string only ever lives in env vars (`Mongo__ConnectionString`), never in code or chat.

The README and plan are in Hebrew. UI is bilingual he/en with RTL support.

## Commands

Run the API on :5080 (Development: email-only dev login; `admin@example.com` becomes admin). Run it from `backend/HourOffice.Api` so it finds `appsettings.Development.json`:
```bash
cd backend/HourOffice.Api && ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5080
```
Run the frontend on :5500 (static dev server with SPA fallback):
```bash
node frontend/dev-server.mjs 5500
```
`.claude/launch.json` defines both as `api` and `web` for the preview tool. Development expects MongoDB on `mongodb://localhost:27017`, database `houroffice`. On this machine it is installed as the `MongoDB` Windows service, and `mongosh` is available.

Tests (in `backend/`): integration tests start their own temporary `mongod` from the EphemeralMongo7 NuGet package, so no MongoDB needs to be running:
```bash
cd backend && dotnet test
cd backend && dotnet test --filter "FullyQualifiedName~BookingTests"          # one class
cd backend && dotnet test --filter "FullyQualifiedName~BookingTests.SomeName" # one test
```
Frontend has no build or lint. CI only runs `node --check` on every `frontend/js/**/*.js`.

E2E smoke (Playwright, phone viewport, he + en). Needs the API and site running against an **empty** database:
```bash
NODE_PATH=$(npm root -g) node e2e/smoke.mjs [screenshotDir]
```

The projects target `net8.0`. A newer SDK builds them fine as long as the .NET 8 runtime is installed.

## Backend architecture (`backend/HourOffice.Api`)

- **Minimal APIs, no controllers.** `Program.cs` wires up DI, JWT, CORS, rate limiting (`"auth"` policy) and forwarded headers. It calls each `Endpoints/*Endpoints.Map(app)`. On startup it runs `MongoContext.EnsureIndexesAsync()` and `TermsSeeder` (seeds terms from `Seed/terms.{he,en}.md`). There are no migrations: indexes and seed data are created at boot.
- **`Endpoints/Api.cs`** holds the shared helpers. Errors are always `Api.Error(status, code)` → `{ code, message }`, and the frontend maps `code` to translated text. Route groups use `.RequireUser()` / `.RequireAdmin()`. These filters **load the user from the DB on every request**; the role claim in the JWT is not trusted for authorization. `ctx.CurrentUserAsync(db)` caches the user per request.
- **Auth flow** (`AuthEndpoints.cs`): `POST /api/auth/google` (or `/api/auth/dev-login` when `Auth:DevLoginEnabled` and not Production) → `SignInAsync`. Emails are lower-cased. Emails in `Auth:AdminEmails` are promoted to admin on every sign-in, and there is no demotion. `Auth:AllowOpenSignup=false` blocks unknown emails. Access JWT plus a refresh token stored in the `refreshTokens` collection. The client keeps the refresh token in localStorage and sends it in the request body, not a cookie, because cross-site cookies are blocked in Safari.
- **Mongo** (`Data/MongoContext.cs`): typed collections `users, offices, bookings, siteSettings, terms, refreshTokens`. A convention pack makes element names **camelCase** and ignores extra elements. Keep that in mind when querying with `mongosh` (`role`, `email`, ...). Models are in `Models.cs`, request/response DTOs in `Endpoints/Dtos.cs`.
- **Booking rules** (open hours, max days ahead, time zone `Asia/Jerusalem`, recurring limits, slot ranges) all live in `Services/BookingRules.cs`. The server is the source of truth, not the browser. Time always comes from the injected `TimeProvider`; never use `DateTime.Now`/`UtcNow` directly, because tests swap in `FakeClock`.
- **Storage:** `IFileStorage` is `LocalFileStorage` (served at `/uploads`) or `S3FileStorage` (Cloudflare R2), chosen by `Storage:Provider`.
- All config is under `Mongo`, `Auth`, `Cors`, `Booking`, `Currency`, `Storage` (see `Options.cs` and `appsettings.json`). In deployment it is set as env vars with `__` (e.g. `Auth__AdminEmails__0`). The README has the full table.

## Tests (`backend/HourOffice.Tests`)

`TestApp.cs` is the harness: `MongoFixture` (one shared `mongod` per run, xunit collection `"api"`), `TestApp : WebApplicationFactory<Program>` (environment `Testing`, a random database per factory, admin email `owner@example.com`), `FakeClock` (fixed at Mon 2026-11-09 08:00 Israel time) and `FakeGoogle` (accepts credentials shaped `good:email[:name]`). Use `LoginAsync(email)` / `AdminAsync()` to get a client that already completed profile + terms, and `CreateOfficeAsync` for fixtures.

## Frontend architecture (`frontend/`)

Vanilla ES modules with no framework and no build step. Paths deliberately match the original app (`/BookOffice?id=`, `/AdminUsers`, ...), and `_redirects` provides the SPA fallback when deployed.
- `js/app.js`: route table (path → lazy `import()` of `js/pages/*.js` plus flags `public`, `optionalAuth`, `admin`, `nav`, `chrome`). Guards run in order: signed in → profile complete (`needsProfile` → `/Register`) → current terms accepted (`needsTerms` → `/TermsOfUse`) → admin.
- `js/api.js`: fetch wrapper against `HOUROFFICE_CONFIG.apiBaseUrl`. Adds the bearer token, auto-refreshes on 401 (single in-flight refresh), and throws `ApiError(status, code)`.
- `js/store.js`: `store.settings` (from `/api/settings/public`: branding, currency, booking hours, Google client id) and `store.me` (from `/api/me`). Branding colors are applied as CSS variables at runtime, so CSS must use the variables rather than hard-coded colors.
- `js/ui.js`: `h(tag, attrs, ...children)` DOM builder plus shared widgets (`pageHeader`, `openSheet`/`confirmSheet` bottom sheets, `toast`, `errorBox`, `icon`). A page module is `export default async function (el)` that renders into `el` with `h`/`clear`.
- `js/i18n.js`: every UI string goes through `t(key)`. Keys come from the original app and both `en` and `he` must be filled. Language is stored in localStorage `appLanguage`.
- CSS is mobile-first (360px base, breakpoints at 640/1024px) and uses **logical properties only** (`margin-inline-start`, etc.) so the same CSS works for RTL and LTR. Touch targets are ≥44px, inputs ≥16px font.
