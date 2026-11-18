# Product Definition

## Purpose
HourOffice lets people book offices and meeting rooms by the hour, and lets the owner manage
offices, users, bookings, branding and terms of use. It is a rebuild of an app originally made
on Base44. The full screen-by-screen spec is `docs/PLAN.md` §1.

## Product Vision
A fast, mobile-first booking site in Hebrew and English that costs nothing to run at small
scale, with every business rule enforced on the server.

## Problem Statement
The original app checked overlaps, prices, opening hours and the booking window only in the
browser, kept the terms hard-coded, and matched users by email. Two people could book the same
hour, and rules could be bypassed by calling the API directly (`docs/PLAN.md` §1.4).

## Value Proposition
- Book an office in a few taps on a phone, with live availability.
- No double bookings — guaranteed by the database, even under concurrent requests.
- The owner controls offices, users, banners, colors/logo and versioned terms without a developer.

## Product Scope
- In scope: Google sign-in, profile completion (phone), versioned terms acceptance, office
  office browsing, hourly booking (08:00–22:00, up to 14 days ahead), my bookings +
  cancel, admin: offices + banners, users (invite, role, edit, delete), bookings (edit, cancel,
  recurring), analytics (week/month), site settings/branding, he/en with RTL, PWA.
  Private client list per user (`/MyClients`), an optional client on a booking, filtering
  My bookings by client, and an Admin menu (`/Admin`) behind one bottom-nav item (plan 002).
  A public privacy policy (`/PrivacyPolicy`, he/en, static text in `frontend/src/content/privacy.ts`)
  linked from the login and terms pages (plan 007).
  Every registered user is copied into each admin's own client list and kept in sync; private
  distribution lists of a user's own clients (API for every user, site for admins only for now);
  a marketing-consent field with a hidden checkbox on `/Register` (plan 008).
- Out of scope: passwords, OTP, sending emails or messages of any kind, online payment,
  migrating data from Base44 (for now).

## Target Users
- Primary users: people who book an office or room by the hour, mostly on a phone.
- Secondary users: the owner/admins who manage offices, users, bookings and content.

## Acceptance Criteria
Each criterion must be provable by a test or a command. (Source: `docs/PLAN.md` §8.)
- AC01 — Quality gates: `dotnet build` and `dotnet test` pass in `backend/`, and every
  `frontend/` passes `npm run typecheck`, `npm run lint` and `npm run build`.
- AC02 — A user who has not accepted the current terms version cannot book, including via a
  direct API call; admins see who accepted and when.
- AC03 — Changing colors/logo in site settings changes the whole app immediately.
- AC04 — Two overlapping confirmed bookings for the same office and hour are impossible, even
  from two concurrent requests.
- AC05 — Booking in the past, more than 14 days ahead, or outside 08:00–22:00 is rejected by the API.
- AC06 — A user without a phone number can reach no screen except Welcome / Register.
- AC07 — A regular user gets 403 on every `/api/admin/*` route.
- AC08 — Deleting a user also deletes their bookings.
- AC09 — A recurring booking skips taken slots and reports created / skipped counts.
- AC10 — At 360px wide: no horizontal scroll, touch targets ≥ 44px, the bottom nav hides no
  content, RTL renders correctly.
- AC11 — Lighthouse mobile: Performance ≥ 90, Accessibility ≥ 90.
- AC12 — A user's clients are readable only by that user: another user or an admin gets 404
  `client_not_found`, and no admin route or admin booking response contains client data.
- AC13 — A booking can link one of the owner's own clients (another user's client → 404); the
  client name shows in My bookings, which can be filtered by client.
- AC14 — Deleting a user also deletes their clients. Client data is stored only: no message is
  sent (§0 #4).
- AC15 — `/PrivacyPolicy` opens signed out and signed in, in Hebrew (RTL) and English (LTR), and lists
  every kind of personal data the app stores; a feature that adds personal data updates it.
- AC16 — When a user completes their profile, every active admin gets exactly one client copy of them
  (`sourceUserId`); a user who becomes an admin gets every registered user; later profile changes
  update the copies; deleting the user deletes the copies. No admin sees another user's clients.
- AC17 — Distribution lists hold only the owner's own clients (another user's client → 404) and are
  readable only by their owner; regular users see no distribution-list entry in the site yet.
