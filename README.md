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
- **Your renters as clients:** when someone finishes signing up, their name, phone and email are added to every admin's client list automatically, and stay up to date when they change their details. A new admin gets all existing renters right away. Removing a user removes them from these lists too.
- **Distribution lists:** group clients into named lists (for example, "Morning renters") to reach them later with announcements. For now this is shown to admins only, and nothing is sent yet.
- **Open or invite-only sign-up:** the site can let anyone with a Google account join, or only people the owner invited. The live site is open to everyone.

### Everywhere
- **Two languages:** Hebrew (right-to-left) and English (left-to-right), one tap to switch.
- **Made for phones:** works from a 360-pixel-wide screen, with a bottom menu and large touch targets.
- **Installable:** you can add it to your phone's home screen like an app.
- **Currency aware:** every office and booking has a currency (Israeli shekel by default), and the symbol matches the language.
- **Works with AI assistants:** connect Claude, ChatGPT or another assistant that supports MCP, and ask it in plain words to find a free office, book it, or list and cancel your bookings. You choose on a consent screen what it may do (for example, your clients' names only, not their phone numbers), and you can disconnect it at any time under *Connected apps*. Admins can also let it manage bookings, offices and branding. The assistant follows exactly the same rules as the site.

---

## Coming next

These features are planned and waiting for development:

- **Late-cancellation warning:** the owner sets a notice period (for example, 24 hours). If a renter cancels with less notice, they see a warning before confirming. The owner sees when each booking was cancelled, and late cancellations get a red mark. *(Approved and scheduled.)*
- **Calendar invitations and reminders:** when a booking is confirmed, the renter and their client get a calendar invitation by email. A reminder goes out before the meeting, and the renter chooses how long before. If a booking moves or is cancelled, the invitations update automatically. *(Planned.)*
- **Sending announcements:** send a message to a distribution list, only to people who agreed to receive marketing messages. The lists and the consent field already exist. *(Future idea.)*
- **Distribution lists for every user:** the server already supports them for all users. Showing them to renters is a later step. *(Future idea.)*
- **WhatsApp and SMS reminders:** the client list and the reminder design already leave room for them. *(Future idea.)*
- **Own web address and Israel-only access:** move to a custom domain and allow visitors from Israel only. *(Future idea.)*

---

## How it is built

The project has two independent parts that talk to each other over the internet. Either part can move to a different hosting provider without changing the other.

| Part | What it does | Built with |
|---|---|---|
| **Website** | Everything you see and tap | **React** with **TypeScript**, plain **CSS**, turned by **Vite** into ready-made files any web host can serve. The only outside libraries in the browser are React and React DOM. |
| **Server (API)** | Stores data, checks every rule, handles sign-in | **C#** on **ASP.NET Core 8** (Microsoft's web platform) |
| **Database** | Keeps offices, users, bookings, clients and terms | **MongoDB** |
| **Automated tests** | Prove the rules hold, for example no double bookings and no admin pages for regular users | **C#** (xUnit, 126 tests against a real database) and **JavaScript** (Playwright, which clicks through the site on a phone-sized screen in both languages) |
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
- Your own client list is private to you. The terms of use say plainly that your sign-up details also go to the admins' client list (clause 8.7).
- Marketing messages will go only to people who explicitly agreed, and they can withdraw at any time.
- Phone numbers, client emails and notes can be stored encrypted in the database, with the key kept in Google's key service, so a stolen copy of the database does not reveal them.
- An AI assistant gets in only after you approve it, only with the permissions you ticked, with short-lived encrypted tokens; every action it takes is logged, and the owner can switch the feature off in seconds.
- The privacy policy lists every kind of personal data the site stores.

---

## For developers

- Local setup, configuration, and the deployment and rollback runbook: [`.doc/operations.md`](.doc/operations.md)
- Architecture overview: [`.doc/architecture.md`](.doc/architecture.md)
- Product definition and acceptance criteria: [`.doc/product-definition.md`](.doc/product-definition.md)
- The original master plan (Hebrew): [`docs/PLAN.md`](docs/PLAN.md). Later decisions are recorded in [`.plan/`](.plan).
