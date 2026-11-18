# Booking invitations and reminders by email

Status: draft
Owner: bennyhot4
Last updated: 2026-11-16
Depends on: `.plan/002-2026-11-16-client-contacts.md` (clients, `bookings.clientId`, consent flag).
This plan is executed only after plan 002 is merged.

## Goal
When a user confirms a booking (`POST /api/bookings` succeeds), the system emails a **calendar
invitation** (`.ics`) for that meeting:
- to the **client** linked to the booking (plan 002), if the client gave consent and has an email;
- to the **user** who booked, as an "add to calendar" copy.

Before the booking starts, the system sends the client (and, if the user wants it, the user) a
**reminder email**. The user sets **how long before** the booking the reminder goes out: a default in
their profile, and an optional override on each booking. Admins set the list of allowed lead times.

When the booking is cancelled or its time changes, the invitations are updated (`.ics` CANCEL / new
SEQUENCE) and pending reminders are cancelled or rescheduled.

WhatsApp/SMS stay out of scope. The design (an outbox collection + a channel field) leaves room
for them later.

## Scope
In scope (both sides, no `stack:` marker):

### §0 decision 4 changes (Q1)
`docs/PLAN.md` §0 #4 says "no emails/messages at all". This plan needs it changed to:
> **Messages:** booking emails only: a calendar invitation when a booking is confirmed, updates
> or cancellations for it, and a reminder before it starts. Still no sign-in emails (no OTP, no
> password reset, no "you were invited" email).

`docs/PLAN.md` §0 row 4 is edited with this text (the only edit to `docs/PLAN.md`), and
`.doc/product-definition.md` "Out of scope" is changed to match.

### Email provider
- New `Services/Email/IEmailSender.cs`:
  `Task<EmailResult> SendAsync(EmailMessage msg, CancellationToken ct)`. `EmailMessage` holds to,
  reply-to, subject, HTML body, text body, and attachments (the `.ics`).
- Implementations, chosen by `Email:Provider`:
  - `Brevo` → `BrevoEmailSender` (HTTPS API, `HttpClient` through `IHttpClientFactory`). Free tier
    300 emails/day, already named in `docs/PLAN.md` §9 (Q2).
  - `File` (Development default) → `FileEmailSender` writes each message as an `.eml` to
    `Email:FilePath` (default `mail-outbox/`, added to `.gitignore`), so you can open invitations
    locally without sending anything.
  - Tests register `FakeEmailSender` (in-memory list) in `TestApp`.
- New options class `EmailOptions` in `Options.cs`, section `Email`:
  `Provider`, `FromAddress`, `FromName`, `ApiKey` (**env var only**: `Email__ApiKey`),
  `FilePath`, `UnsubscribeSigningKey` (**env var only**), `SiteBaseUrl` (for links in the email).
  Every key is added to the README config table.
- New options class `NotificationOptions`, section `Notifications`: `Enabled` (kill switch, default
  `true`), `PollSeconds` (default 60), `MaxAttempts` (3), `CronKey` (**env var only**, see Dispatcher).

### Data
- **New collection `notifications`** (an outbox: one document per email to send). Entity
  `Notification` in `Models.cs`:
  ```jsonc
  { "_id": ObjectId,
    "bookingId": ObjectId, "userId": ObjectId,     // booking owner
    "clientId": ObjectId|null,                     // null = sent to the user (booker) themselves
    "recipient": "client|booker",
    "kind": "invitation|invitation_cancel|reminder",
    "channel": "email",                             // room for "whatsapp" later
    "sequence": 0,                                  // .ics SEQUENCE at the time it was enqueued
    "sendAt": ISODate,                              // invitation: now; reminder: start - lead
    "status": "pending|sending|sent|failed|cancelled|skipped",
    "attempts": 0, "leaseUntil": ISODate|null,
    "lastErrorCode": "provider_error|no_recipient|consent_withdrawn|booking_started|...",
    "sentAt": ISODate|null, "createdAt": ISODate }
  ```
  The recipient **email addresses are not stored** in the outbox. They are read from the client /
  user when the email is sent, so editing or deleting a client and withdrawing consent apply right away.
- **Indexes** (`EnsureIndexesAsync`): `{ status: 1, sendAt: 1 }` (dispatcher),
  `{ bookingId: 1 }` (cancel/reschedule), and a unique
  `{ bookingId: 1, recipient: 1, clientId: 1, kind: 1, sequence: 1 }` so a retry or double click can
  never enqueue the same email twice.
- **`Booking`** gets: `ReminderLeadMinutes` (int?, null = no reminder, copied at create time),
  `IcsSequence` (int, starts at 0, +1 on each time change or cancel), `Language` ("he|en", the
  booker's UI language when booking, for the booker's emails).
- **`User`** gets: `ReminderLeadMinutes` (int?, the user's default, null = off) and
  `RemindMe` (bool, default `true`: the booker also receives the reminder). An invitation copy is
  always sent to the booker (Q4).
- **`Client`** (plan 002): field `whatsAppConsent` / `whatsAppConsentAt` is renamed to
  `messageConsent` / `messageConsentAt` (covers email now, WhatsApp later). Plan 002 is not
  implemented yet, so it is updated before it is built (Q5).
- **Site settings** (`siteSettings`, edited on `/SiteSettingsPage`):
  `reminders.leadOptions` (allowed minutes, default `[60, 120, 180, 1440, 2880]` = 1h, 2h, 3h, 1 day,
  2 days) and `reminders.defaultLeadMinutes` (default `1440`), used for new users. Both are returned
  by `GET /api/settings/public`.

### API
| Method | Path | Change |
|---|---|---|
| POST | `/api/bookings` | Accepts optional `reminderLeadMinutes` (must be `null` or in `leadOptions`, else 400 `invalid_reminder_lead`; missing → the user's default) and `language`. In the same request: enqueue the invitations (`sendAt = now`) and the reminder(s), then wake up the dispatcher. **The booking never fails because of email**: sending happens after the response. |
| PUT | `/api/bookings/{id}/reminder` | New. Owner only (else 404 `booking_not_found`). `{ reminderLeadMinutes \| null }`. Cancels pending reminders and enqueues a new one. Only for future, confirmed bookings (else 409 `booking_not_editable`). |
| PUT | `/api/bookings/{id}/client` | (plan 002) Changing the client: enqueue `invitation_cancel` to the old client if they were sent an invitation, an invitation to the new one, and move the pending reminder. |
| POST | `/api/bookings/{id}/cancel` | Cancel pending notifications of the booking; `IcsSequence++`; enqueue `invitation_cancel` to everyone who was already sent an invitation. |
| PUT | `/api/admin/bookings/{id}` | If the date/time changed: `IcsSequence++`, enqueue updated invitations (same UID, new SEQUENCE) to those who already got one, and recalculate `sendAt` of pending reminders. If the status changed to cancelled: same as cancel. |
| POST | `/api/admin/bookings/{id}/cancel`, `DELETE /api/admin/offices/{id}` | Same as cancel, for each booking cancelled. |
| DELETE | `/api/admin/users/{id}` | Also deletes that user's `notifications` (cascade next to bookings and clients). |
| GET/PUT | `/api/me` | Returns and accepts `reminderLeadMinutes` (validated against `leadOptions`) and `remindMe`. |
| GET | `/api/bookings/mine` | Per booking: `reminderLeadMinutes` and `notifications: [{ recipient, kind, status, sendAt }]` (owner only, no addresses). |
| POST | `/api/public/unsubscribe` | New, no auth, `"auth"` rate-limit policy. `{ token }` → sets the client's `messageConsent = false` and cancels their pending notifications. Token = HMAC-SHA256 of `clientId` with `Email:UnsubscribeSigningKey`; a bad token → 400 `invalid_token`. Always returns the same body (does not reveal whether the client exists). |
| POST | `/api/internal/notifications/run` | New. Header `X-Cron-Key` must equal `Notifications:CronKey` (else 404). Runs one dispatcher pass. For hosts that scale to zero (Cloud Run), where the background loop does not run when there is no traffic (Q6). Disabled when `CronKey` is empty. |

Admin routes never return notification data or client data (plan 002 rule stays).

### Dispatcher
- `Services/Notifications/NotificationScheduler.cs`: the only place that enqueues, cancels and
  reschedules (called from the endpoints above). It computes `sendAt = start (Asia/Jerusalem →
  UTC) - lead`. If `sendAt` is already in the past at booking time, **no reminder is created**
  (the invitation was just sent) (Q7).
- `Services/Notifications/NotificationDispatcher.cs`: `RunOnceAsync(ct)`. It claims due work one
  item at a time with `FindOneAndUpdate` (`status: pending, sendAt <= now` → `sending`,
  `leaseUntil = now + 2 min`; expired `sending` leases are picked up again), so two API instances
  never send the same email. Then:
  - loads booking, office, user, client; checks again: booking still confirmed, start not passed
    (reminder) → else `skipped` with a code; client still has `messageConsent` and at least one email
    → else `skipped` (`consent_withdrawn` / `no_recipient`);
  - builds the email (`EmailTemplates`) and `.ics` (`IcsBuilder`), sends, sets `sent` / retries with
    backoff 1 min, 5 min, 30 min, and `failed` after `MaxAttempts`.
- `NotificationWorker : BackgroundService` calls `RunOnceAsync` every `PollSeconds`, and right away
  when an endpoint signals it (a `Channel`/`SemaphoreSlim` "wake-up"). Time comes from `TimeProvider`.
  Not registered in `Testing`; tests call `RunOnceAsync` directly.

### Email content
- **`.ics` (`Services/Notifications/IcsBuilder.cs`, RFC 5545, no library):**
  `UID: booking-{bookingId}@houroffice`, `SEQUENCE: IcsSequence`, `DTSTART`/`DTEND` in UTC (`...Z`),
  `SUMMARY`, `LOCATION` = office name + `Office.Location`, `DESCRIPTION` = short text.
  - To the client: `METHOD:REQUEST`, `ORGANIZER;CN=<user name>:mailto:<user email>`,
    `ATTENDEE:mailto:<client email>`, so the client's calendar shows Accept/Decline and replies go to
    the user. Cancel: `METHOD:CANCEL` with the same UID and a higher SEQUENCE.
  - To the booker: `METHOD:PUBLISH` (a plain "add to calendar" event, because calendars ignore a
    REQUEST where you are the organizer). Cancel: `METHOD:CANCEL`.
- **Body (HTML + plain text, built with escaping, in the recipient's language: client → `Client.language`,
  booker → `Booking.language`):** subject "Meeting invitation: {date} {start}" / "תזכורת: פגישה מחר
  ב-{start}", date, time, office name and location, the user's name and phone (to the client).
  **Never the price or the booking notes.** Reply-To = the user's email. Footer to the client:
  "You got this because {user name} added you as a client" + **unsubscribe link**
  (`{SiteBaseUrl}/Unsubscribe?t=...`). RTL for Hebrew (`dir="rtl"`).
- Sender: `Email:FromName` / `Email:FromAddress` (a verified sender domain at the provider).

### Site
- **`/Profile` (existing profile page, or `/Register` edit mode, whichever edits `PUT /api/me` today):**
  a "Reminders" section: select "Send a reminder before a booking" (options from
  `store.settings.reminders.leadOptions`, plus "No reminder") and a checkbox "Send the reminder to me
  as well".
- **`/BookOffice`:** below the client picker (plan 002), a "Reminder" select, pre-set to the user's
  default; and one muted line: "An invitation is emailed to the client and to you when you confirm".
  If the chosen client has no consent or no email, a short muted note says the client will not get
  emails.
- **`/MyBookings`:** on each upcoming booking card, a small status line: "Invitation sent" /
  "Reminder: 1 day before (scheduled)" / "Reminder sent" / "Email failed", and an action to change
  the reminder (sheet with the same select → `PUT /api/bookings/{id}/reminder`).
- **`/MyClients`** (plan 002): the consent checkbox label becomes "The client agreed to receive
  invitations and reminders"; a hint when consent is on but there is no email.
- **New public page** `frontend/js/pages/unsubscribe.js`, route `/Unsubscribe` (`public: true`,
  no chrome): reads `t` from the URL, calls `POST /api/public/unsubscribe`, shows "You will not get more
  emails from {app}" / an error.
- **`/SiteSettingsPage`:** "Reminders" card: the allowed lead times (checkbox list of 30m, 1h, 2h, 3h,
  6h, 12h, 1 day, 2 days) and the default.
- **i18n** (`en` + `he`): `reminders`, `reminderLead`, `noReminder`, `remindMeToo`,
  `minutesBefore`, `hoursBefore`, `daysBefore`, `invitationWillBeSent`, `clientGetsNoEmail`,
  `invitationSent`, `reminderScheduled`, `reminderSent`, `emailFailed`, `changeReminder`,
  `unsubscribeTitle`, `unsubscribeDone`, `allowedReminderLeads`, `defaultReminderLead`,
  `messageConsent`, `messageConsentHint`, and errors `invalid_reminder_lead`, `invalid_token`,
  `booking_not_editable`. **Email text** lives on the server in `Services/Notifications/EmailTemplates.cs`
  (he + en), not in `i18n.js`.
- Icons: reuse `bell` if present in `PATHS`, else add `bell`.

### Terms
Clause 8.6, third bullet (plan 002), is changed from "in the future…" to: the system emails clients
who have consent an invitation and reminders for bookings linked to them; every email has an
unsubscribe link; the Renter marks consent only with the client's explicit consent. Still version 1
if the system is not in production yet (same as plan 002 Q6), else a new terms version (Q8).

### Tests, E2E, docs
- **Tests:** new `backend/HourOffice.Tests/NotificationTests.cs` (collection `"api"`), using
  `FakeEmailSender`, `FakeClock` and `RunOnceAsync`. See Validation.
- **E2E** (`e2e/smoke.mjs`, `Email:Provider=File`): book with a client that has consent and an email;
  the booking card shows "Invitation sent" after a poll; the `/Unsubscribe` page with a valid token shows
  the success text.
- **Docs:** `.doc/glossary.md` (`invitation`, `reminder`, `notification` = the outbox item),
  `.doc/architecture.md` (collection, worker, routes, Email/Notifications config),
  `.doc/product-definition.md` (scope + AC15–AC18), README config table.

Out of scope:
- WhatsApp / SMS (the `channel` field leaves room for them).
- Reading the client's Accept/Decline reply into the system (replies go to the user's own mailbox).
- More than one reminder per booking; custom (free-text) lead times.
- Emails for admin recurring bookings (no client, no invitation), or to admins.
- Sign-in / "you were invited" emails (§0 #4 stays for those).
- Bounce/complaint webhooks from the provider.

## Assumptions
- "Clients" are the people in the user's private client list (plan 002), and "confirming a room
  booking" is a successful `POST /api/bookings` from the site.
- Only bookings a user makes for themselves send emails. Admin edits and cancels of such a booking send
  updates and cancellations, because the client already holds the invitation.
- Business time zone is `Asia/Jerusalem` (`BookingRules`); the `.ics` uses UTC so every calendar shows
  the right local time.
- Volume is low (tens of emails a day), well inside the Brevo free tier.
- A sender domain will be verified at the provider (SPF/DKIM) before production; without that, emails
  land in spam.
- Logs contain ids, status and provider HTTP code only, never addresses, names or email text.

## Open Questions
1. **Change §0 decision 4 to allow booking emails (wording under "§0 decision 4 changes")?**
   Recommended: **yes**, with the wording above; sign-in emails stay out.
2. **Email provider?** Recommended: **Brevo** (already in `docs/PLAN.md` §9, 300/day free, HTTPS API,
   no SMTP ports). Alternative: Resend (3,000/month).
3. **Who sets the lead time?** Recommended: **admin sets the allowed list and the default; each user
   picks a default in their profile and can change it per booking.** Alternative: one global value
   set only by the admin.
4. **Who gets the invitation and the reminder?** Recommended: invitation → client (with consent) **and**
   the booker; reminder → client, and the booker if `remindMe` is on (default on). A booking without a
   client still emails the booker.
5. **Rename the plan-002 consent field to `messageConsent`?** Recommended: **yes**, before plan 002
   is built, since it now covers email.
6. **Production host scales to zero (Cloud Run)?** Then the background loop does not run without traffic
   and reminders may be late. Recommended: keep the in-process worker **and** add a Cloud Scheduler job
   every 5 minutes calling `POST /api/internal/notifications/run`. If the host is an always-on
   container (Render/VPS), the scheduler is not needed.
7. **Booking made closer to the start than the lead time** (e.g. booked 30 minutes ahead, reminder
   1 day before)? Recommended: **no reminder**, the invitation that was just sent is enough.
8. **Terms clause 8.6:** still version 1 (not in production), or publish a new version?
   Recommended: **version 1**, same as plan 002 Q6, if production has not started when this ships.
9. **Show the user's phone number in the client's email?** Recommended: **yes**, so the client
   can reach them; the name and email are shown in any case (organizer / Reply-To).

## Steps
1. Get answers and approval (`Status: active`). Update plan 002 for Q5 if approved. Create branch
   `feat/booking-reminder` from the branch where plan 002 was merged.
2. `docs/PLAN.md` §0 row 4 (Q1) and `.doc/product-definition.md` out-of-scope line.
3. `Options.cs`: `EmailOptions`, `NotificationOptions`; `appsettings.json` (no secrets),
   `appsettings.Development.json` (`Provider: File`); `.gitignore` `mail-outbox/`.
4. `Models.cs`: `Notification`; new fields on `Booking`, `User`, `Client`. `MongoContext.cs`:
   `Notifications` collection + 3 indexes. Seed `reminders.*` site settings defaults.
5. `Services/Email/`: `IEmailSender`, `BrevoEmailSender`, `FileEmailSender`; DI by provider in `Program.cs`.
6. `Services/Notifications/`: `IcsBuilder`, `EmailTemplates` (he/en, HTML escaping),
   `NotificationScheduler`, `NotificationDispatcher`, `NotificationWorker`, `UnsubscribeToken`.
7. Endpoints: booking create/cancel/client/reminder, admin booking edit/cancel, office delete,
   user delete cascade, `/api/me`, settings (public + admin), `/api/public/unsubscribe`,
   `/api/internal/notifications/run`. DTOs in `Dtos.cs`.
8. `TestApp.cs`: `FakeEmailSender`, no worker in `Testing`. `NotificationTests.cs` per Validation.
9. Site: profile reminders section, `bookOffice.js`, `myBookings.js`, `myClients.js` label,
   `unsubscribe.js` + route, `siteSettings` card, i18n, icon.
10. Terms seed files: clause 8.6 bullet 3 (he + en).
11. E2E steps; docs (glossary, architecture, product definition AC15–AC18, README config table).
12. Run Validation; open `mail-outbox/*.eml` files in Outlook/Gmail import to see the invitation
    renders in he and en; then ask for approval to commit.

## Validation
- `cd backend && dotnet build` with no new warnings; `cd backend && dotnet test` passes, including:
  - `Booking_enqueues_invitations_and_reminder` — create with a consenting client with 2 emails →
    after `RunOnceAsync`: one client email (both addresses), one booker email, both with `.ics`;
    one pending reminder with `sendAt = start - lead`.
  - `Invitation_ics_is_valid` — `METHOD:REQUEST` for the client, `PUBLISH` for the booker, UID
    `booking-{id}@houroffice`, `SEQUENCE:0`, UTC `DTSTART`/`DTEND` that match the booking
    (Israel time → UTC, including a DST date), CRLF line endings and folding at 75 octets.
  - `Reminder_sent_only_when_due` — `RunOnceAsync` before `sendAt` sends nothing; after moving
    `FakeClock` past `sendAt` it sends once; a second run sends nothing more.
  - `No_consent_no_client_email` — client without consent, or without email → client notification
    `skipped`, booker still gets the invitation.
  - `Consent_withdrawn_before_reminder` — consent turned off after booking → reminder `skipped`
    (`consent_withdrawn`).
  - `Cancel_sends_cancel_and_cancels_reminder` — user cancel and admin cancel: `METHOD:CANCEL`,
    `SEQUENCE:1`, reminder `cancelled`.
  - `Admin_time_change_updates_invitation` — same UID, `SEQUENCE:1`, reminder `sendAt` moved.
  - `Change_client_moves_invitation` — old client gets CANCEL, new client gets REQUEST.
  - `Reminder_lead_validation` — value not in `leadOptions` → 400 `invalid_reminder_lead`
    on booking create, `/reminder` and `/api/me`.
  - `Short_notice_booking_has_no_reminder` — booking whose `start - lead` is in the past (Q7).
  - `Remind_me_off` — booker gets the invitation but no reminder.
  - `Email_failure_does_not_fail_booking` — `FakeEmailSender` throws → `POST /api/bookings` returns
    201; notification retried with backoff, then `failed` after `MaxAttempts`.
  - `Two_dispatchers_send_once` — two `RunOnceAsync` in parallel → each email sent exactly once.
  - `Email_has_no_price_or_notes` — body and `.ics` do not contain the price or the booking notes;
    HTML-special characters in names are escaped.
  - `Unsubscribe` — valid token → consent off, pending client notifications `cancelled`; bad token →
    400 `invalid_token`; unknown client → same 200 body.
  - `Internal_run_requires_cron_key` — no/wrong key → 404; right key → runs.
  - `Other_user_cannot_change_reminder` — 404 `booking_not_found`.
  - `Admin_routes_hide_notifications` — no `notifications`/addresses in admin booking JSON.
  - `Deleting_user_deletes_notifications`.
- `for f in frontend/js/*.js frontend/js/**/*.js; do node --check "$f"; done` is clean; every new
  i18n key appears in both `en` and `he`.
- `git grep -n -i "xkeysib\|api[_-]\?key\s*[:=]\s*\"[^\"]" -- backend frontend` finds no secret;
  `Email:ApiKey`, `Email:UnsubscribeSigningKey`, `Notifications:CronKey` are empty in all
  `appsettings*.json` except dev placeholders.
- E2E smoke (empty DB, `Email:Provider=File`) passes, including the invitation-status and unsubscribe steps.
- Manual: a real Brevo test send (owner's sandbox key, env var) to a Gmail and an Outlook address
  shows Accept/Decline, correct local time, Hebrew RTL body; cancel removes the event.
- Manual at 360px and 1440px, he + en: the reminder select on `/BookOffice`, status line on
  `/MyBookings`, profile section, `/Unsubscribe`, settings card; no horizontal scroll; no console errors.

## Risks
- **Emails to people who did not agree** (legal: Israeli Communications Law §30A and privacy law) —
  sent only with `messageConsent`, checked again at send time, unsubscribe link in every client email,
  no marketing content.
- **Spam folder / sender reputation** — needs SPF/DKIM on a verified domain; plain, short emails;
  no link shorteners.
- **Late or missing reminders** when the host sleeps (Q6) — cron endpoint; status visible to the user.
- **Duplicate emails** on retries or several instances — unique outbox index + leased claim + tests.
- **Free-tier limit** (300/day on Brevo) — volume is low; on `429` the item is retried later, not lost.
- **Personal data in logs or the outbox** — addresses not stored in `notifications`, logs id-only.
- **Organizer address mismatch** (`ORGANIZER` is the user's email while `From` is the system) — some
  clients show "sent on behalf of"; accepted, it makes replies reach the user.

## Rollout Order
1. Plan 002 merged and deployed.
2. API: options, outbox, scheduler/dispatcher, endpoint changes (additive; an old site ignores the new
   fields; with `Notifications:Enabled=false` nothing is sent).
3. Site: profile, booking, my bookings, unsubscribe, settings, i18n.
4. Terms seed text.
5. Production: verify the sender domain at Brevo, set `Email__ApiKey`, `Email__UnsubscribeSigningKey`,
   `Email__FromAddress`, `Email__SiteBaseUrl`, `Notifications__CronKey` in the secret manager, create
   the scheduler job (Q6), deploy the API with `Notifications__Enabled=false`, do one test booking, then
   switch it to `true`. Deploy the site.

## Rollback
- Immediate: set `Notifications__Enabled=false` (stops all sending, keeps the outbox).
- Full: `git revert <sha>`. The `notifications` collection and the new booking/user fields stay in
  MongoDB but nothing reads them (the convention pack ignores extra fields). Drop `notifications` only
  with the owner's OK. Restoring §0 #4 text is part of the revert.
