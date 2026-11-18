# Privacy policy page

Status: active
Owner: bennyhot4
Last updated: 2026-11-17

## Goal
Publish a public privacy policy at `/PrivacyPolicy` (Hebrew + English) that says honestly what HourOffice
collects, why, where it is stored and how to have it deleted, so that:
- Google lets the OAuth app move from "Testing" to "In production" (plan 006 step 6 is blocked on a
  privacy policy link in Google Auth Platform → Branding);
- users can read it before they sign in.

## Scope
`stack:frontend` — no API, collection, index or seed changes.

In scope:
- **Content** — new module `frontend/js/content/privacy.js` exporting `{ version, updated, he, en }`
  Markdown strings in the subset `components/markdown.js` already renders (`##` headings, `**bold**`,
  `-` lists, paragraphs). A JS module (not a fetched `.md` file) so it ships with the site, needs no
  request, and passes the `node --check` CI gate.
- **Page** — `frontend/js/pages/privacy.js`: `pageHeader` + a card with the version/date line and the
  rendered Markdown for the current language (`dir` per language), same look as `/TermsOfUse`.
- **Route** — `/PrivacyPolicy` in `js/app.js` with `nav: false, optionalAuth: true` (readable signed out,
  no chrome when anonymous — exactly like `/TermsOfUse`). Back goes to `/` when signed in, `/login` otherwise.
- **Links** — on the login page under "View terms of use"; at the end of the terms page.
- **i18n keys** (both `en` and `he`): `privacyTitle`, `privacyViewPolicy`, `privacyLastUpdated`.
- **Docs** — `.doc/product-definition.md` (scope line + acceptance criterion), `.doc/glossary.md` only if a
  new term is introduced (none expected).
- **After deploy (plan 006):** set Branding → "Application privacy policy link" to
  `https://houroffice.pages.dev/PrivacyPolicy`, then "Publish app".

Out of scope: cookie banner (the site sets no cookies and loads no analytics), a self-service
"delete my account" button, admin editing of the policy, changes to the terms of use.

### What the policy states (facts checked in the code on 2026-11-17)
- **Collected from Google sign-in:** e-mail, full/first/last name, profile picture URL, Google account id.
  Only the `email` and `profile` scopes; no Google password, contacts, Drive or other data.
- **Entered by the user:** phone number; bookings (office, date, hours, notes, price); the private client
  list (names, up to 2 phones and 2 e-mails, language, WhatsApp consent with its time, notes); terms
  acceptance version and time.
- **Who sees what:** client list and client names on bookings are visible only to their owner — admin
  screens never show them; admins see users, bookings and booking notes.
- **Stored:** MongoDB Atlas (Google Cloud, Belgium); API on Google Cloud Run (Belgium); uploaded office
  images on Cloudflare R2 (public); the site on Cloudflare Pages. Browser storage: sign-in tokens and the
  chosen language in `localStorage`; no cookies, no analytics, no ads, nothing sold or shared.
- **Server logs:** Cloud Run request logs (IP address, time, path) kept by Google for 30 days.
- **Deletion:** an admin deletes the account, which removes the user, their bookings, their client list
  and their sign-in tokens (`DELETE /api/admin/users/{id}`). Request by e-mail to the contact in Q1.
- **Changes:** a new version gets a new date on the page.

## Assumptions
- The site sets no cookies (sign-in tokens are in `localStorage`, see `CLAUDE.md`) and loads no analytics;
  the only third-party script is Google Identity Services on the login page.
- Google accepts a privacy policy hosted on the authorized domain `houroffice.pages.dev`; basic scopes
  (`email`, `profile`) need no verification, so no review delay.
- The text is a plain-language notice written for this app, not legal advice; the owner reviews it.

## Open Questions
Q2–Q4 answered 2026-11-17 by the owner: **recommended answers approved** (static text in code; data
deleted immediately, no backups on M0; links on the login page and the end of the terms page, not on
Register). Q1 answered: name and e-mail below.

1. **Who is named as the operator, and which contact e-mail goes in the policy?** Google and users need
   a way to ask for deletion. **Recommended:** the business name shown in Branding settings plus a
   dedicated contact address you choose (not necessarily your personal Gmail). Please give both.
   **Owner answered 2026-11-17:** the owner's own name and e-mail. E-mail: `benny.hot4@gmail.com` (the
   Google account that owns the project). Name: **בני חוטיאקוב / Benny Hutiakov**.
2. **Static text in code, or editable by the admin like the terms?** **Recommended: static** — it changes
   rarely, needs no API/DB work, and every change goes through review in git. Editable would need a new
   collection or setting, endpoints and an admin screen.
3. **How long is data kept after an account is deleted?** **Recommended:** "deleted immediately from the
   live database; Atlas free tier keeps no backups, so nothing remains" (true for M0, which has no backups).
4. **Where are the links?** **Recommended:** login page and the end of the terms page. Optionally also the
   Register page — say if you want it.

## Steps
1. Branch `feat/privacy-policy` from the trunk (after this plan is approved).
2. Write `frontend/js/content/privacy.js` (he + en) from the facts above and the answers to Q1/Q3.
3. Add `frontend/js/pages/privacy.js` and the `/PrivacyPolicy` route.
4. Add the links on the login and terms pages; add the i18n keys in both languages.
5. Update `.doc/product-definition.md`.
6. Check in the browser: signed out and signed in, he (RTL) and en (LTR), 360 px and 1440 px.
7. ✅ *(done 2026-11-17: link saved, app "In production")* After plan 006 deploys the site: set the privacy link in Google Auth Platform → Branding and publish
   the app.

## Validation
- [ ] `find frontend/js -name '*.js' -print0 | xargs -0 -n1 node --check` passes.
- [ ] Every new i18n key exists in both `en` and `he` in `frontend/js/i18n.js`.
- [ ] `/PrivacyPolicy` opens **signed out** without redirecting to `/login`, shows no bottom nav, and its
      back button goes to `/login`; signed in, back goes to `/`.
- [ ] The page renders the Hebrew text RTL and the English text LTR after switching language.
- [ ] No horizontal scroll at 360 px; text readable at 1440 px.
- [ ] The login page and the terms page each show a working "Privacy policy" link.
- [ ] `grep -rn "innerHTML" frontend/js` finds nothing new.
- [ ] Every data item the policy lists matches a field in `backend/HourOffice.Api/Models.cs` (review step).
- [ ] After deploy: Google Auth Platform → Audience shows **"In production"**, and signing in with an
      account that is not a test user works.

## Risks
- **Policy drifts from the code** (a new field is added later and not mentioned) → the plan template for
  future features gets a line "privacy policy still accurate?"; the version/date on the page shows staleness.
- **Google rejects the link** (unreachable or on a non-authorized domain) → publish the site first
  (plan 006 step 8), open the link in a private window before saving it in Branding.
- **Text read as legal advice** → plain-language notice, reviewed by the owner; a lawyer can replace it later
  without code changes beyond `content/privacy.js`.

## Rollout Order
1. Answer the Open Questions, approve the plan.
2. Implement on `feat/privacy-policy`, PR, merge.
3. Deploy the site (plan 006 step 8, or a redeploy if 006 is already live).
4. Set the privacy link in Google Branding and publish the OAuth app.

## Rollback
- Revert the merge commit and redeploy the site; the route and links disappear.
- In Google Auth Platform the app can be moved back to "Testing" (Audience → "Back to testing"); signed-in
  users keep working, new users must then be test users.
- No data or API changes, so nothing to migrate back.
