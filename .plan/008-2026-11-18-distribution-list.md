# New users become admins' clients + distribution lists

Status: active
Owner: bennyhot4
Last updated: 2026-11-18
Approved: 2026-11-18. Branch `feat/distribution-list` is stacked on `docs/readme-overview` (which contains `feat/client`), because `feat/client` is not merged into the main branch yet.

## Goal
1. **New user → client of every admin.** When a new user joins, the system adds them as a
   `client` in the private client list of **each admin**. So the business can later reach its
   renters, for example with announcements.
2. **Distribution lists.** Any user (not only admins) can group clients from their own client list
   into named **distribution lists**. The API supports this for every user. **In this stage the site shows
   the feature to admins only** (Q6). Regular users do not see it.
3. **Groundwork only.** In the future a user can send an announcement to a distribution list. This
   plan **sends nothing**: no WhatsApp, SMS or email (`docs/PLAN.md` §0 #4, same as plan 002 Q1).

## Scope
In scope (both sides, no `stack:` marker). Builds on plan 002 (`clients`), which stays as is.

### A. Auto-add a new user as a client of each admin
- **Trigger: the user's first profile completion** (Q1), in `PUT /api/me` (`AuthEndpoints.cs`), the
  first time `PhoneNumber` changes from empty to a value. Not on sign-in: at sign-in a user has no phone
  yet, and a client needs at least one phone (plan 002 `phone_required`).
- New service `Services/AdminClientSync.cs` (`sealed`, primary constructor, `MongoContext` +
  `TimeProvider`), method `AddUserToAdminsAsync(User user, CancellationToken ct)`:
  - Loads all users with `role == "admin"` and `status == "active"`, **except the user themself**.
  - For each admin, inserts a `Client` with `userId = admin.Id`, `firstName`, `lastName`,
    `phones = [user.PhoneNumber]`, `emails = [user.Email]`, `language = "he"`,
    `whatsAppConsent = user.MarketingConsent` (Q4, `false` by default), `notes = ""`, and the new field
    `sourceUserId = user.Id`.
  - **No duplicates:** skips an admin who already has a client with `sourceUserId == user.Id`
    (unique partial index below, so a double request cannot add two).
  - **Limit:** skips an admin who already has `Client.MaxPerUser` (1000) clients, and logs a
    warning with ids only (no names, phones or emails).
  - A failure here **never fails** the profile update: catch, log a warning with ids, return the
    normal `PUT /api/me` response.
- **A user who becomes an admin gets all users automatically (Q2). No button.**
  `AdminClientSync.AddAllUsersToAdminAsync(User admin, CancellationToken ct)` adds every user with a
  completed profile (phone set), except the admin themself, to **that admin's** client list, with the
  same rules (skip duplicates through the unique index, limit, ids-only logging, a failure never
  fails the request). It runs whenever someone becomes an active admin:
  - `PUT /api/admin/users/{id}` (`AdminEndpoints.cs`) changes `role` from `user` to `admin`;
  - `SignInAsync` (`AuthEndpoints.cs`) promotes a user through `Auth:AdminEmails`, or creates a new
    user as admin;
  - an invited admin (`status: "invited"`, role `admin`) signs in for the first time and becomes
    `active`.
  - **Once, at startup** (`Program.cs`, after `EnsureIndexesAsync`): `FillAllAdminsOnceAsync` adds,
    for every active admin, the users still missing from their list — this covers the admins and users
    that exist on the day of the deploy. It then writes the marker key `admin_clients_filled` to
    `siteSettings` and never runs again on that database. (Decided while building: Cloud Run restarts
    the API often, and running it on every start would bring back copies an admin deleted.)
  An admin demoted back to `user` keeps their client list (it is their own private list, plan 002).
  This only ever writes into the admin's own list, so the "admins cannot see other users' clients"
  rule from plan 002 still holds.
- **Profile changes are synced (Q5).** When a user changes their profile in `PUT /api/me` (first name,
  last name or phone), `AdminClientSync.UpdateUserInAdminsAsync(user, ct)` updates **every** client
  with `sourceUserId == user.Id` in one `UpdateMany`: `firstName`, `lastName`, `phones[0]` and
  `emails[0]`, plus `updatedAt`. The other fields the admin may have added (a second phone, a
  second email, `language`, `notes`) are kept. If the admin edits one of these synced fields, the
  user's next profile change overwrites it. The admin's edits never change the user.
  An admin renaming a user in `PUT /api/admin/users/{id}` (`fullName`) is not synced (it only sets
  `fullName`, not first/last name).
- **Marketing consent, built but hidden (Q4).** `User` gets `MarketingConsent` (`bool`, default
  `false`) and `MarketingConsentAt` (`DateTime?`, set by the server from `TimeProvider` when it turns
  on, cleared when it turns off). `UpdateProfileRequest` accepts an optional `marketingConsent`;
  missing → unchanged. `UserDto` returns it. When the user creates or changes it, the sync copies it
  into the admins' clients as `whatsAppConsent` / `whatsAppConsentAt`. Auto-added clients start with
  the user's current value (`false` until the user opts in).
  Site: a checkbox "I agree to receive updates and offers" on `/Register` (`pages/register.js`),
  **rendered but hidden with `display: none`** (a `.is-hidden { display: none; }` class in
  `components.css`), so it can be turned on later with no other change. Since it is hidden, nobody can
  turn consent on from the site for now.
- **Deleting a user (Q7):** `DELETE /api/admin/users/{id}` also deletes the clients in other admins'
  lists whose `sourceUserId == id` (next to the existing `Clients.DeleteManyAsync(c => c.UserId == id)`),
  and removes those client ids from every distribution list.
- **Model:** `Client.SourceUserId` (`string?`, ObjectId, JSON/Mongo `sourceUserId`). `null` for
  clients a user typed in by hand. `ClientDto` returns it as `sourceUserId` (only to the owner, as
  with every client field).
- **Index:** `{ userId: 1, sourceUserId: 1 }` **unique, partial** (`sourceUserId` exists), created in
  `EnsureIndexesAsync`. Plan 002's "no uniqueness on phones/emails" rule is not changed.

### B. Distribution lists (all users in the API)
- **New entity and collection:** `DistributionList` in `Models.cs`, collection `distributionLists`
  in `Data/MongoContext.cs`:
  ```jsonc
  { "_id": ObjectId,               // returned as "distributionListId"
    "userId": ObjectId,            // owner, always the signed-in user
    "name": "",                    // 1–60 chars, required
    "description": "",             // max 300 chars
    "clientIds": [ObjectId],       // the owner's own clients only, no duplicates, max 1000
    "createdAt": ISODate, "updatedAt": ISODate }
  ```
  Limits as constants on the class: `MaxPerUser = 100`, `MaxClients = 1000`.
- **Index:** `{ userId: 1, name: 1 }` (list sorted by name). Names are not unique (same rule as clients).
- **API** — new `Endpoints/DistributionListEndpoints.cs`, group `/api/distribution-lists`,
  `.RequireUser().RequireTerms()`, same privacy pattern as `ClientEndpoints` (owner filter **inside**
  the MongoDB filter, a list owned by someone else → 404, `userId` from the body ignored):
  | Method | Path | Notes |
  |---|---|---|
  | GET | `/api/distribution-lists` | The caller's lists: `distributionListId`, `name`, `description`, `clientCount`, `updatedAt` |
  | POST | `/api/distribution-lists` | `{ name, description?, clientIds? }` → 201 |
  | GET | `/api/distribution-lists/{id}` | The list + its members (`clientId`, full name, first phone), sorted by name |
  | PUT | `/api/distribution-lists/{id}` | Replaces name, description and `clientIds` |
  | DELETE | `/api/distribution-lists/{id}` | Hard delete of the list only; the clients stay |
- **Membership check:** every `clientId` must be a client of the caller, checked with one query
  (`_id in ids && userId == me`). Any unknown or foreign id → 404 `client_not_found`, nothing saved.
  The same id twice is stored once.
- **Client deleted** (`DELETE /api/clients/{id}`): also `$pull` its id from the owner's lists. Reads
  ignore any id that no longer resolves, for safety.
- **Delete user cascade:** `DELETE /api/admin/users/{id}` also deletes that user's distribution lists.
- **Error codes** (`snake_case`, `{ code, message }`): `list_name_required` (400),
  `list_description_too_long` (400), `too_many_list_clients` (400, over 1000),
  `list_limit` (409, over 100 lists), `distribution_list_not_found` (404), `client_not_found` (404,
  existing).
- No admin route reads another user's lists.

### C. Site (admins only for now, Q6)
- **New page** `frontend/js/pages/distributionLists.js`, route `/DistributionLists` in `js/app.js`
  with `admin: true` (Q6) and `nav: "MyClients"`. Lists the caller's lists (name, count), an empty
  state, and "New list". Tapping a list opens an `openSheet` form: name, description, and a member
  picker — the caller's clients from `/api/clients` with a text filter and a checkbox per client
  (≥44px rows), "Select all shown" / "Clear". Delete through `confirmSheet`.
- **Entry point:** on `/MyClients`, a "Distribution lists" button in the page header, shown only when
  `store.me.role === "admin"`. No new bottom-nav item (the nav stays at 3/4 items from plan 002).
- **Client sheet on `/MyClients`:** a client with `sourceUserId` shows a small muted line
  "Registered user" so the admin can tell auto-added clients from hand-typed ones.
- **i18n** (`en` + `he`): `distributionLists`, `distributionList`, `newDistributionList`,
  `editDistributionList`, `deleteDistributionList`, `deleteDistributionListConfirm`,
  `noDistributionLists`, `listName`, `listDescription`, `listMembers`, `selectAllShown`, `clearSelection`,
  `clientsCount`, `distributionListSaved`, `distributionListDeleted`, `registeredUser`,
  `marketingConsent`
  ("I agree to receive updates and offers" / "אני מסכים/ה לקבל עדכונים והצעות"), and the error texts for
  `errorText`: `list_name_required`, `list_description_too_long`, `too_many_list_clients`,
  `list_limit`, `distribution_list_not_found`. Reuse `save`/`saveClient`, `cancel`, `filterClients`.
- **Icon:** the existing `list` path in `PATHS` (`js/ui.js`) is reused; no new icon.
- **Privacy policy (AC15):** `frontend/js/content/privacy.js` (he + en) lists the new data
  (distribution lists, marketing consent, the copy in the admins' client lists) and who sees it;
  `version` 2, `updated` 2026-11-18.
- **CSS:** only if `.card`/`.stack`/`.row` are not enough; logical properties, mobile-first.

### D. Terms (Q8)
The terms must say that a renter's profile details are added to the business's client list.
New clause **8.7** in `Seed/terms.he.md` / `terms.en.md`, as part of **version 1** (Q8): users have
not been given access to the site yet, so no new version is published (same as plan 002 Q6).
`TermsSeeder` only seeds when the `terms` collection is empty, and the production database already
holds version 1 without 8.7. So after the deploy, **with the owner's OK**, the production `terms`
collection is emptied and the API restarted, and the seeder writes version 1 again with 8.6 and 8.7
(Step 11). The same is done once on the local dev database. Users who already accepted version 1
(admins only) stay accepted.

Clause text (approved together with the plan):

**Hebrew:**
> **8.7. פרטי השוכר ברשימת הלקוחות של העסק.** עם השלמת ההרשמה, שם השוכר, מספר הטלפון וכתובת הדוא"ל שלו נשמרים גם ברשימת הלקוחות של מנהלי המערכת, לצורך ניהול הקשר עם השוכר ומשלוח עדכונים בעתיד, ומתעדכנים כאשר השוכר מעדכן את פרטיו. הודעות שיווקיות יישלחו רק לשוכר שנתן לכך הסכמה מפורשת, והשוכר רשאי לבטל את הסכמתו בכל עת. עם מחיקת חשבון השוכר יימחקו פרטים אלה גם מרשימות הלקוחות של מנהלי המערכת.

**English:**
> **8.7. The Renter's details in the Business's client list.** When the Renter completes registration, their name, phone number and email address are also saved in the client list of the system administrators, to manage the relationship with the Renter and to send updates in the future, and are updated when the Renter updates their details. Marketing messages will be sent only to a Renter who has given explicit consent, and the Renter may withdraw consent at any time. When the Renter's account is deleted, these details are also deleted from the administrators' client lists.

### E. Tests, E2E, docs
- **Tests:** `backend/HourOffice.Tests/AdminClientSyncTests.cs` and
  `DistributionListTests.cs` (collection `"api"`). See Validation.
- **E2E** (`e2e/smoke.mjs`): after the regular user completes the profile, the admin opens
  `/MyClients` and sees that user marked "Registered user"; the admin creates a distribution list with
  that client, edits it and deletes it. The regular user does not see the "Distribution lists" button,
  and `/DistributionLists` redirects them like other admin pages.
- **Docs:** `.doc/glossary.md` — new term `distribution list` (`DistributionList`, `distributionLists`;
  "a named group of the owner's own clients, for future announcements"; not mailing list / group /
  audience / segment) and update `client` (may be auto-added from a user, `sourceUserId`);
  `.doc/product-definition.md` (scope + new AC); `.doc/architecture.md` (collection, routes, index).

Out of scope:
- **Sending anything** (WhatsApp, SMS, email, push) to a client or a list.
- Showing distribution lists to regular users (later: remove `admin: true` and the role check — no API change).
- Showing the marketing-consent checkbox (it is built but hidden, Q4).
- A shared "business" client list that all admins edit together (Q3).
- Import/export of lists.
- `docs/PLAN.md` is not edited.

## Assumptions
- "Admins" = users with `role == "admin"` and `status == "active"`. An invited admin who has not
  signed in yet gets the users on their first sign-in (Q2).
- An admin who completes their own profile is not added to their own list, but is added to other
  admins' lists like any user.
- A user without a completed profile is never added; they are added to every admin when they
  complete it.
- Few admins (1–3), so one insert per admin inside the request is fine; no background queue.
- Clients created this way are ordinary clients of the admin: they can be edited, deleted, booked,
  and put in lists. Deleting one does not affect the user, and the sync does not bring it back (it only
  updates existing clients).
- Each admin sees only their own client list, exactly like a regular user (plan 002 unchanged, Q3).
- `whatsAppConsent` is `false` on auto-added clients unless the user opted in: registering is not
  consent to marketing (Israeli Communications Law §30A requires explicit opt-in for ads).

## Open Questions
1. **When is a new user added as a client — at first sign-in or when the profile is complete?**
   **Answered (2026-11-18): when the profile is complete** (first `PUT /api/me` with a phone).
2. **Add the users who already exist?**
   **Answered (2026-11-18): no button.** Every new user is added to all admins automatically, and a
   user who **becomes** an admin automatically gets all existing users. A one-time startup pass fills the lists
   of the admins that exist on deploy day. See "A user who becomes an admin" under Scope A.
3. **Several admins — one copy per admin, or one shared list?**
   **Answered (2026-11-18): each admin sees only their own clients, like a regular user** → one copy
   per admin, private to that admin.
4. **Marketing consent on auto-added clients?**
   **Answered (2026-11-18): build the option now but hide it with `display: none`.** A user-side
   consent checkbox on `/Register`, hidden; consent is `false` until it is shown and ticked. See
   "Marketing consent, built but hidden" under Scope A.
5. **When the user changes their name or phone, update the admins' copies?**
   **Answered (2026-11-18, given with Q3): yes, update them.** See "Profile changes are synced" under
   Scope A: name, first phone and first email are overwritten; the admin's extra fields are kept.
6. **How are distribution lists hidden from regular users "at this stage"?**
   **Answered (2026-11-18): not shown to regular users at this stage.** Done in the site: route
   `admin: true` and the button shown to admins only. The API already works for every user, so
   opening it later is a site-only change.
7. **When a user is deleted, delete them from the admins' client lists too?**
   **Answered (2026-11-18): yes.** Matched by `sourceUserId`, also removed from distribution lists.
8. **Terms: new clause 8.7 — new version or version 1?**
   **Answered (2026-11-18): stays version 1.** Users have not been given access to the site yet, so
   they see it as version 1. See Scope D for how production gets the new text.
9. **Name of the feature?**
   **Answered (2026-11-18): distribution list** (`DistributionList`, `/api/distribution-lists`;
   Hebrew UI "רשימות תפוצה"), added to the glossary.

All questions are answered. The plan is ready for approval.

## Steps
1. Get the owner's answers to the Open Questions, update this plan, and get approval (`Status: active`).
   Create branch `feat/distribution-list` from `claude/app-development-plan-imirl7`
   (after `feat/client` is merged, if it is still open).
2. `Models.cs`: `Client.SourceUserId`; new `DistributionList`. `MongoContext.cs`: `DistributionLists`
   collection + the two new indexes.
3. `Models.cs`: `User.MarketingConsent` / `MarketingConsentAt`. `Services/AdminClientSync.cs` + DI
   registration in `Program.cs`; from `PUT /api/me` call `AddUserToAdminsAsync` on the first profile
   completion and `UpdateUserInAdminsAsync` on later changes (name, phone, consent).
   `UpdateProfileRequest` / `UserDto`: `marketingConsent`. Hidden consent checkbox in
   `pages/register.js` + `.is-hidden` in `components.css`; i18n `marketingConsent`.
4. Becoming an admin: call `AddAllUsersToAdminAsync` from `PUT /api/admin/users/{id}` (role → admin)
   and `SignInAsync` (promotion, new admin, invited admin activated); one-time startup fill in `Program.cs`.
   `AdminEndpoints.cs`: delete-user cascade (source clients, their ids in lists, the user's own lists).
5. `Dtos.cs`: `sourceUserId` on `ClientDto`; `DistributionListRequest`, `DistributionListDto`,
   `DistributionListSummaryDto`. New `Endpoints/DistributionListEndpoints.cs`, wired in `Program.cs`.
   `ClientEndpoints.cs`: pull a deleted client from the owner's lists.
6. Tests from Validation.
7. Site: `list` icon; `/DistributionLists` route (`admin: true`); `distributionLists.js`; header
   button + "Registered user" line on `/MyClients`.
8. `i18n.js`: new keys in `en` and `he`.
9. Terms: clause 8.7 in both seed files (text after Q8).
10. `e2e/smoke.mjs` steps; docs (glossary, product definition, architecture).
11. Run Validation, then ask for approval to commit. Local dev DB, with the owner's OK: empty the
    `terms` collection and restart the API so version 1 is re-seeded with 8.7. After the production
    deploy, the same in production, again only with the owner's OK.

## Validation
- `cd backend && dotnet build` with no new warnings; `cd backend && dotnet test` passes, including:
  - `Profile_completion_adds_user_to_each_admin` — 2 active admins → each has one client with
    `sourceUserId`, the user's name, phone (E.164) and email, `whatsAppConsent == false`.
  - `Second_profile_update_adds_nothing` — changing the profile again adds no new client.
  - `Profile_change_updates_admin_copies` — name and phone change on every admin's copy; the admin's
    second phone, `notes` and `language` stay; clients of other users are untouched (Q5).
  - `Deleted_copy_is_not_recreated_by_sync` — the admin deletes the copy, the user updates the
    profile, the admin has no copy.
  - `Marketing_consent_default_false_and_synced` — new user → `marketingConsent == false`, copies
    have `whatsAppConsent == false`; `PUT /api/me` with `marketingConsent: true` sets
    `marketingConsentAt` from `FakeClock` and turns consent on in every copy; `false` clears both (Q4).
  - `Admin_is_not_added_to_own_list` — an admin completing their profile is in the other admin's list only.
  - `Invited_admin_gets_nothing_until_first_sign_in` — an admin with `status: "invited"` gets no
    client; on first sign-in they get every user with a completed profile.
  - `Admin_at_client_limit_is_skipped` — profile update still returns 200.
  - `Promoted_user_gets_all_users` — `PUT /api/admin/users/{id}` role → admin: the new admin has one
    client per user with a completed profile (not themself); users without a phone are skipped; other
    admins' lists are unchanged.
  - `Admin_email_sign_in_gets_all_users` — a user in `Auth:AdminEmails` signing in gets the users.
  - `Becoming_admin_twice_adds_no_duplicates` — demote, promote again → no duplicates.
  - `Startup_fill_runs_once` — with the marker the pass does nothing; without it, it adds the missing
    users, and running it again adds no duplicates.
  - `User_without_profile_is_not_added`.
  - `Deleting_user_removes_them_from_admin_clients_and_lists`.
  - `Distribution_list_crud` — create, list (`clientCount`), get with members, update, delete.
  - `Other_user_cannot_read_update_or_delete_list` — 404 `distribution_list_not_found`, list unchanged.
  - `List_with_foreign_client_rejected` — 404 `client_not_found`, nothing saved.
  - `Duplicate_client_ids_stored_once`.
  - `List_validation_errors` — empty name, long description, >1000 clients, 101st list.
  - `Deleting_client_pulls_it_from_lists`.
  - `Regular_user_can_use_distribution_lists_api` (Q6: API open to all).
  - `Distribution_lists_require_terms_and_auth` — 403 `terms_required`, 401 without token.
- `for f in frontend/js/*.js frontend/js/**/*.js; do node --check "$f"; done` is clean.
- Every new i18n key appears in both `en` and `he`.
- `grep -n "8.7" backend/HourOffice.Api/Seed/terms.he.md backend/HourOffice.Api/Seed/terms.en.md`
  finds the clause in both files; on a fresh database `GET /api/terms/current` returns `version: 1`
  with 8.7 in both languages.
- E2E smoke passes against an empty DB, including the steps under E.
- Manual check at 360px and 1440px, he + en: `/DistributionLists` list and sheet with 50+ clients
  (scrolls inside the sheet, no horizontal scroll), "Registered user" line; a regular
  user sees no distribution-list entry; no console errors.

## Risks
- **Privacy / consent:** renters' details now sit in admins' lists. Mitigated by the terms clause
  (Q8), consent off by default (Q4), deletion with the user (Q7), and nothing being sent.
- **Profile update slowed or broken by the sync** — one insert per admin on the first completion and
  one `UpdateMany` on later changes; errors caught and logged.
- **Admin edits overwritten** — the synced fields (name, first phone, first email, consent) follow
  the user (Q5). Fields the admin added are kept.
- **Admin hitting the 1000-client limit** as the user base grows — skipped with a warning; revisit
  the limit when it gets close.
- **Terms re-seed in production** — emptying the `terms` collection is only safe while no regular
  user has accepted the terms; done once, right after the deploy, with the owner's OK.
- **Startup pass** — runs once per database (marker), so restarts stay fast and deleted copies stay deleted.
- **Scope creep into sending** — out of scope until §0 #4 changes.

## Rollout Order
1. API: model, indexes, sync service, become-admin hooks, one-time startup fill, distribution-list routes,
   cascades (additive; the current site ignores the new fields).
2. Site: page, button, hidden consent checkbox, i18n.
3. Deploy API (its one-time startup pass fills the existing admins' lists), then site.
4. With the owner's OK, re-seed terms version 1 (with 8.7) in production.

## Rollback
Revert the commits (`git revert <sha>`). The `distributionLists` collection and `clients.sourceUserId`
stay in MongoDB, unread (the convention pack ignores extra fields). Removing auto-added clients
(`sourceUserId` set) or the collection deletes admins' data — only with the owner's OK. Clause 8.7
stays in the terms text; remove it in the seed files if the feature is dropped.
