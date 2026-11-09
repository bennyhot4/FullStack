// End-to-end smoke test in a real browser at phone size (Hebrew + English).
// Needs the API (Development, DevLoginEnabled) on :5080 and the frontend on :5500.
//   node e2e/smoke.mjs [screenshotDir]
import { createRequire } from "node:module";
// require() so a globally installed Playwright (NODE_PATH) works too.
const { chromium } = createRequire(import.meta.url)("playwright");

const WEB = process.env.WEB_URL || "http://localhost:5500";
const shots = process.argv[2];
const errors = [];
let step = "";

const browser = await chromium.launch();
const phone = { viewport: { width: 390, height: 844 }, deviceScaleFactor: shots ? 2 : 1, isMobile: true, hasTouch: true };

async function newUser(lang) {
  const ctx = await browser.newContext(phone);
  await ctx.addInitScript((l) => localStorage.setItem("appLanguage", l), lang);
  const page = await ctx.newPage();
  page.on("pageerror", (e) => errors.push(`[${step}] pageerror: ${e.message}`));
  page.on("console", (m) => {
    if (m.type() === "error" && !/images\.unsplash|Failed to load resource/.test(m.text())) errors.push(`[${step}] console: ${m.text()}`);
  });
  return page;
}

// The SPA changes the URL with history.pushState/replaceState, so poll location.pathname.
const waitPath = (page, re) => page.waitForFunction((src) => new RegExp(src).test(location.pathname), re.source, { timeout: 15000 });

async function shot(page, name) {
  if (shots) await page.screenshot({ path: `${shots}/${name}.png`, fullPage: false });
}

async function noHorizontalScroll(page, where) {
  for (const width of [360, 390]) {
    await page.setViewportSize({ width, height: 800 });
    const over = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    if (over > 0) errors.push(`[${where}] horizontal overflow ${over}px at ${width}px`);
  }
  await page.setViewportSize(phone.viewport);
}

async function signIn(page, email, name, { first, last, phone: tel }) {
  step = `signin ${email}`;
  await page.goto(`${WEB}/`);
  await waitPath(page, /\/login/);
  await page.fill('input[type="email"]', email);
  await page.fill('input[autocomplete="name"]', name);
  await shot(page, `login-${email.split("@")[0]}`);
  await page.click('form button[type="submit"]');
  await waitPath(page, /\/Register/);
  await page.fill('input[autocomplete="given-name"]', first);
  await page.fill('input[autocomplete="family-name"]', last);
  await page.fill('input[type="tel"]', tel);
  await shot(page, `register-${email.split("@")[0]}`);
  await page.click('form button[type="submit"]');
  await waitPath(page, /\/TermsOfUse/);
  await page.waitForSelector(".prose h2");
  await shot(page, `terms-${email.split("@")[0]}`);
  await page.click(".action-bar button");
  await waitPath(page, /^\/$/);
}

const check = (cond, msg) => { if (!cond) errors.push(`[${step}] ${msg}`); };

// ---------- Admin (Hebrew) ----------
const admin = await newUser("he");
await signIn(admin, "admin@example.com", "Owner Admin", { first: "מנהל", last: "ראשי", phone: "050-7654321" });
step = "admin nav";
check(await admin.locator(".bottom-nav a").count() === 6, "admin should see 6 nav items");
check(await admin.evaluate(() => document.documentElement.dir) === "rtl", "Hebrew should be RTL");

step = "add offices";
await admin.click('.bottom-nav a[href="/AdminOffices"]');
for (const [name, cap, rate, loc] of [["חדר טיפול 1", "2", "60", "תל אביב"], ["חדר קבוצות", "8", "120", "רמת גן"]]) {
  await admin.click("main .btn.block");
  const sheet = admin.locator("dialog.sheet");
  await sheet.locator("input").first().fill(name);
  await sheet.locator('input[type="number"]').nth(0).fill(cap);
  await sheet.locator('input[type="number"]').nth(1).fill(rate);
  await sheet.locator("input:not([type])").nth(1).fill(loc);
  await sheet.locator("input:not([type])").nth(2).fill("WiFi, Coffee, Parking");
  await shot(admin, "admin-office-form");
  await sheet.locator('button[type="submit"]').click();
  await sheet.waitFor({ state: "detached" });
}
await admin.waitForSelector("text=חדר קבוצות");
await shot(admin, "admin-offices");
await noHorizontalScroll(admin, "AdminOffices");

step = "branding";
await admin.click('a[href="/SiteSettingsPage"]');
await admin.waitForSelector('input[type="color"]');
const colorText = admin.locator(".color-field input.input").first();
await colorText.fill("#0E7C66");
await colorText.dispatchEvent("change");
await admin.locator("section").nth(1).locator("input.input").first().fill("חדרי טיפול לפי שעה");
await admin.locator("section").nth(1).locator("button.btn").last().click();
await admin.waitForSelector(".toast.success");
check((await admin.evaluate(() => getComputedStyle(document.documentElement).getPropertyValue("--primary"))).trim().toLowerCase() === "#0e7c66", "branding color applied");
await shot(admin, "admin-settings");
await noHorizontalScroll(admin, "SiteSettings");

// ---------- User (Hebrew): book ----------
const dana = await newUser("he");
await signIn(dana, "dana@example.com", "Dana Levi", { first: "דנה", last: "לוי", phone: "052-1234567" });
step = "home";
await dana.waitForSelector(".office-card");
check(await dana.locator(".office-card").count() === 2, "home shows 2 offices");
check((await dana.textContent("h1")).includes("חדרי טיפול לפי שעה"), "home uses branding title");
await shot(dana, "home");
await noHorizontalScroll(dana, "Home");
await dana.fill('input[type="search"]', "קבוצות");
await dana.waitForFunction(() => document.querySelectorAll(".office-card").length === 1);
await dana.fill('input[type="search"]', "");
await dana.waitForFunction(() => document.querySelectorAll(".office-card").length === 2);
check(await dana.locator(".bottom-nav a").count() === 2, "user sees 2 nav items");

step = "book";
await dana.click(".office-card >> text=חדר טיפול 1 >> xpath=ancestor::article//a");
await dana.waitForSelector(".slot");
const days = dana.locator(".cal-day:not([disabled])");
await days.nth(1).click(); // tomorrow
await dana.waitForSelector(".slot");
// The slot label starts with its own hour; "– 11:00" below it is the end time, so match the start only.
const slot = (page, hour) => page.locator(".slot").filter({ hasText: new RegExp(`^${hour}`) });
await slot(dana, "10:00").click();
await slot(dana, "12:00").click();
await dana.waitForSelector(".action-bar");
check((await dana.textContent(".action-bar")).includes("₪180"), "3 hours x 60 = ₪180");
await shot(dana, "book-office");
await noHorizontalScroll(dana, "BookOffice");
await dana.click(".action-bar button");
await dana.waitForSelector("text=ההזמנה אושרה!");
await shot(dana, "booking-confirmed");
await dana.click('a[href="/MyBookings"]');
await dana.waitForSelector("article.card");
check((await dana.textContent(".tabs")).includes("(1)"), "1 upcoming booking");
await shot(dana, "my-bookings");
await noHorizontalScroll(dana, "MyBookings");

// ---------- Second user (English): sees the booked hours ----------
const noa = await newUser("en");
await signIn(noa, "noa@example.com", "Noa Cohen", { first: "Noa", last: "Cohen", phone: "0541112233" });
step = "booked slots visible";
check(await noa.evaluate(() => document.documentElement.dir) === "ltr", "English should be LTR");
await noa.click(".office-card a.btn >> nth=0");
await noa.waitForSelector(".slot");
// find the office Dana booked
if (!(await noa.textContent("h1")).includes("חדר טיפול 1")) {
  await noa.goBack();
  await noa.click(".office-card a.btn >> nth=1");
}
await noa.locator(".cal-day:not([disabled])").nth(1).click();
await noa.waitForSelector(".slot.booked");
check(await noa.locator(".slot.booked").count() === 3, "3 booked hours shown");
check((await noa.textContent(".price-tag, .card.pad.row")).includes("$"), "English shows $ symbol");
await shot(noa, "booked-slots-en");

// ---------- Admin pages ----------
step = "admin pages";
for (const path of ["/AdminUsers", "/AdminBookings", "/AdminAnalytics"]) {
  await admin.goto(`${WEB}${path}`);
  await admin.waitForSelector("main article, main .stat, main .empty");
  await shot(admin, path.slice(1));
  await noHorizontalScroll(admin, path);
}
await admin.goto(`${WEB}/AdminUsers`);
await admin.waitForSelector("text=dana@example.com");
check((await admin.textContent("main")).includes("נחתם"), "users list shows signed terms");
await admin.goto(`${WEB}/AdminAnalytics`);
await admin.waitForSelector(".stat");
await admin.click(".tab >> nth=1"); // this month
await admin.waitForFunction(() => document.querySelector(".stat .value")?.textContent === "1");

step = "recurring";
await admin.goto(`${WEB}/AdminBookings`);
await admin.click("main .btn.block");
const rs = admin.locator("dialog.sheet");
await rs.locator("select").nth(0).selectOption({ index: 2 });
await rs.locator("select").nth(1).selectOption({ label: "Noa Cohen" });
await rs.locator(".days-picker .chip").nth(0).click();
await rs.locator(".days-picker .chip").nth(3).click();
await shot(admin, "recurring-form");
await rs.locator('button[type="submit"]').click();
await rs.locator("text=✅").waitFor();
const created = await rs.locator(".empty .bold").textContent();
check(/^\d+ /.test(created) && parseInt(created, 10) > 0, `recurring created bookings (${created})`);
await shot(admin, "recurring-result");

step = "regular user blocked from admin";
await dana.goto(`${WEB}/AdminUsers`);
await dana.waitForSelector("text=נדרשת הרשאת מנהל");

step = "logout";
await dana.click('.top-actions button[title]');
await waitPath(dana, /\/login/);

await browser.close();
if (errors.length) {
  console.error(`FAILED (${errors.length})\n` + errors.join("\n"));
  process.exit(1);
}
console.log("E2E smoke: all checks passed");
