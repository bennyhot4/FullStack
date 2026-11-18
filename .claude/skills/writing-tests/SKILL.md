---
name: writing-tests
description: Write or review backend integration tests and the e2e smoke test. Use when adding tests for a new feature, writing a regression test for a bug fix, deciding what needs coverage, or judging whether a test suite is good enough to ship. Covers the xUnit TestApp harness, required coverage areas, fixtures, flakiness rules, and PR expectations.
---

# Writing Tests

## Principles
- Test **behavior** through the HTTP API, not implementation details.
- Keep tests deterministic and isolated.
- Every bug fix gets a regression test when feasible.

## Must be covered
- Booking rules and state transitions (hours, days ahead, overlaps, cancel, recurring created/skipped).
- API validation and error responses (assert the `code`).
- Auth and role boundaries: 401 without a token, 403 for a user on `/api/admin/*`,
  profile/terms gates.
- Persistence-critical paths, especially unique indexes (double booking, unique email).
- User-facing failure flows for key features (in the e2e smoke where it matters).

## This repository

| Suite | Location | Run |
|---|---|---|
| Backend integration (xUnit) | `backend/HourOffice.Tests/` | `cd backend && dotnet test` |
| One class / one test | | `dotnet test --filter "FullyQualifiedName~BookingTests"` |
| Frontend gate | `frontend/src/**` | `npm run typecheck`, `npm run lint`, `npm run build` in `frontend/` (what CI runs) |
| E2E smoke (Playwright, phone size, he + en) | `e2e/smoke.mjs` | `NODE_PATH=$(npm root -g) node e2e/smoke.mjs` — needs API + site running on an **empty** DB |

### Backend harness (`TestApp.cs`)
- Tests join `[Collection("api")]` and get a `MongoFixture` (one real temporary `mongod` per run,
  from the EphemeralMongo7 package — no installed MongoDB needed).
- `new TestApp(mongo)` gives each test class its own random database.
- Time is frozen by `FakeClock` (Mon 2026-11-09 08:00 Israel time); move it via `app.Clock.Now`.
  Never depend on the real clock.
- Google is faked: credentials `good:<email>:<name>`. `LoginAsync(email)` returns a client that
  already completed profile + terms (`ready: false` to test the gates); `AdminAsync()` signs in
  `owner@example.com` as admin; `CreateOfficeAsync(admin, ...)` creates an office.

## Structure
- Arrange → act → assert, one primary intent per test, descriptive names.
- No shared mutable state between tests and no reliance on execution order.

## PR expectations
- New features ship with a happy-path **and** a failure-path test.
- When a test fails, fix the code — not the test — unless the behavior change is intentional.
