# Prioritized Backlog

Format:
- `- [ ] <title>`
- `- [ ] <title> | figma:<url>`       optional design reference
- `- [ ] <title> | stack:frontend`    limits the task to `frontend/` (no API changes)
- `- [ ] <title> | stack:backend`     limits the task to `backend/` (no UI changes)

A task with no `stack:` marker touches both the API and the site.

Current queue:
- [ ] CI/CD: harden CI (SHA pins, permissions, CodeQL, zizmor, Dependabot), E2E in CI, auto-deploy staging on merge and prod on release tag with approval (Cloud Run + Cloudflare Pages, OIDC) — plan: `.plan/004-2026-11-16-ci-cd-pipeline.md`


## DONE
- [x] Remove the office search and capacity filter from the home page — plan: `.plan/001-2026-11-16-remove-office-search.md`
- [x] Setup
- [x] ASP.NET Core 8 API + MongoDB + integration tests
- [x] Mobile-first HTML/JS frontend + e2e smoke test + Dockerfile + CI
