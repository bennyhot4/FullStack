# Low-cost production deployment (first go-live)

Status: active
Owner: bennyhot4
Last updated: 2026-11-22

## Goal
Put HourOffice on the internet for real users at **~$0 per month**, using the owner's existing free
MongoDB Atlas cluster and their Google account (Google Cloud free tier), with a hard cap so a mistake or an abuse spike
can never produce a surprise bill.

This is the **first, manual go-live**. `.plan/004-2026-11-16-ci-cd-pipeline.md` (active) automates the
same targets later (staging + production from GitHub Actions); this plan does the one-time cloud setup
that 004 step 6 assumes, so 004 reuses it instead of repeating it.

Binding decisions it follows (`docs/PLAN.md` §0):
- Google sign-in only — the OAuth client is set up here, nothing else is needed for auth.
- The site and the API are two separate deployments, linked only by `apiBaseUrl` in
  `frontend/config.js` and CORS on the API.
- `Mongo__ConnectionString` (and the JWT key, the storage keys) live only in a secret manager.

## Scope
In scope:
- **API** → Google **Cloud Run** (scale to zero), built from `backend/Dockerfile`.
- **Site** → **Cloudflare Pages** (Q2), the `frontend/` folder as is, with production `config.js`.
- **Uploaded images** → **Cloudflare R2** through the existing `S3FileStorage` (Q3). Cloud Run's disk is
  wiped on every restart, so `Storage__Provider=Local` must not be used in production.
- **Database** → the owner's existing **Atlas free cluster (M0)**, database `houroffice`.
- **Secrets** → Google **Secret Manager**, mounted into Cloud Run.
- **Cost guard** → billing budget with e-mail alerts, `max-instances`, Artifact Registry cleanup.
- **Google OAuth client** for the production site origin.
- **Docs** — README "פריסה" section rewritten as a step-by-step runbook; `.doc/architecture.md` gets the
  hosting map.

Out of scope: no application code changes, endpoints, collections, pages or i18n keys. No CI/CD
(that is 004). No custom domain unless Q5 says so. No data migration from Base44.

### What it costs (checked 2026-11-17)

| Piece | Service | Free allowance | Expected for HourOffice |
|---|---|---|---|
| API | Cloud Run, request-based billing | 180,000 vCPU-s, 360,000 GiB-s, 2M requests / month (Tier 1 region) | well inside — $0 |
| API image | Artifact Registry | 0.5 GB storage | one .NET image ≈ 0.1–0.2 GB; keep the last 2 → $0 |
| Build | Cloud Build (`gcloud run deploy --source`) | 2,500 build-minutes / month | a few minutes per deploy → $0 |
| Secrets | Secret Manager | 6 active secret versions, 10,000 accesses / month | 3 secrets → $0 |
| Site | Cloudflare Pages | unlimited bandwidth, 500 builds / month | $0 |
| Images | Cloudflare R2 | 10 GB storage, no egress fees | $0 |
| DB | Atlas M0 | 512 MB | owner already has it — $0 |
| Safety net | — (Google One **Basic 100 GB** has no Cloud credit; see Q1) | new Cloud accounts may get a one-time 90-day trial credit | not needed if we stay in the free tier |

Google Cloud requires a billing account (a card) even when everything stays in the free tier. The
budget and `max-instances` below are what keep that safe.

## Assumptions
- Checked in the owner's Google account on 2026-11-17 (read only):
  - The subscription is **Google One Basic (100 GB, ₪80/year)**: storage only, **no Google Cloud credit**
    (only AI Pro / Ultra include the monthly credit). The free tiers above are what keeps the cost at $0.
  - Google Cloud console: **no active billing account**; four existing projects (MyGoogleMaps, My Project,
    Default Gemini Project, BennyTestProj). None is used for HourOffice; a new project `houroffice-prod`
    keeps billing, permissions and cleanup separate.
  - Not a member of the Google Developer Program (no benefits page).
- The Atlas M0 cluster can accept connections from anywhere (`0.0.0.0/0`). Cloud Run has no fixed
  outbound IP without a paid NAT, so an IP allow-list is not possible for free. A long random password
  on a DB user limited to the `houroffice` database replaces it (see Risks).
- Traffic is small (one business, tens of users), so the free tiers are never reached.
- The machine has neither `gcloud` nor `firebase` installed (checked). The runbook installs the Google
  Cloud CLI; Docker is **not** needed locally because `--source` builds in Cloud Build.
- The repository stays public; no secret is ever written to it.

## Open Questions
All answered 2026-11-17 by the owner: Q1 and Q4 from the checks below; **recommended answers approved**
for the rest (billing account created by the owner, free trial accepted if offered; Cloudflare Pages;
R2; free addresses, no domain yet; accept cold start + startup CPU boost; owner is the only admin,
`AllowOpenSignup=false` until users are invited).

1. ~~Which Google subscription?~~ **Answered (checked 2026-11-17):** Google One Basic 100 GB, no Cloud
   credit. **New question — billing account:** Cloud Run needs one (with a card) even at $0.
   **Recommended:** create it in the console (Billing → Create account). If Google offers the new-customer
   free trial credit, accept it; when the trial ends, upgrade to a paid account so the service keeps running
   inside the always-free tier. Upgrading to AI Pro only for the $10/month credit is not worth it here.
2. **Where does the site live: Cloudflare Pages or Firebase Hosting?** Firebase Hosting keeps
   everything in Google, but the free (Spark) plan allows only ~10 GB of transfer a month and **switches
   the site off** until the month ends if it goes over. Cloudflare Pages has no bandwidth limit, is free,
   and is what plan 004 already chose. **Recommended: Cloudflare Pages.**
3. **Where are uploaded office images stored: Cloudflare R2 or Google Cloud Storage?** R2 works with the
   existing `S3FileStorage` as it is, has 10 GB free and no download fees, and uses the same Cloudflare
   account as the site. A Cloud Storage bucket would need extra testing of the S3-compatible mode and is
   free only in US regions. **Recommended: R2.**
4. ~~Which region is your Atlas cluster in?~~ **Answered 2026-11-17: Europe — checked in Atlas: cluster `BookingDB`, GCP / Belgium (europe-west1), the same region as Cloud Run.** It was auto-paused for inactivity (M0 pauses after ~60 idle days) and must be resumed before go-live. Cloud Run runs in
   **`europe-west1`** (Belgium, Tier 1 price region with the full free allowance, ~60 ms from Israel, a
   few ms from the usual Atlas EU regions). Tel Aviv (`me-west1`) was rejected: Tier 2 (smaller free
   allowance) and far from the database.
5. **Custom domain now?** The free addresses are `https://<name>.pages.dev` (site) and
   `https://houroffice-api-….run.app` (API). A domain costs about $10–15 a year. **Recommended:** go
   live on the free addresses first; add a domain later without code changes (only `config.js`, CORS and
   the OAuth origin change).
   **Follow-up once a domain exists (owner asked 2026-11-17):** allow only visitors from Israel with a free
   Cloudflare WAF custom rule (`ip.src.country ne "IL"` → Block) on the site and on an `api.` subdomain
   proxied to Cloud Run. Not possible for free before that: the Atlas access list only sees Cloud Run
   (Belgium), and geo-blocking on Cloud Run itself needs Cloud Armor + a load balancer (~$18+/month).
6. **Accept a slow first load after idle?** With scale to zero, the first request after ~15 idle minutes
   waits a few seconds while the API starts. Keeping one instance always on removes that but costs
   money every month. **Recommended:** accept it, and turn on the free "startup CPU boost" to shorten it.
7. **Who is admin on day one, and is signup open?** **Recommended:** `Auth__AdminEmails__0` = your Google
   account; `Auth__AllowOpenSignup=false` until you have invited your users, then decide.
   **Decided 2026-11-22: signup is open (`Auth__AllowOpenSignup=true`).** A new user the owner shared
   the link with got "Access Restricted" (`user_not_registered`), and the owner chose open signup over
   inviting each user. Any Google account now signs in as a regular `user`; only `Auth__AdminEmails__0`
   is admin, and other admins are promoted on the Users screen.

## Steps
Run by the owner from the runbook (I prepare every command; nothing secret goes through chat).
1. ✅ *(done 2026-11-17)* **Google Cloud project + billing**: billing account "Bennies Billing" (ILS)
   created by the owner; project **`houroffice-prod`** (name "HourOffice") created and linked; budget
   **₪20/month** (≈ $5; the budget must be in the account's currency) on that project only, e-mail alerts
   at 50 % / 90 % / 100 %.
2. ✅ *(done 2026-11-17)* **Google Cloud CLI** 594.0.0 installed (`winget install Google.CloudSDK`),
   `gcloud auth login` done, defaults `core/project=houroffice-prod`, `run/region=europe-west1`; APIs
   enabled: `run`, `artifactregistry`, `cloudbuild`, `secretmanager`, `billingbudgets`.
3. ✅ *(done 2026-11-17)* **Atlas** (project "Project 0", cluster `BookingDB`): cluster resumed; old
   `bennyhot4_admin` (atlasAdmin) user deleted at the owner's request; DB user `houroffice-prod` with an
   autogenerated password, **`readWrite@houroffice` only, restricted to cluster BookingDB**; Network Access
   `0.0.0.0/0` added (comment "Cloud Run houroffice-prod"), the three older home `/32` entries kept. The
   `mongodb+srv://` string goes straight into Secret Manager (step 4), never into a file or chat.
4. ✅ *(done 2026-11-17; the R2 secrets were added in step 5)* **Secret Manager**: runtime
   service account **`houroffice-api@houroffice-prod.iam.gserviceaccount.com`** created with no project
   roles (Cloud Run runs as it instead of the default compute account, which has Editor);
   `mongo-connection-string` (typed by the owner into a hidden prompt, piped to gcloud via stdin, never
   on disk) and `jwt-signing-key` (48 random bytes, base64 = 64 chars, generated locally) stored, version 1
   each, no trailing newline; that account has `secretAccessor` on these two secrets only.
5. ✅ *(done 2026-11-17)* **Cloudflare**: Pages project **`houroffice`** created with `wrangler` 4.164.0
   (`npx.cmd wrangler@4 pages project create houroffice --production-branch=main --force`; site URL
   **`https://houroffice.pages.dev`**, no deployment yet). Note: wrangler 4 now tries to turn
   `pages project create` into a Cloudflare **Workers** static-assets deploy of the whole `frontend/`
   folder (it wrote a `wrangler.jsonc` in the repo root, since removed). That attempt failed on our
   `_redirects` (`/* /index.html 200` is an "infinite loop" for Workers assets, which use
   `not_found_handling: "single-page-application"` instead), so nothing was published. `--force` keeps the
   classic Pages project; later `wrangler pages deploy` commands need no `--force`. Moving to Workers
   static assets is a possible later change (plan 004), not needed now.
   R2 enabled by the owner; bucket **`houroffice-uploads`** (location Western Europe, WEUR,
   Standard class); public development URL on: `https://pub-5c470ba18f4f4b59b37e8f7f9c9779aa.r2.dev`
   (rate-limited, fine at this size; replaced by a custom domain later); S3 endpoint
   `https://dac2d0af6844ca6996701e71d6cffe7b.r2.cloudflarestorage.com`. Account API token
   "houroffice-api uploads": **Object Read & Write on `houroffice-uploads` only**, TTL forever. Its two
   values are stored as secrets `r2-access-key-id` and `r2-secret-key` (`secretAccessor` for
   `houroffice-api` only), so `Storage__S3__AccessKey` also comes from Secret Manager, not a plain env var.
   ⚠️ **Follow-up:** this token's values were pasted into the chat once; the owner chose to keep it for
   now. Rotate it (new token, `gcloud secrets versions add` both secrets, redeploy, delete the old token).
6. ✅ *(done 2026-11-17)* **Google OAuth client** (Web): branding "HourOffice" (owner filled the
   wizard: External audience, support + developer contact e-mail, User Data Policy accepted); home page
   `https://houroffice.pages.dev`, terms link `https://houroffice.pages.dev/TermsOfUse`, authorized domain
   `houroffice.pages.dev`; no logo (a logo would trigger Google verification). Web client "HourOffice web",
   **Client ID `770124643502-lf7d4908khfgkevsrv9rem99sdv6e2bp.apps.googleusercontent.com`** (not secret;
   the client secret is not used by Google Identity Services ID-token sign-in and was not copied);
   JavaScript origins `https://houroffice.pages.dev` and `http://localhost:5500`, no redirect URIs.
   Google kept "Publish app" disabled until the branding had a **privacy policy link**, so plan 007 added
   `/PrivacyPolicy`; after step 8 the link `https://houroffice.pages.dev/PrivacyPolicy` was saved and the app
   was published: **publishing status "In production"** (any Google account can reach the sign-in; the API
   then only admitted invited users because `AllowOpenSignup=false`; open since 2026-11-22, see Q7).
7. **Deploy the API**: `gcloud run deploy houroffice-api --source backend --region europe-west1
   --allow-unauthenticated --max-instances 2 --min-instances 0 --cpu-boost --memory 512Mi --cpu 1
   --set-secrets Mongo__ConnectionString=mongo-connection-string:latest,Auth__JwtSigningKey=jwt-signing-key:latest,Storage__S3__SecretKey=r2-secret-key:latest,Storage__S3__AccessKey=r2-access-key-id:latest
   --set-env-vars` for `Auth__GoogleClientId`, `Auth__AdminEmails__0`, `Auth__AllowOpenSignup`,
   `Cors__AllowedOrigins__0` (the Pages URL), `Storage__Provider=S3`, `Storage__S3__ServiceUrl`,
   `Storage__S3__Bucket`, `Storage__S3__PublicBaseUrl`.
   Then add an Artifact Registry cleanup policy keeping the 2 newest images.
   ✅ *(done 2026-11-17, from branch `feat/privacy-policy` = plan 002 + 007)* Revision
   `houroffice-api-00001-r99`, **`https://houroffice-api-770124643502.europe-west1.run.app`**, runtime account
   `houroffice-api@…`, maxScale 2, 512 MiB, startup CPU boost. Notes from doing it:
   - Added `backend/.gcloudignore` (same exclusions as `.dockerignore`) — without it gcloud would upload
     `HourOffice.Tests/bin` (~0.5 GB); 28 source files are uploaded now.
   - On Windows `gcloud` is a `.cmd`, so `--set-env-vars` with a custom `^|^` delimiter breaks in cmd.exe;
     the plain env vars went in an `--env-vars-file` YAML (no secrets in it).
   - Admin `benny.hot4@gmail.com`, `AllowOpenSignup=false`, `Auth__GoogleClientId` = the step 6 client.
   - *(2026-11-22)* Open signup turned on with
     `gcloud run services update houroffice-api --region europe-west1 --update-env-vars Auth__AllowOpenSignup=true`
     (changes only that variable; secrets and other env vars untouched). Revision `houroffice-api-00003-fgt`
     (previous: `houroffice-api-00002-h66`); `/api/settings/public` returns `allowOpenSignup:true`, and the
     owner confirmed the new user can sign in. The next full `gcloud run deploy` must pass
     `Auth__AllowOpenSignup=true` in its `--env-vars-file`, or it switches signup back off.
   - Artifact Registry repo `cloud-run-source-deploy` (europe-west1, image ≈ 91 MB) has the cleanup policy
     keep-newest-2 + delete-older-than-1d.
   Checks: `/api/settings/public` 200 with the client id and `devLoginEnabled:false`; CORS header only for
   `https://houroffice.pages.dev` (none for a foreign origin); `POST /api/auth/dev-login` → 404;
   `/api/offices` and `/api/me` without a token → 401; terms version 1 seeded in Atlas with clause 8.6 (so the
   Atlas connection string and the `houroffice-prod` user work).
   ⚠️ **`/healthz` cannot be used on `*.run.app`:** Google's front end reserves URL paths ending in `z` and
   answers 404 before the request reaches the container. Use `/api/settings/public` as the smoke check for
   now; follow-up (also for plan 004's post-deploy check): add a `/health` alias in `Program.cs`.
8. **Deploy the site**: set `apiBaseUrl` in a production copy of `frontend/config.js` to the Cloud Run URL
   and upload `frontend/` with `npx wrangler pages deploy` (the repo's `config.js` keeps the localhost
   value; 004 will generate it in CI).
   ✅ *(done 2026-11-17, commit `4f65496`)* Copied `frontend/` without `dev-server.mjs` to a scratch folder,
   wrote `config.js` with `apiBaseUrl: "https://houroffice-api-770124643502.europe-west1.run.app"`, and ran
   `npx.cmd wrangler@4 pages deploy <folder> --project-name=houroffice --branch=main`: 33 files, production
   **`https://houroffice.pages.dev`**. (On Windows, `npx.cmd` breaks on arguments containing spaces —
   pass `--name=value` without spaces.) The first requests to the brand-new `pages.dev` host returned 522
   for a few seconds, then 200. Checked: login page renders the Google button, no dev login, no console
   errors; `/PrivacyPolicy` and deep links such as `/MyBookings` return the app (SPA fallback via `_redirects`).
9. ✅ **First sign-in**: sign in with the admin account, check Branding, add an office with an image, make a
   booking, cancel it.
   *(done 2026-11-17, all in Cloud Run logs: Google sign-in, profile, terms acceptance 200; admin role
   confirmed by image upload to R2 (`POST /api/admin/upload` 200) and office creation
   (`POST /api/admin/offices` 201); booking created (201) and cancelled (200). Branding left at defaults
   for the owner to set when ready.)*
10. ✅ Rewrite README "פריסה" as this runbook (no secret values, only names), update `.doc/architecture.md`,
    add a backlog line.
    *(done 2026-11-18: README "פריסה" = hosting table, API/site redeploy, secret rotation, Windows notes,
    rollback, inviting users; the admin e-mail and R2 account id are placeholders there. `.doc/architecture.md`
    has a "Hosting" section; backlog lines for 006 and 007 checked. Plan stays active for the follow-ups:
    rotate the R2 token, `/health` alias, and the 7-day $0 billing check.)*

## Validation
- [ ] `curl --fail https://<api-url>/api/settings/public` → 200 (`/healthz` is reserved on `*.run.app`, see step 7).
- [ ] `curl -s https://<api-url>/api/settings/public` returns the Google client id and the ILS currency.
- [ ] A request from a foreign origin (`curl -H "Origin: https://evil.example" -I …/api/settings/public`)
      gets **no** `Access-Control-Allow-Origin` header; the Pages origin gets one.
- [ ] `curl -X POST https://<api-url>/api/auth/dev-login` → 404 (dev login is off in Production).
- [ ] The Pages site loads at 360 px, Google sign-in works, the admin account lands on the admin menu.
- [ ] An uploaded office image is served from the R2 public URL and still loads after a Cloud Run
      redeploy (proves it is not on the container disk).
- [ ] Deep link `https://<site>/MyBookings` loads (SPA fallback via `_redirects`).
- [ ] `gcloud run services describe houroffice-api` shows `maxScale: 2`, `minScale` unset/0, and the
      three env vars coming from secrets, not literals.
- [ ] `git grep -iE "mongodb(\+srv)?://[^l]"` finds nothing; no secret value in the repo or README.
- [ ] Billing → Budgets shows the $5 budget; after 7 days the billing report shows $0.00 net cost.

## Risks
- **Open Atlas network access** (`0.0.0.0/0`) → DB user limited to one database, long random password
  stored only in Secret Manager; Atlas alerts on. A fixed outbound IP (Cloud NAT) costs money and is not
  worth it at this size.
- **Surprise bill** from a bug or abuse → `max-instances 2`, the existing `"auth"` rate limit, budget
  alerts. Budget alerts only notify, they do not stop spending; `max-instances`
  is the hard cap.
- **Cold start** after idle (Q6) → startup CPU boost; the site shows its skeletons meanwhile.
- **Atlas M0 limits** (512 MB, shared CPU, ~500 connections) → far above HourOffice's needs; the
  `MongoClient` is a singleton so connections stay low.
- **Wrong `apiBaseUrl` / CORS / OAuth origin** → validation items above check all three together.
- **Free-tier terms change** → re-check the pricing pages at each release; the budget alert catches it.

## Rollout Order
1. Answer the Open Questions.
2. Steps 1–6 (accounts, secrets, OAuth) — nothing is public yet.
3. Step 7 (API) and its `/healthz` + CORS + dev-login checks.
4. Step 8 (site) and the end-to-end checks with the admin account.
5. ~~Invite users (admin pre-creates them), keep `AllowOpenSignup=false` until then.~~ Superseded
   2026-11-22: signup is open (Q7), no invitation needed.
6. Step 10 docs; then plan 004 automates the same deploy.

## Rollback
- **API:** `gcloud run services update-traffic houroffice-api --to-revisions=<previous>=100` (instant);
  or delete the service — nothing else depends on it.
- **Site:** Cloudflare Pages → Deployments → "Rollback to this deployment".
- **Whole go-live:** delete the Cloud Run service, the Pages project and the R2 bucket; disable the
  billing account. No database schema changes are made; the `houroffice` data stays in Atlas.

## Sources
- Cloud Run pricing and free tier: https://cloud.google.com/run/pricing
- Firebase pricing (Hosting Spark limits): https://firebase.google.com/pricing
- Google AI Pro / Ultra developer benefits (monthly Cloud credits):
  https://blog.google/innovation-and-ai/technology/developers-tools/gdp-premium-ai-pro-ultra/ ,
  https://developers.google.com/program/my-benefits
- Cloudflare Pages Direct Upload: https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration
