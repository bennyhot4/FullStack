# Remove the office search and capacity filter

Status: done
Owner: bennyhot4
Last updated: 2026-11-16

## Goal
Remove the free-text office search and the capacity filter chips from the home page (`/Home`, `/`).
Neither is needed: there are few offices, so the home page simply lists all available offices.
The page gets simpler and shorter on a phone.

## Scope
In scope (both sides, no `stack:` marker):
- **Site:** in `frontend/js/pages/home.js` remove the search field (the `search` input, its debounce
  `timer`/listener, `q.set("search", ...)`) and the capacity chips (`sizes`, `chips`, `state.size`,
  `q.set("size", ...)`). `load()` calls `GET /api/offices` with no query string.
- **Site CSS:** remove the `.search` rules in `frontend/css/components.css`. Nothing else uses that class.
- **i18n:** remove `searchOffices`, `filterByCapacity`, `allSizes`, `smallPeople`, `mediumPeople`,
  `largePeople` from both `en` and `he` in `frontend/js/i18n.js`.
- **API:** remove the `search` and `size` query parameters and their filters from `GET /api/offices` in
  `backend/HourOffice.Api/Endpoints/OfficeEndpoints.cs` (Q1, Q2). The endpoint returns every available
  office, newest first.
- **Tests:** in `backend/HourOffice.Tests/BookingTests.cs` replace `Search_and_capacity_filters` with
  `Office_list_ignores_removed_filters`: `?search=` and `?size=` are ignored and all 3 offices come back.
  In `e2e/smoke.mjs` remove the 4 lines that type into `input[type="search"]`.
- **Docs:** `.doc/product-definition.md` — "browsing/search/filter" → "office browsing". This plan file
  is the record of the change (Q3).

Out of scope:
- `docs/PLAN.md` is **not** edited (Q3).
- The `.chips` / `.chip` CSS stays — the recurring-booking day picker in `adminBookings.js` uses it.
- The `search` icon in `frontend/js/ui.js` stays — the "no offices found" empty state uses it.
- The `people` i18n key stays — office cards use it.
- No MongoDB collection or index changes.

## Assumptions
- Nothing besides `home.js` calls `GET /api/offices` with `search` or `size` (verified with a repo-wide
  grep: only `home.js`, the backend test and the e2e smoke).
- An old site calling the new API with `?search=` / `?size=` is harmless: ASP.NET ignores unknown query
  parameters and returns all offices.

## Open Questions
1. **Remove the `search` parameter from the API too, or only hide the field in the site?**
   **Answered (2026-11-16): remove it from the API too.**
2. **Keep the capacity filter (All / Small 1-4 / Medium 5-8 / Large 9+)?**
   First answered "keep it"; **changed (2026-11-16): remove it too**, from the site and the API.
3. **Update `docs/PLAN.md` so it no longer describes the search?**
   **Answered (2026-11-16): no.** `docs/PLAN.md` stays as it is; this plan file is the record.

## Steps
1. Create branch `chore/remove-office-search`.
2. `OfficeEndpoints.cs`: remove the `search` and `size` parameters and their filters.
3. `BookingTests.cs`: replace the filter test with `Office_list_ignores_removed_filters`.
4. `home.js`: remove the search field and the capacity chips; drop the now-empty `stack` wrapper.
5. `components.css`: remove the three `.search` rules.
6. `i18n.js`: remove the six keys listed above from `en` and `he`.
7. `e2e/smoke.mjs`: remove the four search lines in the `home` step.
8. `.doc/product-definition.md`: update the scope line. Leave `docs/PLAN.md` untouched.
9. Run the validation below, then ask for approval to commit.

## Validation
- `cd backend && dotnet build` succeeds with no new warnings.
- `cd backend && dotnet test` passes, including `Office_list_ignores_removed_filters`.
- `for f in frontend/js/*.js frontend/js/**/*.js; do node --check "$f"; done` is clean.
- `grep -rn "searchOffices\|filterByCapacity\|allSizes\|smallPeople\|mediumPeople\|largePeople\|state.search\|state.size" frontend/js`
  returns nothing.
- `grep -n "search\|size" backend/HourOffice.Api/Endpoints/OfficeEndpoints.cs` shows no query filter.
- E2E smoke (`NODE_PATH=$(npm root -g) node e2e/smoke.mjs` against an empty DB) passes.
- `git diff --stat` shows no change to `docs/PLAN.md`.
- Manual check of `/` at 375px and 1440px, Hebrew and English: no search field and no filter chips,
  all available offices listed, no horizontal scroll, no console errors.
- The admin recurring-booking day picker still shows its day chips styled.

## Risks
- **Someone wants to filter later** — low; restoring it is a revert of this commit.
- **Missed reference** breaks the page — `node --check` catches syntax only, so the manual check and
  the e2e smoke cover runtime errors.
- **Header spacing** without the controls — covered by the manual check at both widths.

## Rollout Order
1. API change (backward compatible: an old site's `?search=` / `?size=` are ignored).
2. Site change.
3. Docs.
All in one branch; deploy the API first if they are ever deployed at different times.

## Rollback
Revert the commit (`git revert <sha>`). No data or index changes, so nothing to restore in MongoDB.
