// App-wide state: public settings (branding, currency, auth config) and the signed-in user.
import { api, get } from "./api.js";
import { getLang, locale } from "./i18n.js";

export const store = {
  settings: null,
  me: null, // { user, needsProfile, needsTerms, currentTermsVersion }
};

export async function loadSettings() {
  store.settings = await api("/api/settings/public", { auth: false });
  applyBranding(store.settings.branding);
  return store.settings;
}

export async function loadMe() {
  store.me = await get("/api/me");
  return store.me;
}

export const isAdmin = () => store.me?.user?.role === "admin";

/** Applies admin branding colors as CSS variables so the whole app follows them without a rebuild. */
export function applyBranding(b) {
  if (!b) return;
  const root = document.documentElement.style;
  const valid = (c) => /^#[0-9a-f]{6}$/i.test(c || "");
  if (valid(b.buttonsColor)) {
    root.setProperty("--primary", b.buttonsColor);
    root.setProperty("--primary-contrast", contrastText(b.buttonsColor));
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", b.buttonsColor);
  }
  if (valid(b.backgroundColor)) root.setProperty("--background", b.backgroundColor);
  if (valid(b.pagesColor)) root.setProperty("--card", b.pagesColor);
  document.title = b.title || "HourOffice";
}

function contrastText(hex) {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.45 ? "#0f172a" : "#ffffff";
}

// ---------- Formatting ----------

/**
 * Money with the symbol configured per language (current behaviour: same amount, ₪ in Hebrew, $ in English).
 * The currency code is passed in so real multi-currency (conversion rates) can be added later in one place.
 */
export function money(amount, currencyCode) {
  void currencyCode; // reserved for future conversion
  const symbols = store.settings?.currency?.displaySymbols || { he: "₪", en: "$" };
  const symbol = symbols[getLang()] ?? symbols.en ?? "";
  const n = new Intl.NumberFormat(locale(), { maximumFractionDigits: 2 }).format(Number(amount) || 0);
  return `${symbol}${n}`;
}

const parseDate = (d) => new Date(`${d}T12:00:00`);
export const fmtDate = (d, opts = { weekday: "long", month: "short", day: "numeric", year: "numeric" }) =>
  new Intl.DateTimeFormat(locale(), opts).format(typeof d === "string" && d.length === 10 ? parseDate(d) : new Date(d));
export const fmtShortDate = (d) => fmtDate(d, { month: "short", day: "numeric", year: "numeric" });
export const isoDate = (date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
export const hoursLabel = (n, t) => `${n} ${n === 1 ? t("hour") : t("hours")}`;

/** Today's date ("yyyy-MM-dd") in the business time zone, so the booking window matches the server. */
export function todayInBusinessTz() {
  const tz = store.settings?.booking?.timeZone || "Asia/Jerusalem";
  try { return new Intl.DateTimeFormat("en-CA", { timeZone: tz, year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date()); }
  catch { return isoDate(new Date()); }
}
export function addDays(iso, n) {
  const d = new Date(`${iso}T12:00:00`);
  d.setDate(d.getDate() + n);
  return isoDate(d);
}
