// App-wide state: public settings (branding, currency, auth config) and the signed-in user.
// A plain module store with a subscribe function; components read it through useStore().
import { useSyncExternalStore } from "react";
import { api, get } from "./api";
import { getLang, locale, type TranslationKey } from "./i18n";
import type { Branding, Me, PublicSettings } from "./types/api";

interface State {
  settings: PublicSettings | null;
  me: Me | null;
}

export const store: State = { settings: null, me: null };
const listeners = new Set<() => void>();
let snapshot: State = { ...store };

function changed() {
  snapshot = { ...store };
  listeners.forEach((fn) => fn());
}

export function setMe(me: Me | null) {
  store.me = me;
  changed();
}

function subscribeStore(fn: () => void) {
  listeners.add(fn);
  return () => { listeners.delete(fn); };
}

/** Re-renders the component when settings or the signed-in user change. */
export const useStore = () => useSyncExternalStore(subscribeStore, () => snapshot);

/** The public settings. main.tsx loads them before the first render, so they are always there. */
export function settings(): PublicSettings {
  if (!store.settings) throw new Error("settings not loaded");
  return store.settings;
}

/** The signed-in user. Only for pages behind the sign-in guard, which loads it before rendering them. */
export function me() {
  if (!store.me) throw new Error("not signed in");
  return store.me;
}

export async function loadSettings() {
  store.settings = await api<PublicSettings>("/api/settings/public", { auth: false });
  applyBranding(store.settings.branding);
  changed();
  return store.settings;
}

export async function loadMe() {
  store.me = await get<Me>("/api/me");
  changed();
  return store.me;
}

export const isAdmin = () => store.me?.user?.role === "admin";

/** Applies admin branding colors as CSS variables so the whole app follows them without a rebuild. */
export function applyBranding(b: Branding | undefined) {
  if (!b) return;
  const root = document.documentElement.style;
  const valid = (c: string | undefined) => /^#[0-9a-f]{6}$/i.test(c || "");
  if (valid(b.buttonsColor)) {
    root.setProperty("--primary", b.buttonsColor);
    root.setProperty("--primary-contrast", contrastText(b.buttonsColor));
    document.querySelector('meta[name="theme-color"]')?.setAttribute("content", b.buttonsColor);
  }
  if (valid(b.backgroundColor)) root.setProperty("--background", b.backgroundColor);
  if (valid(b.pagesColor)) root.setProperty("--card", b.pagesColor);
  document.title = b.title || "HourOffice";
}

function contrastText(hex: string) {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b > 0.45 ? "#0f172a" : "#ffffff";
}

// ---------- Formatting ----------

/**
 * Money with the symbol configured per language (current behaviour: same amount, ₪ in Hebrew, $ in English).
 * The currency code is passed in so real multi-currency (conversion rates) can be added later in one place.
 */
export function money(amount: number, currencyCode?: string) {
  void currencyCode; // reserved for future conversion
  const symbols = store.settings?.currency?.displaySymbols || { he: "₪", en: "$" };
  const symbol = symbols[getLang()] ?? symbols.en ?? "";
  const n = new Intl.NumberFormat(locale(), { maximumFractionDigits: 2 }).format(Number(amount) || 0);
  return `${symbol}${n}`;
}

const parseDate = (d: string) => new Date(`${d}T12:00:00`);
export const fmtDate = (d: string | Date, opts: Intl.DateTimeFormatOptions = { weekday: "long", month: "short", day: "numeric", year: "numeric" }) =>
  new Intl.DateTimeFormat(locale(), opts).format(typeof d === "string" && d.length === 10 ? parseDate(d) : new Date(d));
export const fmtShortDate = (d: string | Date) => fmtDate(d, { month: "short", day: "numeric", year: "numeric" });
export const isoDate = (date: Date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
export const hoursLabel = (n: number, t: (k: TranslationKey) => string) => `${n} ${n === 1 ? t("hour") : t("hours")}`;

/** Today's date ("yyyy-MM-dd") in the business time zone, so the booking window matches the server. */
export function todayInBusinessTz() {
  const tz = store.settings?.booking?.timeZone || "Asia/Jerusalem";
  try { return new Intl.DateTimeFormat("en-CA", { timeZone: tz, year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date()); }
  catch { return isoDate(new Date()); }
}
export function addDays(iso: string, n: number) {
  const d = new Date(`${iso}T12:00:00`);
  d.setDate(d.getDate() + n);
  return isoDate(d);
}
