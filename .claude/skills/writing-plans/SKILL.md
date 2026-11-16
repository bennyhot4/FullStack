---
name: writing-plans
description: Create, revise, or supersede an implementation plan in .plan/. Use whenever the user asks for a plan, an approach, or a design doc for a task, when revising an existing plan after feedback, or when the dev loop asks the orchestrator for a plan. Defines the .plan/ lifecycle, the NNN-YYYY-MM-DD-topic.md filename, required metadata, and the nine required sections.
---

# Writing Plans

`.plan/` at the repository root is the source of truth for plans. `000-backlog.md`
is the task queue; every other file is a plan.

## Before writing anything
1. Read `docs/PLAN.md` §0 (binding decisions) and the sections relevant to the task. A task
   plan never contradicts §0 — if it must, the plan says so in `Open Questions`.
2. Read the existing `.plan/*.md` — do this even when the request names no plan
   file. A new plan must build on decisions already made, not contradict them.
3. Read `.doc/product-definition.md` for acceptance criteria and scope.
4. If `.plan/` does not exist, create it.

## Filename
`NNN-YYYY-MM-DD-<topic>.md`, with a sequential numeric prefix — `001`, `002`, `003`.
Example: `.plan/003-2026-08-03-comment-thread.md`.

## Required metadata
Near the top of every plan:

```
Status: draft | active | done | superseded
Owner:
Last updated: YYYY-MM-DD
```

New plans start as `Status: draft`. Only the human approval gate flips a plan to
`active`.

## Required sections
Every plan must contain all nine, in this order:

`Goal` · `Scope` · `Assumptions` · `Open Questions` · `Steps` · `Validation` ·
`Risks` · `Rollout Order` · `Rollback`

## Content rules
- Repository-relative paths only. Never machine-specific absolute paths.
- Generated artifacts belong in `.orchestrate/`. `docs/` holds only the hand-written
  master plan — never write generated output there.
- Respect the task's declared scope. A backlog task touches both the API and the site by
  default; `stack:frontend` or `stack:backend` limits it to one side — then do not plan
  changes on the other side.
- Name the endpoints, collections/indexes, pages and i18n keys the task adds or changes.
- `Validation` is what QA will use as its checklist, so make each item provable
  by a test or a command.

## Open Questions and approval
- Ask every plan question **inside the plan file**, not in chat.
- Make each question easy to answer, and include a recommended answer.
- Update the plan once answers arrive.
- Request approval before executing the plan.

## Superseding a plan
When a plan is replaced, set the old plan to `Status: superseded` and add a link
to the replacing plan.
