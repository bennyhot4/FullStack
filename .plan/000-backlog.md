# Prioritized Backlog

Format:
- `- [ ] <title>`
- `- [ ] <title> | figma:<url>`       optional design reference
- `- [ ] <title> | stack:frontend`    limits the task to `frontend/` (no API changes)
- `- [ ] <title> | stack:backend`     limits the task to `backend/` (no UI changes)

A task with no `stack:` marker touches both the API and the site.

Current queue:
- [ ] Email booking invitations (.ics) on confirm + reminders before the booking, lead time set by admin list / user default / per booking (needs §0 #4 change; after 002) — plan: `.plan/003-2026-11-16-booking-reminders.md`
- [ ] CI/CD: harden CI (SHA pins, permissions, CodeQL, zizmor, Dependabot), E2E in CI, auto-deploy staging on merge and prod on release tag with approval (Cloud Run + Cloudflare Pages, OIDC) — plan: `.plan/004-2026-11-16-ci-cd-pipeline.md`
- [ ] Late cancellation: admin sets a cancellation notice (hours); the user is warned when cancelling inside it, and admins see the cancel date/time plus a red X "less than the approved time" — plan: `.plan/005-2026-11-17-late-cancellation.md`
- [ ] New users auto-added as a client of each admin (on profile completion) + private distribution lists for every user (API open to all, UI admins-only for now; no sending) + terms clause 8.7 in version 1 (approved, in progress on `feat/distribution-list`) — plan: `.plan/008-2026-11-18-distribution-list.md`


## DONE
- [x] Private client list per user (2 phones + 2 emails, client on booking, filter My bookings by client, Admin nav menu; groundwork for future WhatsApp reminders) + terms clause 8.6 he/en — plan: `.plan/002-2026-11-16-client-contacts.md`
- [x] Low-cost go-live (~$0/month, live 2026-11-17 at https://houroffice.pages.dev; follow-ups: rotate the R2 token, `/health` alias): API on Cloud Run (scale to zero, max 2), site on Cloudflare Pages, images on R2, existing Atlas M0, secrets in Secret Manager, $5 budget alert; one-time setup that 004 then automates — plan: `.plan/006-2026-11-17-low-cost-deployment.md`
- [x] Public privacy policy page `/PrivacyPolicy` (he/en, static, linked from login + terms) so the Google OAuth app can be published — plan: `.plan/007-2026-11-17-privacy-policy-page.md` | stack:frontend
- [x] Remove the office search and capacity filter from the home page — plan: `.plan/001-2026-11-16-remove-office-search.md`
- [x] Setup
- [x] ASP.NET Core 8 API + MongoDB + integration tests
- [x] Mobile-first HTML/JS frontend + e2e smoke test + Dockerfile + CI
