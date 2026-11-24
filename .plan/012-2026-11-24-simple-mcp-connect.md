# Connecting an AI assistant in one minute, for non-technical people

Status: active
Owner: bennyhot4
Last updated: 2026-11-24

## Goal
Plan 011 made HourOffice work through AI assistants, but connecting one today needs a long technical address, a
settings screen the person has to find alone, and a consent page full of permission jargon. The first real attempt
(Codex, 2026-11-24) failed because the assistant was pointed at a development address and then tried to open `/mcp`
as a web page.

After this plan a person who is not technical can connect **the assistant they already use** in about a minute:
1. On the site they open **"Connect an AI assistant"**, pick their assistant from a list of logos/names.
2. They see 3–4 short numbered steps for that assistant, a big **Copy** button (or a one-click **Open in …** button
   where the assistant supports it), and nothing else to type.
3. On the consent page they press one big **Allow** (the safe, recommended permissions are preselected; the rest is
   behind "Advanced").
4. The site page notices the new connection by itself and shows **"Connected ✓ — try asking: …"** with example
   sentences in their language.

No security rule from plan 011 is relaxed: the same OAuth flow, scopes, consent, fresh sign-in for sensitive scopes,
kill switch and audit log stay as they are. This plan changes how the steps are presented, not what they check.

## Scope

### Research (2026-11-24): how each assistant adds a remote MCP server
| Assistant | Who can | How | One-click link? |
|---|---|---|---|
| Claude (claude.ai, desktop, mobile) | every plan; Free = 1 custom connector | Customize → Connectors → Add custom connector → paste the address → Add → Connect | Open the connectors screen only (paste still needed) |
| ChatGPT | Plus / Pro / Business (not Free); in "Developer mode" or "Create custom MCP server", the UI changes often | Settings → Apps/Connectors → Advanced → Developer mode → Create → paste the address, OAuth | Open the settings screen only |
| Gemini (consumer app) | unclear, reported US + English only | Settings → Connected apps → custom app → address | No |
| Codex (CLI / desktop) | everyone | `codex mcp add houroffice --url <address>`, then `codex mcp login houroffice` | No (one command to copy) |
| Cursor | everyone | `cursor://anysphere.cursor-deeplink/mcp/install?name=…&config=<base64 {"url": …}>` | **Yes** |
| VS Code (Copilot) | everyone | `vscode:mcp/install?<url-encoded {"name","type":"http","url"}>` | **Yes** |
| Anything else | – | "paste this address where your assistant adds an MCP server / connector" | – |

Sources: claude.com/docs/connectors/custom/add-unlisted, help.openai.com article 12584461, cursor.com/docs/mcp/install-links,
code.visualstudio.com/docs/copilot/customization/mcp-servers. The exact menu names move; the guide texts live in i18n
so they can be fixed without code changes.

### API (`backend/HourOffice.Api`)
- `GET /api/settings/public` gains `mcp: { available: bool, url: string }` (`url` = `Mcp:Resource`; `available` =
  `Mcp:Enabled` and the admin switch). The site needs the address and must hide the feature while it is switched off.
  `PublicSettingsDto` in `Endpoints/Dtos.cs` and `src/types/api.ts` change together.
- `Mcp:KnownClients` gains entries for clients whose redirect address is fixed and published (checked during
  implementation; loopback-only clients like Codex cannot be verified by address and stay "not verified").
- Nothing else: no new collection, no new scope, no change to the OAuth server.

### Site (`frontend/`)
- New page **`/ConnectAssistant`** (`src/pages/connectAssistant.tsx`, signed in, `nav: "MyBookings"`):
  - a grid of assistant cards (Claude, ChatGPT, Gemini, Codex, Cursor, VS Code, Other); choosing one shows its steps;
  - the address in a read-only field with a big **Copy** button (`toast` "Copied");
  - **Open in Cursor / Open in VS Code** deep-link buttons; **Open Claude settings / Open ChatGPT settings** buttons
    that open the assistant's connectors screen in a new tab;
  - for Codex: one copyable command line instead of a file to edit;
  - while the page is open it re-reads `/api/me/connected-apps` every 5 seconds (stops after 10 minutes or when the
    page closes); when a new connection appears it shows **"Connected ✓"** and 3 example requests
    ("Which offices are free tomorrow at 10?", "Book office X on Sunday 9–11", "Cancel my booking on Tuesday");
  - when MCP is switched off: a single "AI assistants are not available right now" message.
- **Consent page `/McpConsent` simplified:**
  - one large **Allow** button for the recommended set (today's `DefaultOn` scopes) and **Deny**;
  - the per-scope checkboxes move under a collapsed **"Advanced: choose permissions"**; `clients:full` and
    `admin:*` appear only there, unchecked, with their warnings (unchanged security behaviour);
  - plain-language summary above the button: "\<assistant\> will be able to see offices and free hours, see your
    bookings, and book or cancel for you.";
  - the "not verified" note is reworded from a warning into an explanation ("Make sure you started this connection
    yourself, from \<redirect host\>").
- **Entry points:** a "Connect an AI assistant" card on the home page (shown until the person has a connection; a
  close button hides it, remembered in localStorage), the existing link under My Bookings and in the Admin menu now
  leads to `/ConnectAssistant`, and `/ConnectedApps` gets a "Connect another assistant" button.
- i18n keys (en + he): `connectAssistant`, `connectAssistantIntro`, `connectPick`, `connectCopy`, `connectCopied`,
  `connectOpenIn`, `connectWaiting`, `connectDone`, `connectTry1`, `connectTry2`, `connectTry3`, `connectUnavailable`,
  `connectOther`, per assistant `connectSteps<Name>` (Claude, ChatGPT, Gemini, Codex, Cursor, VSCode, Other),
  `connectPlanNote<Name>` (e.g. "Needs ChatGPT Plus or higher"), `mcpAllowRecommended`, `mcpAdvanced`,
  `mcpSummary`, `mcpUnverifiedHint`, `connectHomeCard`, `connectHomeDismiss`.
- Assistant names are shown as text (no logos or brand images: no trademark assets in the repo).

### Docs
README "How to connect HourOffice to your assistant" points to the site page instead of listing steps;
`.doc/product-definition.md` acceptance criteria; `.doc/glossary.md` unchanged (uses `connected app`).

### Out of scope
- A shorter custom address (custom domain) — see Q2.
- Listing HourOffice in the Claude / ChatGPT app directories (review process by each vendor, a separate plan).
- Any change to scopes, token lifetimes or the OAuth rules of plan 011.

## Assumptions
- Plan 011 is live in production (since 2026-11-24, revision `houroffice-api-00007`).
- The assistants' menu names keep moving; guide texts are i18n strings, so fixing them is a text-only change.
- Deep-link formats for Cursor and VS Code are checked against the real apps during implementation; if one does not
  work, its button falls back to Copy.
- Polling `/api/me/connected-apps` every 5 s for at most 10 minutes is negligible load (one indexed query).

## Open Questions
All answered 2026-11-24 (all as recommended).
1. **Which assistants get their own guide?** **Answered: Claude, ChatGPT, Gemini, Codex, Cursor, VS Code, and
   "Other"** (generic instructions). Copilot (Microsoft 365) is admin-only for organisations, so it gets no card.
2. **Shorter address with a custom domain?** **Answered: not now** — with Copy and one-click buttons nobody types the
   address. Revisit if a domain is bought for the site anyway.
3. **Consent page: one "Allow" with the recommended permissions and the rest under "Advanced"?** **Answered: yes.**
   Security stays the same (sensitive scopes still unchecked, warned, and need a fresh sign-in).
4. **Home page card "Connect an AI assistant"** until the person has a connection, dismissible? **Answered: yes.**

## Steps
Branch: `feat/simple-mcp-connect`.
1. API: `mcp { available, url }` in public settings + DTO + `src/types/api.ts`; test.
2. Check each assistant's current menus and deep links (Claude, ChatGPT, Cursor, VS Code, Codex CLI) and the
   published redirect addresses for `Mcp:KnownClients`; write the i18n guide texts (en + he).
3. `/ConnectAssistant` page (cards, steps, Copy, deep links, polling, "Connected ✓", unavailable state) + route.
4. Consent page: recommended Allow, Advanced section, summary, reworded unverified note.
5. Entry points: home card, My Bookings / Admin menu links, button on `/ConnectedApps`.
6. e2e smoke: `/ConnectAssistant` renders in he + en at phone size, Copy button present; README + product docs.
7. Manual end-to-end on production with Claude (web), ChatGPT, Codex and Cursor as a non-admin user; time each one.

**Implementation notes (2026-11-24, `feat/simple-mcp-connect`)**
- `/api/settings/public` returns `mcp { available, url }`; `url` is empty while MCP is off.
- "Connected on" in `/api/me/connected-apps` is now the **latest** consent (was the first), so a reconnect of the same
  assistant moves it and `/ConnectAssistant` can tell that it just worked.
- Assistant data (deep links, Codex command) is in `src/components/assistants.ts`; step texts are i18n keys
  `connectSteps<Name>` with one step per line.
- The consent page keeps every existing key; `mcpConsentUnverified`, `mcpConsentChoose` and `mcpAllow` are no longer
  shown but are kept (keys are not removed).
- The privacy policy's localStorage sentence now mentions the hidden home card (no new version: it is a display
  preference, not personal data).
- Admin menu: the 5th link is now "AI assistants" (`/ConnectAssistant`); Connected apps is reached from there.
- Checked in the browser at 375px against a local API: home card, Claude guide, Copy field, live "Connected!" after a
  real consent, one-button consent granting exactly the 4 default scopes, Advanced in Hebrew (RTL). Not checked: the
  Cursor and VS Code install links (apps not installed here) and real Claude / ChatGPT / Codex connections (step 7).

## Validation
- [ ] `GET /api/settings/public` returns `mcp.url` = `Mcp:Resource` and `mcp.available` false when `Mcp:Enabled` is
      false or the admin switch is off (backend test).
- [ ] `npm run typecheck`, `npm run lint`, `npm run build` pass; every new key exists in en and he.
- [ ] `/ConnectAssistant` at 360px in he and en: no horizontal scroll, all buttons ≥ 44px, Copy puts the exact
      address on the clipboard (e2e smoke).
- [ ] With MCP off, `/ConnectAssistant` shows only the unavailable message and the home card is hidden.
- [ ] Polling: a connection created while the page is open shows "Connected ✓" within 10 s; polling stops after
      10 minutes and when leaving the page (manual, network tab).
- [ ] Consent: pressing Allow without opening Advanced grants exactly the `DefaultOn` scopes; `clients:full` and
      `admin:*` are reachable only in Advanced and still trigger `reauth_required` when the sign-in is old (existing
      OAuth tests still pass).
- [ ] Cursor and VS Code buttons open the app with the server pre-filled (manual).
- [ ] Manual, recorded in `.orchestrate/qa-report.md`: a non-admin connects Claude, ChatGPT, Codex and Cursor on
      production, each in about a minute from opening `/ConnectAssistant`.

## Risks
- **Vendor screens change** and the guide becomes wrong. Mitigated by texts in i18n (text-only fix) and a generic
  "Other" guide that always works.
- **ChatGPT needs a paid plan / developer mode**: some people cannot connect at all. The ChatGPT card says so up front
  instead of letting them fail.
- **Simpler consent could make people approve without reading.** Mitigated: the default set is the least-privilege
  one, sensitive scopes are never preselected, and the summary sentence says in plain words what the assistant can do.
- **Deep links** (Cursor, VS Code) have broken in past versions; the Copy button is always shown next to them.

## Rollout Order
1. Merge after review; deploy the API (new public settings field, backward compatible) with the runbook in
   `.doc/operations.md`, then upload the new `dist/`.
2. The owner connects Claude and ChatGPT from `/ConnectAssistant` on production as a regular user.
3. Announce the page to users.

## Rollback
- Site: Cloudflare Pages "Rollback to this deployment" (the old consent page comes back; the OAuth flow is unchanged).
- API: route traffic to the previous revision; the old site ignores the extra `mcp` field.
- Everything stays behind the MCP kill switch from plan 011.
