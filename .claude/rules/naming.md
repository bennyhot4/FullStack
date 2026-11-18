# Naming

Applies to API routes, domain entities, collections, files, and data fields.

- Domain entities are **singular** in code: `User`, `Office`, `Booking`, `TermsDoc`, `SiteSetting`.
- REST resources and MongoDB collections are **plural**, matching the existing API:
  `/api/offices`, `/api/bookings`, `/api/admin/users`; collections `users`, `offices`, `bookings`.
- JSON and MongoDB field names are **camelCase** (`hourlyRate`, `termsAcceptedVersion`) —
  the API's JSON policy and the Mongo convention pack produce this automatically from C# PascalCase.
- Frontend routes keep the original app's paths (`/BookOffice`, `/MyBookings`, `/AdminUsers`,
  `/SiteSettingsPage`); page modules are camelCase files in `frontend/src/pages/` (`bookOffice.tsx`).
- i18n keys keep the original app's names (`bookNow`, `maxTwoWeeks`) — do not rename existing keys.
- Error codes are `snake_case` (`slot_taken`, `terms_required`, `admin_required`).
- Use the canonical term, never a synonym: `office` (not room/space), `booking` (not
  reservation/order), `terms` (not policy/regulations), `admin` (not manager). Canonical terms
  live in `.doc/glossary.md`; document a new shared term there before using it broadly.
