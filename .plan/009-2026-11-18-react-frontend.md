# Rebuild the site (client) in React

Status: done
Owner: bennyhot4
Last updated: 2026-11-22

## Goal
Move `frontend/` from vanilla ES modules (no build) to **React** with a build step, with **the same
screens, routes, texts, look and behaviour**, without raising the monthly cost (today ~$0, plan 006)
and without breaking the binding decisions in `docs/PLAN.md` §0. The point is easier long-term
development (components, state, an ecosystem), not new features.

`stack:frontend`: no API, collection, index, seed or i18n-key changes.

## Scope

### Today (measured 2026-11-18)
| | Today |
|---|---|
| Files in `frontend/` | 36, **232 KB** raw, ~43 KB gzip (JS + CSS) |
| Runtime dependencies | **0** (no `package.json` in `frontend/`) |
| Build | none; CI only runs `node --check` |
| First load on a phone | `app.js` + `store/ui/i18n/api` + one lazy page ≈ 15–20 KB gzip |
| Deploy | `wrangler pages deploy` of a copy of `frontend/` with production `config.js` (plan 006, 004) |

### In scope
- **Tooling:** `frontend/package.json` + `package-lock.json`, **Vite** (static SPA build only),
  `@vitejs/plugin-react`, **TypeScript** (`strict: true`, `noEmit` — Vite strips types, `tsc` only
  checks). Output goes to `frontend/dist/` (gitignored). No SSR, no Next.js, no server runtime of any
  kind (see Risks → "costs that can jump").
- **Runtime dependencies (the whole list):** `react`, `react-dom` (React 19). Dev-only: `vite`,
  `@vitejs/plugin-react`, `typescript`, `@types/react`, `@types/react-dom`, `eslint` +
  `typescript-eslint` + `eslint-plugin-react-hooks`. No UI kit, no CSS framework, no state library,
  no router library, no icon library.
- **API types:** `src/types/api.ts` mirrors the request/response DTOs in
  `backend/HourOffice.Api/Endpoints/Dtos.cs` and `Models.cs` (camelCase, as the JSON policy produces).
  Hand-written, no code generator; a DTO change in the API updates this file in the same PR.
- **Layout after the move:**
  ```
  frontend/
    index.html            Vite entry (same <head>, manifest, icon, theme-color)
    tsconfig.json         strict, jsx: react-jsx, noEmit
    public/               copied as-is into dist/: config.js, _redirects, _headers, sw.js,
                          manifest.json, assets/
    src/
      main.tsx            createRoot, providers
      app.tsx             route table + guards (port of js/app.js)
      router.tsx          tiny history router (useRoute, <Link>, navigate) — no dependency (Q3)
      api.ts              port of api.js with types (fetch wrapper, refresh, ApiError)
      types/api.ts        API DTO types (see above); global.d.ts declares window.HOUROFFICE_CONFIG
      i18n.ts             same keys/strings, `TranslationKey` type so t("typo") fails tsc;
                          + LanguageContext / useT() hook
      store.tsx           SettingsContext + MeContext (port of store.js), branding → CSS variables
      ui/                 Button, Sheet (openSheet/confirmSheet), Toast, ErrorBox, Icon, PageHeader,
                          Skeletons, Empty — same class names as today
      components/         Calendar, Carousel, ImageInput, Markdown, Clients
      pages/              one file per page, same names as today (home.tsx, bookOffice.tsx, …)
    css/base.css, css/components.css   kept as they are (imported from main.tsx)
  ```
- **All 15 pages ported 1:1:** welcome, login, register, terms, privacy, home, bookOffice, myBookings,
  myClients, adminMenu, adminOffices, adminUsers, adminBookings, adminAnalytics, siteSettings, + 404 and
  Access Restricted. Routes and paths unchanged (`/BookOffice?id=`, `/AdminUsers`, `/PrivacyPolicy`, …).
- **Same DOM contract:** keep the class names, `href`s, `aria-current`, `dialog.sheet` etc. that CSS
  and `e2e/smoke.mjs` rely on, so the CSS and the smoke test stay **unchanged** and prove parity.
- **Runtime config stays runtime:** `config.js` stays a plain `<script>` in `public/` (not a Vite
  `import.meta.env` value), so **one build is deployed to staging and production** and §0 #2
  ("the only link is `apiBaseUrl` in `config.js`") still holds.
- **Service worker:** `sw.js` cache name → `houroffice-shell-v2` (drops the old cache of `/js/*.js`);
  shell list = `/`, `/index.html`, `/config.js`, `/assets/icon.svg`. Hashed `/assets/*.js|css` are
  cached on first use as today (network-first).
- **`public/_headers`** (new): `/assets/*` → `Cache-Control: public, max-age=31536000, immutable`;
  `/index.html`, `/config.js`, `/sw.js` → `no-cache`.
- **Dev workflow:** `npm run dev` (Vite on :5500, SPA fallback built in) replaces `dev-server.mjs`,
  which is deleted (Q6); `.claude/launch.json` `web` entry updated.
- **CI (`.github/workflows/ci.yml`):** `frontend` job becomes `npm ci` → `npm run typecheck`
  (`tsc --noEmit`) → `npm run lint` → `npm run build` → print a size report of `dist/` (raw + gzip per
  file; informational, **no budget that fails CI** — Q7). `e2e` job builds the site and serves `dist/`
  with `vite preview --port 5500`. Dependabot gets an `npm` entry for `/frontend` (7-day cooldown,
  same as `/e2e`). Plan 004 deploy steps change from "deploy `frontend/`" to "build, write `dist/config.js`,
  deploy `dist/`".
- **Docs/rules updated in the same branch:** `.claude/rules/code-style.md` (JS section),
  `.claude/rules/ui-and-styling.md` (building blocks are now components), `CLAUDE.md` + `AGENTS.md`
  (commands, frontend architecture), `.doc/architecture.md`, `.doc/operations.md` (local run + deploy
  commands), `.github/copilot-instructions.md`, `.claude/agents/frontend.md`.

### Out of scope
New features or UI changes, generating API types from the backend, unit-test framework (Vitest — later if wanted), SSR/SEO,
changing the host, any API change, renaming i18n keys.

## Assumptions
- React is wanted for maintainability; the app stays a client-only SPA on Cloudflare Pages.
- Node 22 LTS in CI (already used); Node is installed on the dev machine (v24).
- The repository is **public** (checked 2026-11-18), so GitHub Actions minutes are free and unlimited
  on standard runners; extra build time costs nothing.
- Traffic stays small (one business, tens of users), as in plan 006.
- Port from vanilla JS keeps the existing rules: no `innerHTML` ⇒ in React **no
  `dangerouslySetInnerHTML`**; Markdown keeps building elements (now React elements).

## Open Questions
All seven answered 2026-11-18 by the owner: recommended answers approved for Q1, Q3, Q4, Q5, Q6;
**Q2 → TypeScript from the start; Q7 → no budget, size report only.** The plan above reflects the
answers. Original questions kept for the record:

1. **This contradicts the master plan's stack line ("Vanilla, בלי framework") and
   `.claude/rules/code-style.md` ("no framework, no bundler, no npm dependencies in `frontend/`").**
   §0 itself is not broken (site is still a separate static deployment). Approve changing the rule?
   **Recommended: yes**, update the rule files in this branch; `docs/PLAN.md` stays as is (per
   `CLAUDE.md`, newer `.plan/` files win).
2. **JavaScript (JSX) or TypeScript?** TS adds a type-check step and DTO types for the API, no runtime
   cost. Recommended: JSX now. **Answer: TypeScript from the start.**
3. **Router: own tiny router or `react-router`?** `react-router` adds ~15–20 KB gzip and a dependency
   to keep updated; the current route table + guards are ~200 lines. **Recommended: own router**
   (`router.tsx`, history API + `React.lazy` per page).
4. **React or Preact (`preact/compat`)?** Same code style, ~4 KB instead of ~60 KB gzip, but some React
   libraries/devtools behave differently. **Recommended: React 19** (you asked for React; the extra
   ~50 KB is acceptable — see Risks), keep the build so a later switch to Preact is a 2-line alias.
5. **Big bang or page by page in production?** Mixing vanilla pages and React under one router is
   possible but doubles the routing/guard code during the move. **Recommended: port page by page
   in commits on one branch, release once** when all pages + smoke pass.
6. **e2e: how is the built site served?** **Recommended: `npx vite preview --port 5500`** (SPA fallback,
   no extra code) and delete `dev-server.mjs`.
7. **Bundle budget?** **Recommended: initial JS ≤ 90 KB gzip, each page chunk ≤ 15 KB gzip, total
   `dist/` ≤ 1 MB**; CI fails above it. **Answer: no budget** — CI only prints a size report.

## Steps
1. Branch `feat/react-frontend` from `claude/app-development-plan-imirl7`.
2. Scaffold: `frontend/package.json` (scripts `dev`, `build`, `preview`, `typecheck`, `lint`), Vite
   config (`base: "/"`, `build.outDir: "dist"`, `build.target: "es2020"`, port 5500), `tsconfig.json`,
   ESLint config, `.gitignore` (`frontend/node_modules`, `frontend/dist`). Write `src/types/api.ts`
   from `Dtos.cs`/`Models.cs` before porting pages. Move `config.js`, `_redirects`, `sw.js`,
   `manifest.json`, `assets/` into `public/`; add `_headers`.
3. Port the core: `api.js` (unchanged), `i18n.js` + `useT()`/language provider (sets `dir`/`lang` on
   `<html>`, `localStorage.appLanguage`), `store.ts` (settings + me, branding CSS variables),
   `router.tsx`, `app.tsx` with the guard order signed in → profile → terms → admin, full-screen
   spinner, bottom nav, top corner buttons.
4. Port shared UI (`ui.js` → `ui/*`): Sheet (native `<dialog>` as today), toast queue, icons
   (same `PATHS`), errorBox mapping `ApiError.code` → text.
5. Port components: Markdown (elements, no HTML strings), Calendar, Carousel, ImageInput, Clients.
6. Port pages in this order (smoke-test coverage first): login (Google Identity Services script
   loaded once in an effect), register, terms, home, bookOffice, myBookings, welcome, privacy,
   myClients, adminMenu, adminOffices, siteSettings, adminUsers, adminBookings, adminAnalytics, 404.
7. Delete the old `frontend/js/` tree and `dev-server.mjs` (Q6).
8. CI + Dependabot + size report script (`frontend/scripts/size-report.mjs`: raw + gzip of each file
   in `dist/`, printed to the log, always exits 0).
9. Update `.claude/launch.json`, rules, `CLAUDE.md`, `AGENTS.md`, `.doc/*`, frontend agent, and plan 004
   deploy steps.
10. Staging/preview deploy: `wrangler pages deploy frontend/dist --branch=react-preview` (a Cloudflare
    preview URL, free) → manual check on a real phone (he + en, admin + user) → production.

### Implementation notes (2026-11-18)
Steps 1–9 done on `feat/react-frontend` (from `origin/claude/app-development-plan-imirl7`); step 10 and
plan 004's deploy job are still open. Decisions taken while implementing:
- Dependencies were installed with `npm install --before=2026-11-04` (a 14-day cooldown, like Dependabot's)
  and pinned exactly: React 19.3.1, Vite 8.3.5, TypeScript 6.0.3, ESLint 10.14.0.
- Hashed build output goes to `/app/*` (Vite `assetsDir: "app"`), so `_headers` can mark only it immutable;
  `/assets/icon.svg` from `public/` keeps a normal cache.
- Pages are lazy route chunks loaded by the guard in `app.tsx` (not `React.lazy`), so the guard order stays
  exactly as before. Each page is keyed by the location it was resolved for, so a page is not remounted
  while a redirect is in progress (a remount re-ran Register's redirect and looped).
- Sheets stay imperative (`openSheet`), each in its own React root inside `<dialog class="sheet">`.
- Data loading: `src/useAsync.ts`. Hand-written API types: `src/types/api.ts`.
- `he` in `src/i18n.ts` is typed as `typeof en`, so a missing translation fails the type check.
- Measured: `dist/` 30 files, 349 KB raw / 115 KB gzip; JS 108 KB gzip in total, of which the initial
  chunk is 83 KB gzip; page chunks 0.4–3.1 KB gzip. `node_modules` 118 MB (never deployed).
- Plan 008 (merged to the trunk while this was open) is ported: `/DistributionLists` page, the
  "registered user" line in the client sheet, the admin-only entry on `/MyClients`, the hidden marketing
  consent on `/Register`, its i18n keys and types. Its e2e steps pass against the React build.
- **Step 10, preview (2026-11-22):** built from `1731f25` and deployed with
  `wrangler pages deploy … --branch=react-preview` → `https://react-preview.houroffice.pages.dev`.
  - Size: 29 files, 118 KB gzip.
  - Checked and passing:
    - deep links return 200;
    - `/app/*` is served `immutable`, and the shell, `config.js` and `sw.js` are served `no-cache`;
    - `config.js` points at Cloud Run, and there is no `localhost` in `app/`.
  - The app cannot load data there: the API's CORS allows only `https://houroffice.pages.dev`. The phone
    check and the production deploy are planned in `.plan/010-2026-11-22-react-production-deploy.md`.
- **Production (2026-11-22):** deployment `da9b1fd2` on `https://houroffice.pages.dev`, after the owner's
  phone check on the preview. Rollback target: `fa3cf2be` (vanilla). Details are in plan 010.

## Validation
- [ ] `cd frontend && npm ci && npm run build` succeeds; `npm run typecheck` and `npm run lint` have
      0 errors; `grep -rn ": any\b\|as any" frontend/src` → no matches.
- [ ] `NODE_PATH=$(npm root -g) node e2e/smoke.mjs` passes **without changes to `e2e/smoke.mjs`**
      against the built `dist/` (he + en, phone viewport).
- [ ] Size report printed in the CI log; numbers recorded in the PR description (before/after).
- [ ] `dist/` contains `config.js`, `_redirects`, `_headers`, `sw.js`, `manifest.json`; `grep -r
      localhost dist/assets` finds nothing (API URL only in `config.js`).
- [ ] `grep -rn "dangerouslySetInnerHTML\|innerHTML" frontend/src` → no matches.
- [ ] Every route in the old `js/app.js` table exists in `src/app.tsx` with the same path and flags
      (checked by a side-by-side list in the PR description).
- [ ] Deep link `/MyBookings` on the preview URL loads (SPA fallback), and a reload after deploy picks
      up new hashed assets (no stale page from the service worker).
- [ ] At 360px: no horizontal scroll in he (RTL) and en on every page (screenshots from smoke).
- [ ] Branding colors from SiteSettings still change `--primary`, `--background`, `--card` live.
- [ ] Lighthouse mobile on the preview URL: Performance ≥ 90 (today's baseline measured first and
      recorded in the PR).
- [ ] Cloud Run request count after release is not higher per page view than before (no duplicated
      fetches from effects — React StrictMode double-run only in dev).

## Risks

### Deploy size (the folder that is uploaded)
| | Today | After (estimate) | Limit (Cloudflare Pages free) |
|---|---|---|---|
| Uploaded folder | `frontend/` copy, 232 KB, 36 files | `dist/`, ~400–600 KB, ~30–45 files (hashed chunks) | 20,000 files, 25 MiB per file |
| First load (gzip) | ~15–20 KB JS + 6 KB CSS | ~70–90 KB JS (React ~60 KB) + same CSS | — |
| `node_modules` | none | ~80–150 MB **on the dev machine and CI only, never uploaded** | — |

TypeScript adds nothing to the upload (types are stripped at build). The upload stays tiny and far
below every limit. **The real risk is `node_modules` or the source folder
being uploaded by mistake** (e.g. `wrangler pages deploy frontend` instead of `frontend/dist`): mitigated
by changing the deploy command in plan 004/operations to `frontend/dist` only and the `dist/` content check
in Validation.

### Costs that can jump — and how this plan avoids each one
| Possible cost | When it happens | This plan |
|---|---|---|
| Hosting bandwidth | Netlify free (100 GB/mo) or Firebase Spark (~10 GB/mo, site switched off) | Stay on **Cloudflare Pages**: unlimited bandwidth, $0 |
| Paid hosting plan | Next.js/SSR needs a server (Vercel Hobby is **non-commercial only** → Pro $20/user/mo; or Cloud Run/Workers CPU for SSR) | **No SSR, static SPA only** |
| Cloudflare build quota | Git-connected builds: 500 builds/month on free | Build in **GitHub Actions + Direct Upload** (does not use the quota) |
| CI minutes | Private repo: 2,000 free min/month | Repo is **public** → free; build adds ~1 min/job anyway |
| CI artifact storage | Uploading `dist/` or `node_modules` as artifacts | Only failure screenshots, `retention-days: 7` (as today) |
| API (Cloud Run) requests | Effects that fetch twice, polling, missing cache of `/api/settings/public` | Fetch once in providers; StrictMode double effects only in dev; Validation checks request count |
| R2 / Atlas | — | Unchanged |
| Paid libraries / services | UI kits with licences, error-tracking SaaS | Only `react` + `react-dom` (MIT) |

Expected monthly cost after release: **$0, unchanged**.

### Other risks
- **Supply chain:** `frontend/` goes from 0 to a few hundred transitive npm packages (mostly dev-only:
  Vite, ESLint). Mitigation: `package-lock.json`, `npm ci` only, Dependabot with 7-day cooldown, only 2
  runtime deps, dev deps never shipped; zizmor/CodeQL already in `security.yml`.
- **Slower first load on cheap phones:** ~+60 KB gzip JS to parse. Mitigation: `React.lazy` per page,
  size report in CI, Lighthouse check. With no budget (Q7) growth is not blocked automatically —
  reviewers compare the size report in each frontend PR.
- **API types drift:** `src/types/api.ts` is hand-written, so a backend DTO change can leave it stale
  without a compile error. Mitigation: PR checklist item "DTO changed → `types/api.ts` updated"; the
  e2e smoke catches the visible breaks.
- **More work up front:** TypeScript makes the port slower than a 1:1 JSX copy (typing props, API
  responses, `t()` keys), in exchange for catching mistakes at build time.
- **Stale app after deploy:** old service worker caching old chunk names → cache `v2`, network-first,
  `no-cache` on `index.html`/`sw.js`.
- **Behaviour drift in the port** (guards, RTL, sheets, focus): the unchanged smoke test + side-by-side
  route list + manual phone check.
- **Two moving parts in local dev:** Node + npm now required to run the site; documented in operations.
- **Time:** a full port of ~150 KB of code is the largest single frontend change so far; it freezes
  other frontend work (plan 005, 008 UI) until merged. Recommended to run after 008's UI lands, or
  rebase those pages into React.

## Rollout Order
1. Owner answers Open Questions → `Status: active`.
2. Implement on `feat/react-frontend` (Steps 2–9), PR to `claude/app-development-plan-imirl7`, CI green.
3. Preview deploy (`--branch=react-preview`) and manual check on a phone.
4. Production deploy of `dist/` (same command as plan 006, folder changed). No API deploy needed.
5. Watch Cloud Run requests and the $5 budget alert for a week.

## Rollback
- **Site:** Cloudflare Pages → Deployments → "Rollback to this deployment" on the last vanilla build
  (instant, no rebuild). The API is untouched, so nothing else needs rolling back.
- **Code:** revert the merge commit; the vanilla `frontend/js/` comes back with it.
- **Service worker:** the vanilla `sw.js` uses cache `houroffice-shell-v1` and deletes other caches on
  activate, so returning users self-heal on next load.
