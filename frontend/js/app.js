import { applyDocumentLang, getLang, setLang, t } from "./i18n.js";
import { ensureSession, hasSession, onSignedOut, signOut } from "./api.js";
import { isAdmin, loadMe, loadSettings, store } from "./store.js";
import { clear, errorBox, h, icon, spinner } from "./ui.js";

// path -> page module. Paths match the original app (/BookOffice?id=..., /AdminUsers ...).
const routes = {
  "/login": { load: () => import("./pages/login.js"), public: true, chrome: false },
  "/": { load: () => import("./pages/home.js"), nav: "Home" },
  "/Home": { load: () => import("./pages/home.js"), nav: "Home" },
  "/Welcome": { load: () => import("./pages/welcome.js"), chrome: false },
  "/Register": { load: () => import("./pages/register.js"), nav: false },
  // Readable without signing in (linked from the login page); signed-in users can accept it here.
  "/TermsOfUse": { load: () => import("./pages/terms.js"), nav: false, optionalAuth: true },
  // Public (Google's OAuth consent screen links here), same access rules as /TermsOfUse.
  "/PrivacyPolicy": { load: () => import("./pages/privacy.js"), nav: false, optionalAuth: true },
  "/BookOffice": { load: () => import("./pages/bookOffice.js"), nav: "Home" },
  "/MyBookings": { load: () => import("./pages/myBookings.js"), nav: "MyBookings" },
  "/MyClients": { load: () => import("./pages/myClients.js"), nav: "MyClients" },
  // Plan 008: the API is open to every user, but the site shows distribution lists to admins only for now.
  "/DistributionLists": { load: () => import("./pages/distributionLists.js"), nav: "MyClients", admin: true },
  // The admin pages sit behind one "Admin" nav item; their own paths still work (bookmarks).
  "/Admin": { load: () => import("./pages/adminMenu.js"), nav: "Admin", admin: true },
  "/AdminOffices": { load: () => import("./pages/adminOffices.js"), nav: "Admin", admin: true },
  "/AdminUsers": { load: () => import("./pages/adminUsers.js"), nav: "Admin", admin: true },
  "/AdminAnalytics": { load: () => import("./pages/adminAnalytics.js"), nav: "Admin", admin: true },
  "/AdminBookings": { load: () => import("./pages/adminBookings.js"), nav: "Admin", admin: true },
  "/SiteSettingsPage": { load: () => import("./pages/siteSettings.js"), nav: "Admin", admin: true },
};

const root = document.getElementById("app");
let renderToken = 0;

export function navigate(to, { replace = false } = {}) {
  const url = new URL(to, location.origin);
  if (url.pathname + url.search === location.pathname + location.search && !replace) return render();
  history[replace ? "replaceState" : "pushState"]({}, "", url.pathname + url.search + url.hash);
  render();
}

async function render() {
  const token = ++renderToken;
  const path = location.pathname.replace(/\/+$/, "") || "/";
  const route = routes[path];
  window.scrollTo(0, 0);

  if (!route) return show(notFound(), { nav: false });
  const anonymous = route.public || (route.optionalAuth && !hasSession());
  const effective = anonymous && route.optionalAuth ? { ...route, chrome: false } : route;

  // 1. Signed in?
  if (!anonymous) {
    if (!hasSession() || !(await ensureSession())) {
      const from = location.pathname + location.search;
      return navigate(`/login${from !== "/" ? `?from=${encodeURIComponent(from)}` : ""}`, { replace: true });
    }
    if (!store.me) {
      try { await loadMe(); } catch (e) {
        if (e.status === 401) return navigate("/login", { replace: true });
        return show(errorBox(e, () => render()), { nav: false });
      }
    }
    // 2. Profile (phone) and 3. terms. Only the Welcome splash is exempt (the original app also exempted "/",
    // which let new users browse without ever completing the profile).
    const exempt = path === "/Welcome";
    if (store.me.needsProfile && path !== "/Register" && !exempt) return navigate("/Register", { replace: true });
    if (!store.me.needsProfile && store.me.needsTerms && path !== "/TermsOfUse" && !exempt) return navigate("/TermsOfUse", { replace: true });
  }

  // 4. Admin pages
  if (route.admin && !isAdmin()) {
    return show(h("div", { class: "fullscreen-center" }, h("div", { class: "empty" }, icon("shield"), h("p", null, t("adminAccessRequired")))), route);
  }

  let mod;
  try { mod = await route.load(); } catch (e) { return show(errorBox(e, () => render()), route); }
  if (token !== renderToken) return; // a newer navigation started meanwhile
  const container = h("main", { class: `page ${effective.nav === false || effective.chrome === false ? "no-nav" : ""}` });
  show(container, effective);
  try {
    await mod.default(container, { navigate, params: new URLSearchParams(location.search), rerender: render });
  } catch (e) {
    if (token === renderToken) clear(container, errorBox(e, () => render()));
  }
}

function show(content, route) {
  const chrome = route?.chrome !== false && !route?.public;
  clear(root,
    chrome ? topActions() : null,
    content,
    chrome && route?.nav !== false && route?.nav !== undefined ? bottomNav(route.nav) : null);
}

function topActions() {
  return h("div", { class: "top-actions" },
    h("button", {
      class: "icon-btn", type: "button", title: t("logout"), "aria-label": t("logout"),
      onclick: async () => { await signOut(); store.me = null; navigate("/login"); },
    }, icon("logout", "sm flip-rtl")),
    languageButton());
}

export function languageButton(extraClass = "") {
  const next = getLang() === "he" ? "en" : "he";
  return h("button", {
    class: `icon-btn ${extraClass}`, type: "button", title: next === "he" ? "עברית" : "English",
    "aria-label": `${t("language")}: ${next === "he" ? "עברית" : "English"}`,
    onclick: () => setLang(next),
  }, h("span", { class: "xs bold" }, next === "he" ? "עב" : "EN"));
}

function bottomNav(active) {
  const items = [
    { page: "Home", href: "/", icon: "compass", label: t("explore") },
    { page: "MyBookings", href: "/MyBookings", icon: "calendar", label: t("myBookings") },
    { page: "MyClients", href: "/MyClients", icon: "client", label: t("myClients") },
    ...(isAdmin() ? [{ page: "Admin", href: "/Admin", icon: "grid", label: t("adminMenu") }] : []),
  ];
  return h("nav", { class: "bottom-nav", "aria-label": "Main" },
    h("div", { class: "items" }, items.map((i) =>
      h("a", { href: i.href, "data-link": "", "aria-current": i.page === active ? "page" : null }, icon(i.icon), h("span", null, i.label)))));
}

function notFound() {
  return h("div", { class: "fullscreen-center" }, h("div", { class: "empty" },
    h("p", { style: { fontSize: "3rem", fontWeight: 800, color: "var(--text-faint)" } }, "404"),
    h("h2", null, t("pageNotFound")),
    h("a", { class: "btn", href: "/", "data-link": "" }, t("goHome"))));
}

// Internal links: <a href="/x" data-link> navigates without reloading.
document.addEventListener("click", (e) => {
  const a = e.target.closest("a[data-link]");
  if (!a || e.metaKey || e.ctrlKey || e.shiftKey || a.target === "_blank") return;
  e.preventDefault();
  navigate(a.getAttribute("href"));
});
window.addEventListener("popstate", render);
// Hide images that fail to load (e.g. a deleted upload) instead of showing the broken-image icon.
document.addEventListener("error", (e) => { if (e.target instanceof HTMLImageElement) e.target.classList.add("broken"); }, true);
window.addEventListener("languagechange", render);
onSignedOut(() => { store.me = null; navigate("/login", { replace: true }); });

async function start() {
  applyDocumentLang();
  try {
    await loadSettings();
  } catch (e) {
    clear(root, errorBox(e, start));
    return;
  }
  render();
  if ("serviceWorker" in navigator && location.protocol === "https:") navigator.serviceWorker.register("/sw.js").catch(() => {});
}

start();
