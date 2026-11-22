# Deploy the React site to production

Status: active
Owner: bennyhot4
Last updated: 2026-11-22

## Goal
Replace the vanilla site that is live at `https://houroffice.pages.dev` (still serving `/js/app.js`, uploaded
2026-11-17 by plan 006) with the React build from plan 009 (`frontend/dist/`), with no API change, no
downtime, no new cost and a one-click way back. This is plan 009's Rollout Order steps 3–5.

`stack:frontend`: no API, collection, index, seed or i18n-key changes.

## Scope
### Today (checked 2026-11-22)
- **Production site:** vanilla build, Cloudflare Pages project `houroffice`, production branch `main`.
- **Production API (Cloud Run `houroffice-api`):** already has everything the React site calls. The plan 008
  routes `/api/clients` and `/api/distribution-lists` answer 401 without a token (they exist).
  `/api/settings/public` returns `auth.allowOpenSignup`. **No API deploy is needed.**
- **Preview deploy (plan 009 step 10): done 2026-11-22.**
  - Command: `wrangler pages deploy … --branch=react-preview`, built from `claude/app-development-plan-imirl7` at `1731f25`.
  - URL: `https://react-preview.houroffice.pages.dev`; 29 files, 118 KB gzip.
  - Checked and passing:
    - `/`, `/MyBookings` and `/PrivacyPolicy` return 200 (SPA fallback).
    - `/app/*` is served `immutable`; `/`, `config.js` and `sw.js` are served `no-cache`.
    - `config.js` holds the Cloud Run URL, and there is no `localhost` in `app/`.
  - **The app itself cannot load there:** the API's CORS allows only `https://houroffice.pages.dev`, so
    `/api/settings/public` is blocked and the page shows "Something went wrong" with "Try again". Google
    sign-in would also refuse that host until it is added as an authorized JavaScript origin. See Q1.

### In scope
- One manual production upload of `frontend/dist/` with the production `config.js`, using the runbook in
  `.doc/operations.md` → "עדכון האתר" (it already says `dist/`, never `frontend/`).
- A manual phone check before (Q1) and after the upload.
- Optional and temporary: add the preview origin to the API's CORS list and to the Google OAuth client (Q1).

### Out of scope
- Automating the deploy in CI (plan 004's deploy job). Until then every site release is this manual
  procedure.
- A custom domain, API changes, data changes.

## Assumptions
- `claude/app-development-plan-imirl7` CI is green on the commit that is built (all 4 jobs, e2e included).
- The `wrangler` login on the owner's machine can deploy to the `houroffice` project (true on 2026-11-22).
- Every previous Pages deployment stays in Cloudflare's list, so rollback needs no rebuild.
- Returning users have the vanilla service worker (cache `houroffice-shell-v1`). The new `sw.js` (cache v2)
  is served `no-cache`, takes over on the next visit and deletes the v1 cache.

## Open Questions
1. **How do we check the React site on a real phone before production?** **Answered 2026-11-22: (A).**
   - **Recommended: (A) open the preview to the API for a few hours.**
     - Add `https://react-preview.houroffice.pages.dev` as `Cors__AllowedOrigins__1` on Cloud Run. That makes
       a new revision: no code change, the same image, a few seconds.
     - The owner adds the same URL under "Authorized JavaScript origins" of the Google OAuth client in
       Google Cloud Console. Only the owner can do this, and it can take a few minutes to apply.
     - Then sign in on the phone (he + en, admin + user) against the real data, and remove both again
       after the production deploy.
   - (B) Skip the preview check: deploy to production at a quiet hour, check it there, and roll back in one
     click if anything is wrong. This is faster, but real users may meet a bug for a few minutes.
2. **When?** Recommended: a quiet hour (evening), not during booking hours.
3. **Remove the temporary CORS origin and OAuth origin afterwards (if Q1 = A)?** Recommended: yes, on the
   same day. A preview URL that may later serve another build should not be trusted by production.

## Steps
1. **Pick the build.**
   - Use the head of `claude/app-development-plan-imirl7` (`1731f25` at writing time); its CI run must be green.
   - Note in Cloudflare (Workers & Pages → `houroffice` → Deployments) which production deployment is
     current. This is the rollback target: the vanilla build from 2026-11-17.
2. **(Q1 = A) Open the preview.**
   - Run:
     ```
     gcloud run services update houroffice-api --region europe-west1 --update-env-vars Cors__AllowedOrigins__1=https://react-preview.houroffice.pages.dev
     ```
   - The owner adds the same origin in the Google OAuth client.
   - Check the preflight:
     ```
     curl -s -D - -o /dev/null -H "Origin: https://react-preview.houroffice.pages.dev" <api>/api/settings/public
     ```
     It must show `access-control-allow-origin`.
   - ✅ *(done 2026-11-22)* Cloud Run revision `houroffice-api-00004-585` has `Cors__AllowedOrigins__1`. The
     preflight returns the preview origin, production still returns its own, and other hosts get none. The
     preview loads settings and renders the login page with the Google button. The owner added the OAuth
     origin the same day, and the button then showed "Continue as".
   - ✅ *(2026-11-22, desktop browser at 360–375px, admin account, read-only)* Signed in with Google, then
     loaded every page with no error box and no horizontal scroll:
     - in en: home, My bookings, My clients, distribution lists, Admin, the 5 admin pages, terms and privacy;
     - in he (`dir=rtl`): the same pages plus `/BookOffice` (calendar, 14-day limit, ₪ rate, 15 slots).
     - `--primary` comes from Branding (`#3B6A8F`).
     - No booking was made. A real-phone check (and booking + cancel) is left to the owner.
   - ✅ The owner checked on a real phone and approved ("Passed, deploy").
3. **(Q1 = A) Phone check on the preview.** On a real phone, in he and in en:
   - login with Google, the home page, booking an office, My bookings and cancelling;
   - My clients, distribution lists (as admin), the admin pages and the Branding colors.
4. **Build and upload to production.** Follow `.doc/operations.md` → "עדכון האתר":
   - Run `cd frontend && npm ci && npm run build`.
   - Copy `frontend/dist/` to a scratch folder whose path has no spaces.
   - Write the production `config.js` (the Cloud Run URL) in that copy.
   - Run `npx.cmd wrangler@4 pages deploy <folder> --project-name=houroffice --branch=main`.
   - ✅ *(done 2026-11-22)*
     - Uploaded the same folder that was checked on the preview (built from `1731f25`, CI green). The
       rollback target was the vanilla production deployment `fa3cf2be`.
     - Result: production deployment `da9b1fd2` on `https://houroffice.pages.dev`; 29 files, all already
       uploaded by the preview.
5. **Check production.** Run every item under Validation.
   - ✅ *(2026-11-22)*
     - `/` references `/app/index-CtuNJlTw.js` and no longer `/js/app.js`.
     - `/`, `config.js` and `sw.js` are served `no-cache`, and the entry is served `immutable`.
     - `config.js` holds the Cloud Run URL.
     - `/MyBookings`, `/PrivacyPolicy` and `/AdminUsers` return 200.
     - In the browser:
       - a visitor who had the vanilla site is now on the React entry;
       - Cache Storage holds only `houroffice-shell-v2`;
       - the Google button renders;
       - no console errors.
     - Still open: the 7-day Cloud Run and budget watch.
6. **(Q1 = A) Close the preview.**
   - Run `gcloud run services update houroffice-api --region europe-west1 --remove-env-vars Cors__AllowedOrigins__1`.
   - The owner removes the origin from the Google OAuth client.
   - The preflight from step 2 must no longer show `access-control-allow-origin`.
   - ✅ *(2026-11-22)* API side done: revision `houroffice-api-00005-lds` has no `Cors__AllowedOrigins__1`.
     The preview origin gets no CORS header, and production still does. Still for the owner: remove the
     origin from the Google OAuth client "HourOffice web".
7. **Record.**
   - Add a "Production" note to plan 009 (date, deployment id, sizes); its status becomes `done`.
   - Tick the 009 backlog line.
   - Note in this plan the API revision changes from steps 2 and 6.

## Validation
- [ ] `curl -s https://houroffice.pages.dev/ | grep -o '/app/index-[^"]*\.js'` prints a hashed React entry, and
      `/js/app.js` is no longer referenced.
- [ ] Response headers, checked with `curl -D -`:
      - `/`, `/config.js` and `/sw.js` are served `Cache-Control: no-cache`;
      - the entry `/app/index-*.js` is served `public, max-age=31536000, immutable`.
- [ ] `curl -s https://houroffice.pages.dev/config.js` shows the Cloud Run URL (not localhost).
- [ ] Deep links `/MyBookings`, `/PrivacyPolicy` and `/AdminUsers` return 200 and render the app.
- [ ] Browser on `https://houroffice.pages.dev`, at phone width:
      - the Google button renders;
      - no console errors;
      - `/api/settings/public` returns 200.
- [ ] Real phone, he (RTL) and en, as admin and as a regular user:
      - sign in;
      - book an office, then cancel it;
      - My clients and distribution lists (admin);
      - Branding colors applied;
      - no horizontal scroll.
- [ ] **Returning visitor:** a phone that had the vanilla site open before the deploy shows the React site
      after one reload, and DevTools → Application → Cache Storage holds only `houroffice-shell-v2`.
- [ ] **Load and cost:** Cloud Run request count for the next 7 days is not higher per page view than the
      week before, and the $5 budget alert does not fire.
- [ ] **(Q1 = A) Preview closed again:** the preflight from step 6 has no `access-control-allow-origin`.

## Risks
- **Uploading the wrong folder** (`frontend/` with ~120 MB of `node_modules`). Mitigation:
  - the runbook uploads a copy of `dist/` only;
  - step 4 checks the folder listing before upload;
  - Pages' 25 MiB/file limit would reject a large file anyway.
- **Stale site for returning users** (the old service worker). Mitigation: `no-cache` on the shell and on `sw.js`,
  cache v2 deletes v1, and the "returning visitor" Validation item.
- **Google sign-in breaks.** The production origin is unchanged, so nothing is expected. If the button does not
  render, roll back first and then investigate.
- **Temporary CORS origin left behind (Q1 = A).** Step 6 removes it, and Validation proves it.
- **Cost.** Uploads to Pages are free and unlimited in bandwidth, a Cloud Run env change is free, and the build runs
  locally. Expected cost stays $0/month.

## Rollout Order
1. The owner answers the Open Questions, and the plan becomes `Status: active`.
2. (Q1 = A) Steps 2–3: open the preview and check it on a phone.
3. At the chosen hour, steps 4–5: production upload and checks.
4. (Q1 = A) Step 6: close the preview.
5. Step 7, then watch Cloud Run requests and the budget for 7 days.

## Rollback
- **Site (instant):** Cloudflare → Workers & Pages → `houroffice` → Deployments → the vanilla deployment from
  2026-11-17 → "Rollback to this deployment". The API is untouched. Users self-heal on the next load because
  the vanilla `sw.js` deletes caches other than v1.
- **API env (Q1 = A):** `--remove-env-vars Cors__AllowedOrigins__1`, or route traffic back to the previous
  revision: `gcloud run services update-traffic houroffice-api --to-revisions=<previous>=100`.
- **Preview:** harmless to leave. Without the CORS origin it cannot reach the API, so it has no data.
