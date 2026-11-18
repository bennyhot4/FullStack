# Glossary

## Purpose
- Define canonical domain terms and approved short forms used across code, API routes, docs, and plans.

## Core Terms
| Term | Meaning | Not |
|---|---|---|
| `office` | A bookable space (office or meeting room) with capacity, hourly rate, amenities, location | room, space, workspace |
| `booking` | One office reserved by one user for a contiguous hour range on one date | reservation, order |
| `slot` | One hour on the booking grid, written `HH:00`; the day runs 08:00–22:00 | time block |
| `status` (booking) | `confirmed`, `cancelled`, `completed` | |
| recurring booking | Admin action that creates many bookings over weekdays in a date range, skipping taken slots | repeated/series |
| `user` / `admin` | The two roles | manager, customer |
| invited | A user record an admin created before that person ever signed in (`status: "invited"`) | pending |
| `terms` | The versioned terms of use; users must accept the current version | policy, regulations |
| profile | The required user details (first/last name, phone) completed on `/Register` | account |
| site settings | Admin-managed key/value content: banners, branding title/description/logo, colors | config |
| branding | The three colors + title, description, logo applied to the whole site at runtime | theme |
| `currency` | ISO code stored on each office and booking (default `ILS`); display symbol by language | |
| `client` | A person a user serves, kept in that user's private client list (name, 1–2 phones, 0–2 emails, consent flag); a booking may link one via `clientId`. Visible to its owner only, never to admins. Each admin's list also holds a copy of every registered user, marked with `sourceUserId` and kept in sync with that user's profile | customer, contact, patient |
| distribution list | A named group of the owner's own clients, for future announcements (`DistributionList`, collection `distributionLists`, `/api/distribution-lists`; Hebrew "רשימת תפוצה"). Private to its owner. Nothing is sent yet | mailing list, group, audience, segment |
| marketing consent | A user's own opt-in to receive updates and offers (`users.marketingConsent`); copied to the admins' copies of that user as `whatsAppConsent` | opt-in, subscription |

## Naming Alignment
- Keep this glossary aligned with naming decisions in `../.claude/rules/naming.md`.
- If a new domain term is introduced, add it here before broad usage.
- Avoid synonyms for existing terms unless explicitly approved and documented here.
