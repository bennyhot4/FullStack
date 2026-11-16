---
name: database-schema
description: Change the MongoDB data model. Use when adding or altering a collection, a document field, an index, or seed data, or when setting up a local database from scratch. Covers where the model is defined, the camelCase convention, indexes created at startup, additive-change rules, and seed data.
---

# Database Schema (MongoDB)

There is no SQL and no migration tool. The schema is the C# model plus the indexes the API
creates at startup.

## Source of truth
| What | Where |
|---|---|
| Document shapes | `backend/HourOffice.Api/Models.cs` |
| Collections | `Data/MongoContext.cs` — `users`, `offices`, `bookings`, `siteSettings`, `terms`, `refreshTokens` |
| Indexes | `MongoContext.EnsureIndexesAsync()` — runs on every API start, must stay idempotent |
| Seed data | `Services/TermsSeeder.cs` (initial terms from `Seed/terms.{he,en}.md`) |
| Design notes | `docs/PLAN.md` §4.2–4.3 |

## Conventions
- A convention pack maps C# PascalCase to **camelCase** element names and ignores unknown
  elements. Write queries in `mongosh` with camelCase (`role`, `hourlyRate`).
- Ids are `string` in C# stored as `ObjectId` (`[BsonRepresentation(BsonType.ObjectId)]`).
  Validate incoming ids with `Api.IsObjectId` before querying.
- Money is `decimal`; every office and booking also stores an ISO `currency` (default `ILS`).
- Dates of bookings are `yyyy-MM-dd` strings and hours are slot lists, so the
  `uniq_confirmed_slot` partial unique index can block double booking. Do not change that
  shape without replacing the guard.

## Changing the model
- Prefer **additive** changes: new fields get a sensible C# default, so old documents still
  deserialize (extra elements are ignored, missing ones get the default).
- Renaming or removing a field needs a one-off update script; describe it in the plan's
  Rollout/Rollback and run it against Atlas only with explicit approval.
- New query patterns get an index in `EnsureIndexesAsync`. Changing an existing index's options
  means dropping the old one first — Mongo will not alter it in place.
- Never drop a collection or database, or run `deleteMany({})`, against a shared or Atlas DB.

## Tests
Add an integration test in `backend/HourOffice.Tests/` for any index that enforces a rule
(uniqueness, partial filters) — see the `writing-tests` skill.
