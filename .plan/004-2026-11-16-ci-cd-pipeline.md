# CI/CD pipeline: harden CI, add E2E, deploy API and site

Status: active
Owner: bennyhot4
Last updated: 2026-11-16

## Goal
Every change reaches production the same safe way: a pull request runs all checks, a merge to the
trunk deploys automatically to **staging**, and a release tag promotes the **same build** to
**production** after one manual approval. No long-lived cloud keys are stored in GitHub, and a bad
release can be rolled back in under five minutes.

This follows `docs/PLAN.md` §0:
- #2 — the frontend (static site) and the backend (Docker container) are two separate deployments,
  linked only by `apiBaseUrl` in `frontend/config.js` and CORS on the API.
- #5 — the code is tested before it is deployed (this plan only deploys what CI has already proven).
- #7 — `Mongo__ConnectionString` lives only in the cloud secret manager, never in the repo, the
  workflow files or GitHub logs.

Online best practices this plan applies (sources at the end):
- Pin every third-party action to a full commit SHA; let Dependabot keep the pins current.
- Least-privilege `GITHUB_TOKEN` (`permissions: contents: read` by default, widen per job).
- Short-lived credentials via OIDC (Workload Identity Federation to Google Cloud) instead of JSON keys.
- Build once, deploy many: the image tagged with the commit SHA is promoted, never rebuilt.
- Protected branch with required status checks; GitHub Environments with a required reviewer for prod.
- Static analysis of the workflows themselves (zizmor) and of the code (CodeQL).

## Scope
In scope:
- **CI hardening** — `.github/workflows/ci.yml`: SHA-pinned actions, `permissions`, `concurrency`,
  `timeout-minutes`, triggers fixed so a PR does not run twice (push only on the trunk,
  plus `pull_request`), and the frontend syntax check fixed to really cover nested folders
  (today `frontend/js/**/*.js` without `globstar` only matches one level deep).
- **E2E job in CI** — runs `e2e/smoke.mjs` against a `mongo:7` service container, the API in
  Development mode and `frontend/dev-server.mjs`. Screenshots uploaded as an artifact on failure.
- **Docker build check on PRs** — `docker build backend` (no push) so a broken Dockerfile fails the PR.
- **Security workflows** — `.github/workflows/codeql.yml` (C# + JavaScript), `zizmor` on
  `.github/**` (all three in `.github/workflows/security.yml`), `actions/dependency-review-action` on PRs.
- **Dependabot** — `.github/dependabot.yml` for `github-actions`, `nuget` (`backend/`) and `docker`
  (`backend/Dockerfile`), weekly, grouped minor/patch.
- **CD workflow** — `.github/workflows/deploy.yml`:
  - `build-image`: build `backend/Dockerfile`, push to Google Artifact Registry tagged
    `${{ github.sha }}` (and the version tag on release).
  - `deploy-api-staging` / `deploy-api-production`: `google-github-actions/auth` (OIDC) +
    `google-github-actions/deploy-cloudrun` with the already built image; then smoke-check
    `GET /healthz` on the new URL.
  - `deploy-site-staging` / `deploy-site-production`: write `frontend/config.js` with that
    environment's `apiBaseUrl`, then `cloudflare/wrangler-action` → `npm ci && npm run build` in `frontend/`, then `wrangler pages deploy frontend/dist` (plan 009: write `config.js` into `dist/`)
    (Direct Upload; staging uses `--branch=staging`, prod the production branch).
- **GitHub settings (manual, documented in the README)** — branch ruleset on the trunk, Environments
  `staging` and `production`, repository variables and secrets listed below.
- **Docs** — README "פריסה" section rewritten; `.doc/architecture.md` gets a short "Delivery
  pipeline" section.

Out of scope: no application code changes, no new endpoints, collections, pages or i18n keys. No
custom domains/DNS (can follow later). No data migration from Base44 (§0 #6).

### Environments and configuration

| | staging | production |
|---|---|---|
| Trigger | push to trunk (after CI is green) | tag `v*.*.*` (per `cutting-a-release`) |
| Approval | none | required reviewer: `bennyhot4` |
| Cloud Run service | `houroffice-api-staging` | `houroffice-api` |
| Pages branch | `staging` | production branch |
| Mongo database | `houroffice_staging` | `houroffice` |
| `Auth__DevLoginEnabled` | `false` | `false` |

GitHub **variables** (not secret): `GCP_PROJECT_ID`, `GCP_REGION`, `GCP_WIF_PROVIDER`,
`GCP_DEPLOY_SA`, `AR_REPOSITORY`, `CF_PAGES_PROJECT`, `API_BASE_URL` (per environment).
GitHub **secrets** (per environment): `CLOUDFLARE_API_TOKEN` (Pages:Edit only), `CLOUDFLARE_ACCOUNT_ID`.
**Not in GitHub at all:** `Mongo__ConnectionString`, the JWT signing key, R2 keys — stored in Google
Secret Manager and mounted into Cloud Run with `--set-secrets`; the deploy service account only has
`secretAccessor` on those secrets.

## Assumptions
- The repository is **public** (checked: `gh repo view` → `PUBLIC`), so Environments with required
  reviewers, CodeQL and secret scanning with push protection are free.
- Hosting follows the README's existing suggestion: Google Cloud Run for the API, Cloudflare Pages
  for the site (Q1).
- One MongoDB Atlas cluster (the owner's existing one) holds both databases (Q3).
- Production stays at a single developer, so the "prevent self-review" option on the production
  environment stays **off** (otherwise the owner could never approve their own deploy).
- The integration tests keep using EphemeralMongo, so the `backend` CI job needs no service.

## Open Questions
All six answered 2026-11-16 by the owner: **recommended answers approved** (Cloud Run + Cloudflare Pages;
trunk = `master`, reached by a PR from the working branch, which fast-forwards since `master` only has
the initial commit; separate `houroffice_staging` DB; prod on tag `v*.*.*` + `workflow_dispatch`;
E2E on every PR; E2E non-blocking for the first week).

1. **Hosting providers?** Recommended: **Cloud Run + Cloudflare Pages** (already in the README, scale
   to zero, generous free tiers, OIDC supported). Alternative: Render/Fly.io for the API, Netlify for
   the site.
2. **Which branch is the trunk?** GitHub's default branch is `master`, but recent work merged into
   `claude/app-development-plan-imirl7`. Recommended: merge the work into **`master`** once, make it
   the only trunk, protect it, and delete the old working branch.
3. **Separate staging database?** Recommended: **yes**, database `houroffice_staging` on the same
   Atlas cluster with its own DB user limited to that database, so staging can never touch prod data.
4. **Prod trigger: tag or manual button?** Recommended: **release tag `v*.*.*`** (matches the
   `cutting-a-release` skill) plus `workflow_dispatch` with an image-SHA input, used for rollback.
5. **Run E2E on every PR or only on the trunk?** Recommended: **every PR** — the smoke test is short
   and it is the only check that exercises the frontend in a browser.
6. **Make E2E a required check from day one?** Recommended: **non-blocking for the first week**, then
   required once it has proven not flaky.

## Steps
1. Branch `chore/ci-cd-pipeline` from the trunk (after Q2 is answered).
2. Harden `ci.yml`: top-level `permissions: contents: read`; `concurrency` group per ref with
   `cancel-in-progress` for PRs; `timeout-minutes` per job; pin `actions/checkout`,
   `actions/setup-dotnet`, `actions/setup-node` to SHAs with a `# vX.Y.Z` comment; triggers `push: branches: [<trunk>]` + `pull_request`; syntax check via
   `find frontend/js -name '*.js' -print0 | xargs -0 -n1 node --check`.
3. Add the `docker` job to `ci.yml` (`docker build backend`, no push).
4. Add the `e2e` job to `ci.yml`: `services: mongo: image: mongo:7`, start the API in the
   background with `ASPNETCORE_ENVIRONMENT=Development`, start `node frontend/dev-server.mjs 5500`,
   wait for `/healthz` and `:5500`, `npx playwright install --with-deps chromium`, run
   `node e2e/smoke.mjs screenshots`, `actions/upload-artifact` the screenshots `if: failure()`.
   (Playwright is pinned in `e2e/package.json` + `e2e/package-lock.json` and installed with `npm ci`,
   which zizmor's `adhoc-packages` audit requires; `frontend/` stays dependency-free.)
5. Add `.github/dependabot.yml` and `.github/workflows/codeql.yml`; add a `workflow-lint` job running
   `zizmor` and a `dependency-review` job (PRs only).
6. One-time cloud setup (documented step by step in the README, run by the owner — not by CI):
   Artifact Registry repo; Workload Identity pool + GitHub provider **restricted by
   `attribute.repository == "bennyhot4/FullStack"`**; deploy service account with
   `run.admin`, `iam.serviceAccountUser`, `artifactregistry.writer`; Secret Manager secrets;
   Cloud Run runtime service account; Cloudflare Pages project + scoped API token.
7. Write `.github/workflows/deploy.yml` with the jobs in Scope. Each job declares only what it needs
   (`id-token: write` only on GCP jobs). The production jobs use `environment: production` and
   deploy the image digest produced for that commit; they never rebuild.
8. Post-deploy smoke in `deploy.yml`: `curl --fail <service-url>/healthz` and a fetch of the site's
   `config.js` checking the expected `apiBaseUrl`. A failure fails the workflow.
9. GitHub settings: ruleset on the trunk (require PR, required checks `backend`, `frontend`,
   `docker`, `workflow-lint`, later `e2e`; block force-push and deletion); Environments `staging`
   and `production` (production: required reviewer, deployment tags `v*.*.*` only); enable secret
   scanning + push protection; Actions setting "Read repository contents" as default token
   permission.
10. Update README "פריסה" and `.doc/architecture.md`; add a backlog line.

## Validation
- [ ] A PR shows the checks `backend`, `frontend`, `docker`, `e2e`, `workflow-lint`,
      `dependency-review`, `CodeQL` and each passes on the PR branch.
- [ ] A PR triggers **one** CI run, not two (push + pull_request).
- [ ] Adding a file `frontend/js/a/b/broken.js` with a syntax error fails the `frontend` job.
- [ ] Breaking the Dockerfile on a test branch fails the `docker` job.
- [ ] `zizmor .github/workflows` reports no high-severity findings.
- [ ] `grep -rE "uses: [^@]+@v[0-9]" .github/workflows` returns nothing (all actions SHA-pinned).
- [ ] Every workflow has a top-level `permissions:` block (`grep -L permissions .github/workflows/*.yml` is empty).
- [ ] `git grep -iE "mongodb(\+srv)?://[^l]"` finds no non-localhost connection string.
- [ ] Merging to the trunk deploys staging; `curl --fail https://<staging-api>/healthz` → `{"status":"ok"}`;
      the staging site loads and Google sign-in works there.
- [ ] Pushing tag `v0.1.0` waits for approval on `production`; after approval the prod Cloud Run
      revision uses the **same image digest** as staging.
- [ ] Direct push to the trunk is rejected by the ruleset.
- [ ] Rollback drill: `workflow_dispatch` with the previous image SHA restores the previous version
      and `/healthz` passes.

## Risks
- **E2E flakiness** blocks merges → start non-blocking (Q6), use Playwright's built-in waits, upload
  screenshots for diagnosis.
- **Compromised third-party action** (several 2025–2026 supply-chain incidents) → SHA pins,
  Dependabot review, minimal token permissions, no secrets in PR jobs from forks.
- **Over-broad OIDC trust** lets another repo impersonate the deployer → provider condition on the
  exact repository, prod additionally on `environment:production`.
- **Staging writes to prod data** → separate database and DB user (Q3).
- **CORS / `apiBaseUrl` mismatch** after a new URL → post-deploy check of `config.js` and
  `Cors__AllowedOrigins__0` set from the same variable.
- **Cost** — Cloud Run min instances 0, Artifact Registry cleanup policy (keep last 20 images).

## Rollout Order
1. CI hardening + Dependabot + CodeQL + zizmor (steps 2, 3, 5) — merge first; zero deploy risk.
2. E2E job (step 4), non-blocking.
3. Branch ruleset with required checks (step 9, first half).
4. Cloud setup + staging deploy (steps 6–8 for staging), verify for a few days.
5. Production environment + first release tag `v0.1.0`.
6. Make `e2e` a required check once it has a week of green runs.

## Rollback
- **API:** re-run `deploy.yml` via `workflow_dispatch` with the previous image SHA, or immediately
  `gcloud run services update-traffic houroffice-api --to-revisions=<previous>=100`.
- **Site:** Cloudflare Pages → Deployments → "Rollback to this deployment" on the previous one
  (or re-run the previous successful workflow).
- **Pipeline itself:** every change here is in `.github/` and docs; revert the merge commit. Disable
  the ruleset temporarily only if a broken required check blocks an urgent fix.
- No database changes are made, so no data rollback is needed.

## Sources
- GitHub — security hardening for Actions / CI/CD best practices (SHA pinning, least privilege):
  https://www.wiz.io/blog/github-actions-security-guide ,
  https://github.blog/news-insights/product-news/whats-coming-to-our-github-actions-2026-security-roadmap/
- zizmor (Actions static analysis): https://socket.dev/blog/hardening-github-actions-against-outside-threats
- Cloud Run + Workload Identity Federation:
  https://cloud.google.com/blog/products/devops-sre/deploy-to-cloud-run-with-github-actions ,
  https://oneuptime.com/blog/post/2026-02-17-how-to-set-up-continuous-deployment-to-cloud-run-using-github-actions-and-workload-identity-federation/view
- Cloudflare Pages Direct Upload in CI:
  https://developers.cloudflare.com/pages/how-to/use-direct-upload-with-continuous-integration
- Rulesets and environments:
  https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets ,
  https://docs.github.com/en/enterprise-server@3.18/actions/reference/workflows-and-actions/deployments-and-environments
