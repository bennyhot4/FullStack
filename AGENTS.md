# Agent Instructions

## communication with me
- Please start all your responses with Hey You!

## Security
- Never commit or expose secrets (tokens, API keys, passwords, cluster credentials, secret values).
- The MongoDB Atlas connection string, the JWT signing key and S3/R2 keys live only in
  deployment env vars / secret managers — never in code, `appsettings*.json`, docs or chat.
  The values in `appsettings.Development.json` are local-only dev placeholders.

## Guardrails (Single Source of Truth)
- Guardrail logic lives in `.claude/hooks/`. It is wired into the runtime by
  `.claude/settings.json`, which is the only location Claude Code reads hook and
  permission config from — a hook script that is not listed there never runs.
- Do not duplicate permission or hook rules in other agent docs.
- If any instruction conflicts with the hooks, the hooks win.
- A dev-loop driver (not part of this repo yet) sets `AGENT_ROLE` when it spawns a
  sub-agent; that is what `.claude/hooks/enforce-agent-boundaries.js` uses to enforce
  per-role write paths. Interactive sessions have no `AGENT_ROLE` and are not restricted by it.

## Repository Layout
- `backend/` — ASP.NET Core 8 minimal API + MongoDB (`HourOffice.Api`) and its xUnit
  integration tests (`HourOffice.Tests`).
- `frontend/` — React 19 + TypeScript SPA built by Vite into static files (`dist/` is what gets deployed).
- `e2e/` — Playwright smoke test at phone size.
- `docs/PLAN.md` — the master development plan (Hebrew). Its section 0 decisions override
  everything else. `docs/` holds only this hand-written plan; generated output never goes there.
- `.doc/` — product definition, architecture overview, glossary, operations runbook.
- `.claude/rules/` — always-on constraints, imported below. Short by design.
- `.claude/skills/` — procedural know-how, loaded on demand by task.
- `.claude/agents/` — sub-agent definitions (orchestrator, frontend, backend, qa, security-reviewer).
- `.claude/hooks/` — guardrail hook implementations, wired by `.claude/settings.json`.
- `.plan/` — `000-backlog.md` is the task queue; `NNN-YYYY-MM-DD-*.md` are per-task plans.
- `.orchestrate/` — everything the dev loop generates (tickets, agent reports, QA report,
  API contract, traces). Gitignored except its README.

## Rules — always in context
@.claude/rules/code-style.md
@.claude/rules/naming.md
@.claude/rules/ui-and-styling.md
@.claude/rules/git-workflow.md

## Skills — load when the task calls for it
| Skill | Use it when |
|---|---|
| `writing-plans` | Creating, revising, or superseding a plan in `.plan/` |
| `writing-tests` | Adding or reviewing backend integration tests or the e2e smoke test |
| `error-handling` | Shaping an error response, status code, error code, or failure UX |
| `database-schema` | Adding or changing a MongoDB collection, document field, index, or seed |
| `cutting-a-release` | Choosing a version number or tagging a release |

## Product and Domain
- Product definition and acceptance criteria: `.doc/product-definition.md`
  (the full screen-by-screen spec is `docs/PLAN.md` §1 and §8).
- Architecture overview: `.doc/architecture.md`.
- Canonical domain terms: `.doc/glossary.md` — document a new shared term there
  before using it broadly.
- Keep these docs updated when API routes, auth/role boundaries, or collections/indexes change.
