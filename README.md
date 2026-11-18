# HourOffice

**Book an office or a meeting room by the hour, straight from your phone.**

HourOffice is a booking website for a business that rents out offices and meeting rooms by the hour. Renters see what is free and book in a few taps. The owner runs everything (offices, people, bookings, branding and terms of use) from the same site, without a developer.

It works in Hebrew and English and is designed for phones first.

🌐 **Live site:** https://houroffice.pages.dev

---

## Why it was built

HourOffice is a rebuild of an earlier version of the app made with Base44, a no-code tool. That version checked every rule (opening hours, overlapping bookings, how far ahead you may book) only inside the visitor's browser. Two people could book the same hour, and anyone who knew how could skip the rules.

In this version **the server enforces every rule**, and the database itself makes a double booking impossible, even if two people press "Book" at the same moment.

---

## What you can do

### As a renter
- **Sign in with your Google account.** You don't need a password and you won't get any emails from the site.
- **Quick first-time setup:** add your phone number and accept the terms of use. When the terms change, you're asked to accept the new version.
- **Browse offices** with photos, a description and the hourly price. Announcements from the owner appear as banners on the home page.
- **Book an office:** pick a day and the hours you need, and see what is free right away. You can book between 08:00 and 22:00, up to 14 days ahead.
- **My bookings:** see upcoming and past bookings, cancel one, and filter them by client.
- **My clients:** keep a private list of your own clients, with up to two phone numbers and two email addresses each. Attach a client to a booking so you remember who the meeting was for. Only you can see this list. The site owner can't.
- **Public pages:** anyone can read the terms of use and the privacy policy, even without signing in.

### As the site owner (admin)
- **Offices:** add, edit and remove offices and upload photos. Manage the home-page banners.
- **People:** invite someone by entering their email and choosing a role. When they sign in with Google, they get that role automatically. You can also change roles, edit details or remove a user. Removing a user also removes their bookings and clients.
- **Bookings:** see every booking, edit or cancel any of them, and create **recurring bookings** (for example, every Monday for two months). Hours that are already taken are skipped, and the site tells you how many bookings it created and how many it skipped.
- **Statistics:** see how busy the offices are, by week and by month.
- **Branding:** change the logo, colors and site name. The whole site updates immediately.
- **Terms of use:** edit the terms, publish a new version, and see who accepted them and when.
- **Invite-only option:** the site can be set so only people the owner invited can join. The live site works this way today.

### Everywhere
- **Two languages:** Hebrew (right-to-left) and English (left-to-right), one tap to switch.
- **Made for phones:** works from a 360-pixel-wide screen, with a bottom menu and large touch targets.
- **Installable:** you can add it to your phone's home screen like an app.
- **Currency aware:** every office and booking has a currency (Israeli shekel by default), and the symbol matches the language.

---

## Coming next

These features are planned and waiting for development:

- **Late-cancellation warning:** the owner sets a notice period (for example, 24 hours). If a renter cancels with less notice, they see a warning before confirming. The owner sees when each booking was cancelled, and late cancellations get a red mark. *(Approved and scheduled.)*
- **Calendar invitations and reminders:** when a booking is confirmed, the renter and their client get a calendar invitation by email. A reminder goes out before the meeting, and the renter chooses how long before. If a booking moves or is cancelled, the invitations update automatically. *(Planned.)*
- **Contact lists for announcements:** new members are added automatically to the owner's client list, and anyone can group their clients into named distribution lists. This prepares the ground for sending announcements later. *(Draft.)*
- **WhatsApp and SMS reminders:** the client list and the reminder design already leave room for them. *(Future idea.)*
- **Own web address and Israel-only access:** move to a custom domain and allow visitors from Israel only. *(Future idea.)*

---

## How it is built

The project has two independent parts that talk to each other over the internet. Either part can move to a different hosting provider without changing the other.

| Part | What it does | Built with |
|---|---|---|
| **Website** | Everything you see and tap | **React** with **TypeScript**, plain **CSS**, built by **Vite** into static files. Only two runtime libraries (React and React DOM). |
| **Server (API)** | Stores data, checks every rule, handles sign-in | **C#** on **ASP.NET Core 8** (Microsoft's web platform) |
| **Database** | Keeps offices, users, bookings, clients and terms | **MongoDB** |
| **Automated tests** | Prove the rules hold, for example no double bookings and no admin pages for regular users | **C#** (xUnit, 48 tests against a real database) and **JavaScript** (Playwright, which clicks through the site on a phone-sized screen in both languages) |
| **Packaging** | Bundles the server so any cloud can run it | **Docker** |

### Where it runs
Everything runs on the free tiers of well-known cloud providers, so it costs about **$0 a month** at this size:

| Part | Hosted on |
|---|---|
| Website | Cloudflare Pages |
| Server | Google Cloud Run. It sleeps when nobody is using it and wakes on the first visit. |
| Office photos | Cloudflare R2 storage |
| Database | MongoDB Atlas |
| Passwords and keys | Google Secret Manager. They never appear in the code. |
| Sign-in | Google Sign-In |

A monthly budget alert and a limit on the number of server copies keep a mistake from turning into a surprise bill.

### Security and privacy in short
- Nobody has a password on the site, so there are no passwords to leak.
- The server checks every request again, including who is an admin. It doesn't trust what the browser says.
- Your client list is private to you.
- The privacy policy lists every kind of personal data the site stores.

---

## For developers

- Local setup, configuration, and the deployment and rollback runbook: [`.doc/operations.md`](.doc/operations.md)
- Architecture overview: [`.doc/architecture.md`](.doc/architecture.md)
- Product definition and acceptance criteria: [`.doc/product-definition.md`](.doc/product-definition.md)
- The original master plan (Hebrew): [`docs/PLAN.md`](docs/PLAN.md). Later decisions are recorded in [`.plan/`](.plan).
