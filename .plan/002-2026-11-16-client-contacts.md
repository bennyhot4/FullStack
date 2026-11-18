# Private client list per user

Status: active
Owner: bennyhot4
Last updated: 2026-11-16

## Goal
Every signed-in user can keep their own list of clients: a name, up to two phone numbers and up to
two emails each, phones stored in international format. A booking can be linked to one of the
user's clients, and the client's name is shown on that booking in "My bookings".
A user's clients are visible **only to that user**. Nobody else can read them, including admins and
other users, through the API or the site.

The terms of use get a new clause, in Hebrew and English, that explains this data: who owns it, who
can see it, and that a client must consent before any future messages.

This plan stores and shows the data only. **It sends no messages** (Q1). In the future the list will
be used to send a client an invitation or a booking reminder (for example on WhatsApp).

A new **"Admin"** item in the bottom nav groups Offices, Users, Analytics and Bookings (Q5).

## Scope
In scope (both sides, no `stack:` marker):

### Clients
- **New entity and collection:** `Client` in `backend/HourOffice.Api/Models.cs`, collection `clients`
  in `Data/MongoContext.cs`. Document shape (camelCase from the convention pack):
  ```jsonc
  { "_id": ObjectId,                            // MongoDB key; the API returns it as "clientId" (Q12)
    "userId": ObjectId,                         // owner, always set from the signed-in user
    "firstName": "", "lastName": "",
    "phones": ["+972501234567", "+14155550100"], // 1–2, international E.164 (Q3, Q4)
    "emails": ["a@b.com"],                     // 0–2, lower-cased (Q4)
    "language": "he|en",                       // for future message templates, default he
    "whatsAppConsent": false,                  // the client agreed to get messages; covers the whole
                                               // client card: all of its phones and emails (Q9)
    "whatsAppConsentAt": ISODate|null,         // set by the server when consent turns on
    "notes": "",                               // max 500 chars
    "createdAt": ISODate, "updatedAt": ISODate }
  ```
- **Index** (created at startup in `EnsureIndexesAsync`): `{ userId: 1, lastName: 1, firstName: 1 }`
  for the list. **No uniqueness** on phones or emails (Q4, Q10): the same phone or email may appear on
  several clients of one user and on clients of different users.
- **API** (new file `Endpoints/ClientEndpoints.cs`, group `/api/clients` with `.RequireUser()`):
  | Method | Path | Notes |
  |---|---|---|
  | GET | `/api/clients` | The signed-in user's clients only, sorted by last + first name. Max 1000 |
  | POST | `/api/clients` | Create. `userId` comes from the signed-in user, never from the body |
  | GET | `/api/clients/{id}` | 404 if it does not exist **or belongs to someone else** |
  | PUT | `/api/clients/{id}` | Same 404 rule. Updates `updatedAt`, sets/clears `whatsAppConsentAt` |
  | DELETE | `/api/clients/{id}` | Same 404 rule. Hard delete. Bookings linked to it keep their `clientId` but show no name |

  Every query filters by `userId == ctx.Me().Id` **inside the MongoDB filter** (not by loading and
  checking afterwards). Someone else's client returns 404 `client_not_found`, not 403, so the API does
  not reveal that the id exists.
  **Terms (Q15):** a user who has not accepted the current terms version gets `403 terms_required`.
  The check is written inline in `POST /api/bookings` today (`BookingEndpoints.cs`); it moves to a new
  shared filter `.RequireTerms()` in `Endpoints/Api.cs` (runs after `.RequireUser()`), used by
  `POST /api/bookings`, the whole `/api/clients` group and `PUT /api/bookings/{id}/client`.
  DTOs `ClientRequest` / `ClientDto` in `Endpoints/Dtos.cs`. Wire `ClientEndpoints.Map(app)` in `Program.cs`.
- **Validation and error codes** (`snake_case`, `{ code, message }`):
  `client_name_required` (400, first name empty), `phone_required` (400, no phone),
  `invalid_phone` (400, existing code), `invalid_email` (400), `invalid_language` (400, `language`
  other than `he`/`en`; missing → `he`), `too_many_phones` (400, more than 2),
  `too_many_emails` (400, more than 2), `notes_too_long` (400), `client_limit` (409, more than 1000
  clients), `client_not_found` (404).
  Empty phone/email fields are dropped before validation. The same number (or email) typed in both
  fields of one client is stored once — no error.
- **Phone format (Q3, Q16):** every number is stored in international E.164 format (`+` country code +
  number). New helper `Services/Phone.cs`:
  - **Israeli numbers use the existing check** (Q16): `AuthEndpoints.NormalizeIsraeliPhone` moves to
    `Services/Phone.cs` unchanged (8 or 9 digits after `972`/leading zeros → `+972…`) and both
    `PUT /api/me` and the client routes call it. A number without `+`/`00`, or one whose country code
    is `972`, goes through it: `050-123-4567`, `0501234567`, `+972 50 123 4567`, `00972501234567` →
    `+972501234567`. `PUT /api/me` behaves exactly as today.
  - **Other countries:** input with `+` or `00` and a country code other than `972`
    (`+1 415 555 0100`, `0044 20 7946 0958`) → `+` and 8–15 digits.
  - Anything else → 400 `invalid_phone`.
- **No admin access:** no `/api/admin/clients` route. Admin user lists and analytics show no clients
  or client counts.
- **Delete user cascade:** `DELETE /api/admin/users/{id}` in `AdminEndpoints.cs` also deletes that
  user's clients (`db.Clients.DeleteManyAsync(c => c.UserId == id, ct)`), next to the bookings delete.

### Client on a booking (Q1, Q2)
- `Booking` in `Models.cs` gets optional `ClientId` (ObjectId|null). The client's **name is not
  copied** into the booking; it is looked up at read time, only for the booking's owner.
- `POST /api/bookings` (`CreateBookingRequest`) accepts optional `clientId`. The server checks the
  client belongs to the signed-in user (else 404 `client_not_found`).
- New `PUT /api/bookings/{id}/client` `{ clientId | null }`: the booking's owner sets, changes or
  removes the client. Someone else's booking → 404 `booking_not_found` (existing code if present).
  Cancelled bookings can still be changed (it is only a label).
- `GET /api/bookings/mine` returns `clientId` and `clientName` (first + last) per booking, using one
  extra query: clients with `_id in [ids]` **and** `userId == me`.
- **Admin and shared routes never return `clientId` or `clientName`:** `GET /api/admin/bookings`,
  `PUT /api/admin/bookings/{id}`, recurring, `GET /api/admin/users/{id}/bookings` use a DTO without
  these fields. `/api/offices/{id}/availability` keeps returning slots only. Admin edit of a booking
  keeps the existing `clientId` untouched. Admin recurring bookings have no client.
- One client per booking.

### Site
- **New page** `frontend/js/pages/myClients.js`, route `/MyClients` in `js/app.js`
  (`nav: "MyClients"`). List with a client-side filter by name/phone/email, an empty state with a short
  "only you can see this list" note, and an "Add client" button. Tapping a client opens an `openSheet`
  form: first name, last name, phone + "Add phone" link for a second phone (`type="tel"`, `dir="ltr"`),
  email + "Add email" link for a second one (`type="email"`), language, consent checkbox, notes.
  Delete goes through `confirmSheet`. List rows show the name and the first phone.
- **Translated API errors (new, Q14):** today the site shows the server's English `message`
  (e.g. the toast in `bookOffice.js`), even in Hebrew; there is no code→text map. Add
  `errorText(err)` to `js/ui.js`: if `i18n.js` has a key equal to `err.code`, return `t(err.code)`,
  else `err.message || t("loadingFailed")`. `i18n.js` exports a `hasKey(key)` helper for this.
  Use it in `myClients.js`, the client picker and the booking toast in `bookOffice.js`; other pages
  keep their current behaviour (out of scope).
- **`/BookOffice`** (`pages/bookOffice.js`): an optional "Client" picker (select from my clients, plus
  "+ New client" that opens the same sheet and selects the new client). Sent as `clientId`.
- **`/MyBookings`** (`pages/myBookings.js`): the client's name on each booking card, and an action to
  set/change/remove the client (same picker in a sheet).
  **Filter by client (Q13):** a "Client" filter above the Upcoming/Past tabs (All clients / one of my
  clients, from `/api/clients`). It filters the already-loaded bookings in the browser, applies to
  both tabs, and the tab counts show the filtered numbers. The choice is kept in the URL
  (`/MyBookings?clientId=...`), so the client sheet on `/MyClients` gets a "Bookings with this
  client" link that opens My bookings already filtered. An unknown or deleted `clientId` in the URL
  falls back to "All clients". Booking cards without a client show only under "All clients".
- **Booking notes footnote** (Q11): the notes label on `/BookOffice` (`notesOptional`) gets an
  asterisk ("Notes (optional) *"), and under the field a small muted line: "* Notes are visible to
  the admin. Pick the client above instead of writing client details here." Only on `/BookOffice`:
  a user cannot edit notes after the booking is created.
- **Bottom nav (Q5):**
  - Everyone: Explore · My bookings · **My clients**.
  - Admins also get **Admin** — 4 items in total (was 6).
  - The new **Admin** item goes to new page `frontend/js/pages/adminMenu.js`, route `/Admin`
    (`nav: "Admin"`, `admin: true`): four large links — Offices (`/AdminOffices`), Users
    (`/AdminUsers`), Analytics (`/AdminAnalytics`), Bookings (`/AdminBookings`). The Admin nav item is
    shown active on `/Admin`, those four pages and `/SiteSettingsPage` (set their `nav` to `"Admin"`).
    Offices, Users, Analytics and Bookings leave the bottom nav. Site settings stays reachable from the
    Offices page header, as today.
  - Old paths keep working (bookmarks).
- **Icons** in `PATHS` in `js/ui.js`: new `client` (clients) and `grid` (Admin). No icon library.
- **CSS** in `frontend/css/components.css` only if `.card`, `.stack`, `.row` are not enough;
  logical properties only, ≥44px rows, mobile-first.
- **i18n** (`frontend/js/i18n.js`, both `en` and `he`): `myClients`, `addClient`, `editClient`,
  `deleteClient`, `deleteClientConfirm`, `noClients`, `filterClients`, `clientLanguage`,
  `whatsAppConsent`, `whatsAppConsentHint`, `clientSaved`, `clientDeleted`, `clientsPrivateNote`,
  `addPhone`, `secondPhone`, `addEmail`, `secondEmail`, `client`, `chooseClient`, `noClient`,
  `newClient`, `changeClient`, `allClients`, `filterByClient`, `bookingsWithClient`, `notesVisibleToAdmin` (the footnote text, starting with "* "), `adminMenu` ("Admin" / "ניהול"),
  `email`, `clientNotes`, `saveClient` (the existing `save` is "Save & Continue", which does not fit),
  and error texts keyed by the API code for `errorText` (Q14): `client_name_required`,
  `phone_required`, `invalid_phone`, `invalid_email`, `invalid_language`, `too_many_phones`,
  `too_many_emails`, `notes_too_long`, `client_limit`, `client_not_found`, `terms_required`.
  Existing keys reused, not duplicated: `firstName`, `lastName`, `phoneNumber`, `cancel`, `offices`,
  `users`, `analytics`, `bookings`, `notesOptional`, `loadingFailed`.

### Terms (Hebrew and English)
- Add clause **8.6** to section 8 ("Confidentiality, Privacy and Client Information") in
  `backend/HourOffice.Api/Seed/terms.he.md` and `terms.en.md`, with the text below.
- The system is not in production yet (Q6), so this becomes part of **version 1** of the terms that
  every user accepts when they first sign in. No new version is published.

### Tests, E2E, docs
- **Tests:** new `backend/HourOffice.Tests/ClientTests.cs`, plus booking-link tests (collection `"api"`).
  See Validation.
- **E2E** (`e2e/smoke.mjs`): a regular user adds a client, books an office with that client, sees the
  name in "My bookings", filters My bookings by that client (and via the "Bookings with this client"
  link), deletes the client. The admin opens the Admin page and each of its four links.
  Update the existing nav checks: admin 6 → **4** items, regular user 2 → **3** items, and the step
  that clicks `/AdminOffices` in the bottom nav goes through `/Admin` instead.
- **Docs:** `.doc/glossary.md` (new term `client`), `.doc/product-definition.md` (scope + AC12–AC14),
  `.doc/architecture.md` (new collection, routes, `bookings.clientId`).

Out of scope:
- **Sending any WhatsApp, SMS or email.** Not even a test message (Q1).
- Several clients on one booking.
- Import/export of clients (CSV, phone contacts).
- Field-level encryption (Q7).
- `docs/PLAN.md` is not edited; this plan is the record (same as plan 001).

### Text for the new terms clause

**Hebrew (`terms.he.md`, after 8.5):**

> **8.6. פרטי הלקוחות של השוכר.** המערכת מאפשרת לשוכר לשמור רשימת לקוחות (שם, מספרי טלפון, כתובות דוא"ל והערות) ולשייך לקוח להזמנה.
>
> - המידע נגיש לשוכר בלבד, כולל שם הלקוח המוצג בהזמנות שלו. העסק, מנהלי המערכת ושוכרים אחרים אינם צופים בו ואינם משתמשים בו, אלא אם הדבר נדרש על פי דין או לצורך תחזוקה טכנית הכרחית ובהיקף המינימלי הנדרש.
> - השוכר הוא בעל המידע והאחראי הבלעדי לו. עליו לוודא שיש לו בסיס חוקי לשמירתו, לשמור רק את המידע הנחוץ, ולעמוד בחוק הגנת הפרטיות ובכללי האתיקה של מקצועו. אין לשמור במערכת מידע רפואי או טיפולי, גם לא בשדות ההערות.
> - בעתיד ייתכן שהמערכת תאפשר לשלוח ללקוחות זימונים ותזכורות לפגישות, למשל בוואטסאפ. הודעות כאלה יישלחו לכל פרטי הקשר השמורים בכרטיס הלקוח. השוכר מתחייב לסמן הסכמה רק ללקוח שנתן לכך את הסכמתו המפורשת, ולבטל את הסימון אם הלקוח חזר בו. לקוח רשאי לבטל את הסכמתו בכל עת.
> - השוכר רשאי לערוך ולמחוק כל לקוח בכל עת.
> - השוכר אינו יכול למחוק את חשבונו בעצמו. מחיקת חשבון מתבצעת על ידי מנהל המערכת בלבד. עם מחיקת החשבון יימחקו לצמיתות גם כל ההזמנות שלו וכל פרטי הלקוחות שלו, ולא ניתן יהיה לשחזרם.

**English (`terms.en.md`, after 8.5):**

> **8.6. The Renter's client details.** The system lets the Renter keep a list of their own clients (name, phone numbers, email addresses and notes) and link a client to a booking.
>
> - This information is accessible to the Renter only, including the client name shown on the Renter's bookings. The Business, system administrators and other renters do not view or use it, except where required by law or for necessary technical maintenance, and then only to the minimum extent required.
> - The Renter owns this information and is solely responsible for it. The Renter must make sure they have a lawful basis to keep it, store only what is needed, and comply with the Protection of Privacy Law and the rules of their professional ethics. Medical or therapy information must not be stored in the system, including in notes fields.
> - In the future the system may allow sending clients invitations and booking reminders, for example via WhatsApp. Such messages will be sent to all contact details saved on the client's card. The Renter undertakes to mark consent only for a client who has given explicit consent, and to clear it if the client withdraws. A client may withdraw consent at any time.
> - The Renter may edit or delete any client at any time.
> - The Renter cannot delete their own account. Accounts are deleted only by a system administrator. When an account is deleted, all of its bookings and all of its client details are permanently deleted as well and cannot be restored.

## Assumptions
- "User" here is any signed-in user (role `user` or `admin`). An admin's own clients are just as
  private as anyone else's.
- The list per user is small (tens to hundreds), so the site loads all of it and filters locally.
- The "clients" a user manages are the people the terms already call "Client" / "מטופל", so the new
  clause sits naturally in section 8.
- There is no self-service account deletion today, and this plan adds none: only an admin deletes
  a user (existing `DELETE /api/admin/users/{id}`).
- Server logs never contain client names, phones or emails (only ids), per the `error-handling` skill.
- The local development database already holds terms version 1 without clause 8.6. To see the new
  text locally, its `terms` collection must be emptied so the seeder writes it again (Step 13).

## Open Questions
1. **§0 decision 4 says "no emails/messages at all". Is it OK to store data meant for future messages?**
   **Answered (2026-11-16): for now only store the data and show it in bookings.** Sending needs its
   own plan and a change to §0 #4.
2. **Link a booking to a client and show the name in "My bookings"?**
   **Answered (2026-11-16): yes**, in this plan, as described under "Client on a booking".
3. **Which phone numbers are accepted?**
   **Answered (2026-11-16): store every number in international format.**
4. **Limit duplicate phones?**
   **Answered (2026-11-16): no restriction.** Up to 2 phones and 2 emails per client.
5. **Bottom nav?**
   **Answered (2026-11-16): a new "Admin" button holding Offices, Users, Analytics and Bookings;
   My clients stays outside, in the nav.**
6. **How do existing databases get the new terms clause?**
   **Answered (2026-11-16): the system is not in production yet**, so the clause goes into the seed
   files (version 1) and users accept it as part of the terms. No new version is published.
7. **Encrypt client fields in the database?**
   **Answered (2026-11-16): no, not for now.**
8. **Tell people that deleting a user also deletes their clients?**
   **Answered (2026-11-16): put it in the terms**, together with the fact that a user cannot delete
   their own account, only an admin can. Added as the last bullet of clause 8.6 (he + en). The admin's
   delete-user confirm text is not changed.
9. **With two phones, where will future messages go?**
   **Answered (2026-11-16): to the whole client card** — all of its phones and emails. One consent
   checkbox per client.
10. **Same details on several clients / users?**
    **Answered (2026-11-16): allowed**, for phones and emails alike.
11. **Show a hint under the booking notes field that notes are visible to the admin?**
    **Answered (2026-11-16): yes, as an asterisk footnote** under the notes field.

12. **Name of the client's id field?**
    **Answered (2026-11-16, revised the same day): `clientId`**, matching the canonical term `client`
    (the glossary forbids "contact" in code). Applied as follows:
    - In MongoDB the client document key stays `_id` (MongoDB requires that name).
    - `ClientDto` returns the client's id as `clientId` (instead of `id`), via
      `[JsonPropertyName("clientId")]`.
    - The link on a booking is C# `Booking.ClientId`, which the existing conventions turn into
      `clientId` in MongoDB and in the API JSON; request bodies use `clientId`. No attributes needed.
13. **Filter "My bookings" by client?** (raised by the owner, 2026-11-16)
    **Answered (2026-11-16): yes** — see the `/MyBookings` item under Site. Done in the browser, since
    `GET /api/bookings/mine` already returns all of the user's bookings; no API change.

14. **How does the site show API errors in Hebrew?** (found in review, 2026-11-16)
    There is no code→text map today; pages show the server's English `message`.
    **Decided (2026-11-16):** add `errorText(err)` + `hasKey` as described under Site, used by the new
    client UI and the booking toast only.
15. **Does `PUT /api/bookings/{id}/client` require accepted terms?**
    **Answered (2026-11-16): yes** — via the shared `.RequireTerms()` filter.
16. **Which check validates Israeli phone numbers?**
    **Answered (2026-11-16): the same check as the profile** (`NormalizeIsraeliPhone`), moved to
    `Services/Phone.cs`.
17. **Invalid `language` on a client?** **Decided (2026-11-16):** missing → `he`; any value other than
    `he`/`en` → 400 `invalid_language`.

All questions are answered. The plan is ready for approval.

## Steps
1. Get approval of this plan (set `Status: active`). Then create branch `feat/client`.
2. `Models.cs`: add `Client`, add `Booking.ClientId`. `MongoContext.cs`: `Clients` collection + index.
3. `Services/Phone.cs`: move `NormalizeIsraeliPhone` here (profile keeps using it) and add the
   international normalizer (Q3, Q16). `Api.cs`: `.RequireTerms()` filter; switch `POST /api/bookings`
   to it (Q15).
4. `Dtos.cs`: `ClientRequest`, `ClientDto`, `clientId` on `CreateBookingRequest`, `clientName` on the
   "mine" booking DTO, admin booking DTO without client fields. New `Endpoints/ClientEndpoints.cs`;
   register in `Program.cs`.
5. `BookingEndpoints.cs`: `clientId` on create, `PUT /api/bookings/{id}/client`, names in `/mine`,
   admin routes on the client-free DTO.
6. `AdminEndpoints.cs`: delete the user's clients on delete-user; admin user-bookings on the client-free DTO.
7. Tests: `ClientTests.cs` and booking-link tests from Validation.
8. Site: icons; `app.js` routes (`/MyClients`, `/Admin`), nav items and `nav` flags; `myClients.js`;
   `adminMenu.js`; picker in `bookOffice.js`; name + change action in `myBookings.js`; notes hint;
   `errorText` in `ui.js` (Q14).
9. `i18n.js`: the new keys in `en` and `he`, and `hasKey`.
10. Terms: clause 8.6 in `Seed/terms.he.md` and `Seed/terms.en.md` (text above).
11. `e2e/smoke.mjs`: client step, admin menu step, nav counts admin 4 / user 3.
12. Docs: glossary (`client`: a person a user serves and keeps in their private client list; not
    customer / contact / patient in code), product definition, architecture.
13. Local dev DB only, with the owner's OK: empty the `terms` collection
    (`mongosh houroffice --eval "db.terms.deleteMany({})"`) and restart the API so version 1 is
    re-seeded with clause 8.6.
14. Run the validation, then ask for approval to commit.

## Validation
- `cd backend && dotnet build` succeeds with no new warnings.
- `cd backend && dotnet test` passes, including:
  - `Create_list_update_delete_own_client` — full CRUD for one user.
  - `Other_user_cannot_read_update_or_delete` — user B gets 404 on GET/PUT/DELETE of user A's client;
    A's client is unchanged afterwards.
  - `List_returns_only_own_clients` — A and B each add clients; each list has only their own.
  - `Admin_cannot_see_other_users_clients` — `AdminAsync()` gets only its own (empty) list;
    `/api/admin/clients` does not exist (404).
  - `UserId_in_body_is_ignored` — POST with another user's id in the body saves it as the caller's.
  - `Same_details_allowed_on_many_clients` — two clients of one user, and a client of another user,
    share a phone and an email; all save.
  - `Same_phone_twice_in_one_client_stored_once` — `050-1234567` and `+972 50 123 4567` → one phone.
  - `Two_phones_and_two_emails` — saves and reads back in order; 3 phones → `too_many_phones`,
    3 emails → `too_many_emails`, no phone → `phone_required`.
  - `Validation_errors` — missing first name, bad phone, bad email, notes > 500 → the right 400 codes.
  - `Phone_is_normalized` — `050-123-4567` → `+972501234567`, `00972501234567` →
    `+972501234567`, `00 44 20 7946 0958` → `+442079460958`, `+1 (415) 555-0100` → `+14155550100`.
  - `Profile_phone_unchanged` — the existing profile phone tests still pass after the move to
    `Services/Phone.cs`.
  - `Invalid_language` — `language: "fr"` → 400 `invalid_language`; missing → saved as `he`.
  - `Set_client_on_booking_requires_terms` — `PUT /api/bookings/{id}/client` → 403 `terms_required`.
  - `Consent_timestamp_set_by_server` — on sets `whatsAppConsentAt` from `FakeClock`; off clears it.
  - `Terms_required` — a user who has not accepted the current version gets 403 `terms_required`.
  - `Unauthenticated_gets_401` — no token → 401 on all client routes.
  - `Deleting_user_deletes_their_clients` — A's clients are gone, B's remain.
  - `Booking_with_own_client_shows_name_in_mine` — create with `clientId`; `/mine` returns `clientName`.
  - `Booking_with_other_users_client_rejected` — 404 `client_not_found`, no booking created.
  - `Set_change_remove_client_on_booking` — `PUT /api/bookings/{id}/client` works for the owner;
    another user gets 404; another user's `clientId` is rejected.
  - `Admin_booking_routes_hide_client` — the JSON from `/api/admin/bookings`,
    `/api/admin/users/{id}/bookings` and `PUT /api/admin/bookings/{id}` has no `clientId` or
    `clientName`; availability returns slots only.
  - `Deleted_client_leaves_booking_without_name` — booking still listed, `clientName` null.
  - `Mine_returns_clientId_per_booking` — so the site can filter by client (Q13).
- `for f in frontend/js/*.js frontend/js/**/*.js; do node --check "$f"; done` is clean.
- Every new i18n key exists in both `en` and `he` (each key appears twice in `i18n.js`).
- `grep -n "8.6" backend/HourOffice.Api/Seed/terms.he.md backend/HourOffice.Api/Seed/terms.en.md`
  finds the clause in both files; on a fresh database `GET /api/terms/current` contains it in both languages.
- E2E smoke (against an empty DB) passes, including the client and admin-menu steps.
- Manual check at 360px and 1440px, Hebrew and English: `/MyClients`, the client picker on
  `/BookOffice` with the notes asterisk footnote, the name in `/MyBookings`, `/Admin` with its four
  links; bottom nav shows 3 items for a user and 4 for an admin with no truncated labels; no
  horizontal scroll; RTL correct; no console errors.

## Risks
- **Data leak between users or to admins** — the main risk. Mitigated by the owner filter inside every
  MongoDB query, 404 for foreign ids, ignoring `userId` in the body, a client-free admin booking DTO,
  and the cross-user and admin tests above.
- **Admins lose one tap** to reach Offices/Users/Analytics/Bookings — accepted (Q5).
- **Personal data in logs** — keep logs id-only; checked in the security pass.
- **Scope creep into sending messages** — out of scope until §0 #4 is changed (Q1).

## Rollout Order
1. API: model, index, `/api/clients`, booking `clientId`, admin DTO, delete-user cascade (additive;
   an old site ignores the new fields).
2. Site: pages, nav, picker, i18n.
3. Seed terms files.
4. Deploy API first, then the site (first production deploy seeds the terms with clause 8.6).

## Rollback
Revert the commits (`git revert <sha>`). The `clients` collection and `bookings.clientId` stay in
MongoDB but nothing reads them (the convention pack ignores extra fields). Drop `clients` only if the
feature is abandoned for good, and only with the owner's OK, because it deletes users' client data.
