# Late cancellation mark ("less than the approved time")

Status: draft
Owner: bennyhot4
Last updated: 2026-11-17

## Goal
An admin sets a **cancellation notice** in hours (for example 24). A cancellation is **late** when
the booking starts in less than that many hours.

- **User:** when they cancel a booking late, the cancel dialog warns them **before** they confirm:
  "פחות מהזמן המאושר" / "Less than the approved time", plus a sentence that explains it.
  Example: notice 24h, booking today at 21:00, the user cancels at 15:00 → the dialog shows the warning.
  The cancellation is **still allowed** (warning only, Q1).
- **Admin:** every cancelled booking shows the cancel **date and time**. A late cancellation also
  shows a **red X** with the text "פחות מהזמן המאושר".

The server decides whether a cancellation is late and stores the result on the booking when it is
cancelled. A later change to the setting does not change old cancellations.

## Scope
In scope (both sides, no `stack:` marker):

### Setting
- New `SettingKeys.CancelNoticeHours = "cancel_notice_hours"` in the `siteSettings` collection
  (key/value, same as branding). Integer `0..168`. **`0` = feature off** (nothing is ever late).
  Default when the key is missing: **`24`** (Q2), so the feature is on from the first deploy.
- New endpoint `PUT /api/admin/settings/booking-policy` body `{ "cancelNoticeHours": int }`.
  Out of range or missing → `400 invalid_cancel_notice`. Success → `204`.
- `GET /api/settings/public` → `booking.cancelNoticeHours` is added next to `openHour`, `closeHour`, ...

### Data (`bookings`, additive, no index)
- New field `Booking.CancelledLate` (`bool?`, JSON/Mongo `cancelledLate`). `null` on bookings that
  are not cancelled and on old cancelled bookings (they show no mark).

### Booking rules (`Services/BookingRules.cs`)
- `DateTime StartsAtUtc(Booking b)`: `date + startTime` in `Booking:TimeZone` → UTC.
- `bool IsLateCancel(Booking b, DateTime cancelledAtUtc, int noticeHours)`:
  `noticeHours > 0 && StartsAtUtc(b) - cancelledAtUtc < TimeSpan.FromHours(noticeHours)`.
  A booking that has already started counts as late.
- `DateTime? LateCancelFrom(Booking b, int noticeHours)`: `StartsAtUtc(b) - noticeHours`, or `null`
  when `noticeHours == 0`. The browser uses it so it never does time-zone math.

### Endpoints (`Endpoints/BookingEndpoints.cs`)
- `CancelAsync` reads the setting once and sets `CancelledLate` with the other cancel fields
  (in the same `UpdateOne`). Time comes from the injected `TimeProvider`.
  - `POST /api/bookings/{id}/cancel` by the **owner** → computed flag.
  - Cancelled by an **admin** (either route) → `CancelledLate = false` when the admin is not the
    owner (Q3).
- DTOs (`Endpoints/Dtos.cs`):
  - `BookingDto` and `MyBookingDto` get `bool? CancelledLate`.
  - `MyBookingDto` also gets `DateTime? LateCancelFrom` (only for `confirmed` bookings, else `null`).
- A small helper `SettingsReader.CancelNoticeHoursAsync(db, ct)` (in `Services/`) reads the value,
  so the booking and settings endpoints share the parsing and the default.

### Frontend
- `js/pages/myBookings.js` `cancel(b)`: if `b.lateCancelFrom` and now ≥ `lateCancelFrom`, the confirm
  sheet shows a warning block (red X icon + `lessThanApprovedTime` in bold + `lateCancelWarning`)
  above the usual "cannot be undone" text. `confirmSheet` in `js/ui.js` gets an optional `extra`
  node so the warning can be passed in (no `innerHTML`).
- `bookingCard` (shared by `/MyBookings` and the admin user sheet in `/AdminUsers`): "Cancelled on"
  shows date **and time** (`dateStyle: "medium", timeStyle: "short"`). When `b.cancelledLate`, add a
  line with `icon("x")` in `var(--danger)` + `lessThanApprovedTime`.
- `js/pages/adminBookings.js` card: for cancelled bookings add the same "Cancelled on <date time>"
  line and the same red-X late mark (it shows neither today). The mark is one shared helper
  `lateCancelMark()` exported from `myBookings.js` next to `bookingCard`.
- `js/pages/siteSettings.js`: new card "Cancellation policy" (`cancelPolicy`) with a number input
  (`cancelNoticeHours`, min 0, max 168, 16px font) + hint (`cancelNoticeHint`) + Save →
  `PUT /api/admin/settings/booking-policy`, toast on success. Value comes from `store.settings`.
- CSS (`css/components.css`): `.late-mark` (inline-flex, gap, `color: var(--danger-fg)`, logical
  properties only). The X icon is symmetric, no `flip-rtl`.

### i18n keys (en + he)
| key | en | he |
|---|---|---|
| `lessThanApprovedTime` | Less than the approved time | פחות מהזמן המאושר |
| `lateCancelWarning` | This booking starts in less than the cancellation notice set by the site. You can still cancel, and the cancellation will be marked as late. | ההזמנה מתחילה בפחות מזמן ההתראה לביטול שנקבע באתר. אפשר עדיין לבטל, והביטול יסומן כביטול מאוחר. |
| `cancelPolicy` | Cancellation policy | מדיניות ביטול |
| `cancelNoticeHours` | Minimum notice for cancellation (hours) | זמן מינימלי לביטול (שעות) |
| `cancelNoticeHint` | 0 turns the late-cancellation mark off. | 0 מבטל את סימון הביטול המאוחר. |

Error code `invalid_cancel_notice` gets its translated message in the existing error map.

### Docs
- `.doc/glossary.md`: new term **late cancellation** (`cancelledLate`) and **cancellation notice**
  (`cancel_notice_hours`).
- `.doc/product-definition.md`: acceptance criteria below; `.doc/architecture.md`: the new route.
- `.plan/000-backlog.md`: add this task.

Out of scope: blocking late cancellations, fees/penalties, emailing anyone about it, reports.

## Assumptions
- "The time set in the settings" does not exist yet, so this plan adds it as an admin site setting
  (changeable without redeploy), not as an `appsettings` option.
- Notice is measured from the booking **start** time in `Asia/Jerusalem`.
- Exactly at the boundary (start − cancel time == notice) is **not** late.
- Recurring bookings: each booking in the series is judged on its own.

## Open Questions
All answered by the owner on 2026-11-17:
1. **Warn or block?** → **Warn and mark only.** Late cancellations are still allowed.
2. **Default notice** when the admin never set it? → **24 hours.**
3. **Admin cancels a user's booking late** — mark it? → **No.** An admin cancelling their own
   booking is judged like any user.
4. **User sees the red X on their own cancelled booking** in `/MyBookings` → Past? → **Yes.**

## Steps
1. Create branch `feat/late-cancellation` from `claude/app-development-plan-imirl7` (after `feat/client`
   is merged, since both touch `BookingEndpoints.cs`, `Dtos.cs`, `myBookings.js`).
2. Backend: `SettingKeys.CancelNoticeHours`, `SettingsReader`, `BookingRules` helpers,
   `Booking.CancelledLate`, `CancelAsync` change, DTO fields, `PUT /api/admin/settings/booking-policy`,
   `booking.cancelNoticeHours` in public settings.
3. Tests in `backend/HourOffice.Tests` (`LateCancelTests.cs`) — see Validation.
4. Frontend: i18n keys, `confirmSheet` `extra`, cancel warning, `bookingCard` + admin card marks,
   Site settings card, CSS.
5. Docs: glossary, product definition, architecture, backlog.
6. `cd backend && dotnet test`, `node --check` on all `frontend/js/**/*.js`, e2e smoke.

## Validation
Backend (`FakeClock` = Mon 2026-11-09 08:00 Israel):
- [ ] `PUT /api/admin/settings/booking-policy` `{24}` as admin → 204; public settings show
      `booking.cancelNoticeHours == 24`.
- [ ] Same route with `-1`, `169`, missing body → `400 invalid_cancel_notice`; as a non-admin → 403.
- [ ] Notice 24h, booking 2026-11-09 21:00, owner cancels → `cancelledLate == true`, stored in Mongo.
- [ ] Notice 24h, booking 2026-11-11 09:00, owner cancels → `cancelledLate == false`.
- [ ] Boundary: booking exactly 24h after the clock → `false`.
- [ ] Setting never saved → public settings show `cancelNoticeHours == 24`, and an owner cancel 1h
      before start → `cancelledLate == true`.
- [ ] Notice 0 → `false` even 1h before start.
- [ ] Admin cancels another user's booking 1h before start → `false` (per Q3).
- [ ] `GET /api/bookings/mine` returns `lateCancelFrom == start − 24h (UTC)` for confirmed bookings,
      `null` for cancelled ones and when notice is 0.
- [ ] `GET /api/admin/bookings` returns `cancelledLate` and `cancelledAt`.
- [ ] Changing the setting after a cancellation does not change that booking's `cancelledLate`.
- [ ] Old booking documents without the field still deserialize (`cancelledLate == null`).

Frontend (manual / e2e, he + en, 360px):
- [ ] Notice 24h, booking today later → cancel dialog shows red X + "פחות מהזמן המאושר" + explanation;
      booking 3 days ahead → no warning.
- [ ] `/MyBookings` → Past: a late-cancelled booking shows the red X + text; an on-time one does not.
- [ ] `/AdminBookings` cancelled card shows "בוטל ב <date time>" and, if late, red X + text.
- [ ] `/AdminUsers` user bookings sheet shows the same.
- [ ] Site settings card saves and reloads the value; no horizontal scroll at 360px; RTL correct.
- [ ] `node --check` passes for every `frontend/js/**/*.js`.

## Risks
- Time-zone mistakes around DST: start time is converted with `TimeZoneInfo` on the server only;
  tests include a booking on the DST change day if one is within range (else covered by unit test of
  `StartsAtUtc` with a fixed date, e.g. 2026-10-25).
- Browser clock skew: the warning uses the device clock vs. `lateCancelFrom`; the stored flag uses the
  server clock, so they can differ by seconds at the boundary. Acceptable — the server flag wins.
- Merge conflicts with `feat/client` (same files) → start after it merges.

## Rollout Order
1. Backend deploy (new field + route are additive; old frontend ignores them).
2. Frontend deploy.
3. The feature is on with 24 hours from the backend deploy (Q2). The admin can change it in
   `/SiteSettingsPage`. Between steps 1 and 2 the server already stores `cancelledLate`, but the
   old site does not show it yet.

## Rollback
- Set notice to `0` in Site settings → no new late marks, no warnings (instant, no deploy).
- Code rollback: revert the frontend first, then the backend. The `cancelledLate` field and the
  `cancel_notice_hours` setting can stay in the database; the convention pack ignores extra elements.
