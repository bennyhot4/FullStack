// Route table and guards. Paths match the original app (/BookOffice?id=..., /AdminUsers ...).
// Guards run in order: signed in -> profile complete -> current terms accepted -> admin.
import { Component, useEffect, useState, useSyncExternalStore, type ComponentType, type ReactNode } from "react";
import { ensureSession, hasSession, signOut } from "./api";
import { getLang, subscribeLang, t } from "./i18n";
import { Link, navigate, reload, useLocation, type Loc } from "./router";
import { isAdmin, loadMe, setMe, store, useStore } from "./store";
import { ErrorBox, Icon, Spinner, type IconName } from "./ui";
import { LanguageButton } from "./ui/language";

export interface PageProps {
  params: URLSearchParams;
}
type Page = ComponentType<PageProps>;
type NavKey = "Home" | "MyBookings" | "MyClients" | "Admin";

interface Route {
  load: () => Promise<{ default: Page }>;
  public?: boolean;
  /** Readable signed out; signed-in users get the normal checks. */
  optionalAuth?: boolean;
  /** false: no top buttons and no bottom nav. */
  chrome?: boolean;
  /** Which bottom-nav item is active; false: no bottom nav. */
  nav?: NavKey | false;
  admin?: boolean;
}

const routes: Record<string, Route> = {
  "/login": { load: () => import("./pages/login"), public: true, chrome: false },
  "/": { load: () => import("./pages/home"), nav: "Home" },
  "/Home": { load: () => import("./pages/home"), nav: "Home" },
  "/Welcome": { load: () => import("./pages/welcome"), chrome: false },
  "/Register": { load: () => import("./pages/register"), nav: false },
  // Readable without signing in (linked from the login page); signed-in users can accept it here.
  "/TermsOfUse": { load: () => import("./pages/terms"), nav: false, optionalAuth: true },
  // Public (Google's OAuth consent screen links here), same access rules as /TermsOfUse.
  "/PrivacyPolicy": { load: () => import("./pages/privacy"), nav: false, optionalAuth: true },
  "/BookOffice": { load: () => import("./pages/bookOffice"), nav: "Home" },
  "/MyBookings": { load: () => import("./pages/myBookings"), nav: "MyBookings" },
  "/MyClients": { load: () => import("./pages/myClients"), nav: "MyClients" },
  // Plan 008: the API is open to every user, but the site shows distribution lists to admins only for now.
  "/DistributionLists": { load: () => import("./pages/distributionLists"), nav: "MyClients", admin: true },
  // The admin pages sit behind one "Admin" nav item; their own paths still work (bookmarks).
  "/Admin": { load: () => import("./pages/adminMenu"), nav: "Admin", admin: true },
  "/AdminOffices": { load: () => import("./pages/adminOffices"), nav: "Admin", admin: true },
  "/AdminUsers": { load: () => import("./pages/adminUsers"), nav: "Admin", admin: true },
  "/AdminAnalytics": { load: () => import("./pages/adminAnalytics"), nav: "Admin", admin: true },
  "/AdminBookings": { load: () => import("./pages/adminBookings"), nav: "Admin", admin: true },
  "/SiteSettingsPage": { load: () => import("./pages/siteSettings"), nav: "Admin", admin: true },
  // Plan 011: AI assistants. The consent page needs a signed-in person (login brings them back with ?request=).
  "/McpConsent": { load: () => import("./pages/mcpConsent"), nav: false },
  "/ConnectedApps": { load: () => import("./pages/connectedApps"), nav: "MyBookings" },
};

type View =
  | { kind: "loading" }
  | { kind: "page"; route: Route; Page: Page; loc: Loc }
  | { kind: "notFound" }
  | { kind: "forbidden"; route: Route }
  | { kind: "error"; error: unknown; route?: Route };

/** Runs the guards for a location. Returns null when it redirected somewhere else. */
async function resolve(loc: Loc): Promise<View | null> {
  const route = routes[loc.path];
  if (!route) return { kind: "notFound" };
  const anonymous = route.public || (route.optionalAuth && !hasSession());
  const effective: Route = anonymous && route.optionalAuth ? { ...route, chrome: false } : route;

  // 1. Signed in?
  if (!anonymous) {
    if (!hasSession() || !(await ensureSession())) {
      const from = loc.path + loc.search;
      navigate(`/login${from !== "/" ? `?from=${encodeURIComponent(from)}` : ""}`, { replace: true });
      return null;
    }
    if (!store.me) {
      try { await loadMe(); } catch (e) {
        if ((e as { status?: number }).status === 401) { navigate("/login", { replace: true }); return null; }
        return { kind: "error", error: e };
      }
    }
    const me = store.me!;
    // 2. Profile (phone) and 3. terms. Only the Welcome splash is exempt (the original app also exempted "/",
    // which let new users browse without ever completing the profile).
    const exempt = loc.path === "/Welcome";
    if (me.needsProfile && loc.path !== "/Register" && !exempt) { navigate("/Register", { replace: true }); return null; }
    if (!me.needsProfile && me.needsTerms && loc.path !== "/TermsOfUse" && !exempt) { navigate("/TermsOfUse", { replace: true }); return null; }
  }

  // 4. Admin pages
  if (route.admin && !isAdmin()) return { kind: "forbidden", route };

  try {
    const mod = await route.load();
    return { kind: "page", route: effective, Page: mod.default, loc };
  } catch (e) {
    return { kind: "error", error: e, route };
  }
}

export function App() {
  const loc = useLocation();
  const lang = useSyncExternalStore(subscribeLang, getLang);
  useStore(); // the bottom nav depends on the signed-in user's role
  const [view, setView] = useState<View>({ kind: "loading" });

  useEffect(() => {
    let current = true;
    window.scrollTo(0, 0);
    resolve(loc).then((v) => { if (current && v) setView(v); });
    return () => { current = false; }; // a newer navigation started meanwhile
  }, [loc]);

  switch (view.kind) {
    case "loading":
      return <Spinner />;
    case "notFound":
      return <Layout route={{ load: routes["/"].load, nav: false }}><NotFound /></Layout>;
    case "forbidden":
      return (
        <Layout route={view.route}>
          <div className="fullscreen-center"><div className="empty"><Icon name="shield" /><p>{t("adminAccessRequired")}</p></div></div>
        </Layout>
      );
    case "error":
      return <Layout route={view.route ?? { load: routes["/"].load, nav: false }}><ErrorBox error={view.error} onRetry={reload} /></Layout>;
    case "page": {
      const { route, Page } = view;
      // Key and params come from the location this page was resolved for, not the live one: during a
      // redirect the old page must stay as it is (a remount would re-run its effects).
      return (
        <Layout route={route}>
          <main className={`page ${route.nav === false || route.chrome === false ? "no-nav" : ""}`}>
            {/* A new key on every navigation and language change gives the page a fresh mount, like a visit. */}
            <PageBoundary key={`${view.loc.key}-${lang}`}>
              <Page params={new URLSearchParams(view.loc.search)} />
            </PageBoundary>
          </main>
        </Layout>
      );
    }
  }
}

function Layout({ route, children }: { route: Route; children: ReactNode }) {
  const chrome = route.chrome !== false && !route.public;
  return (
    <>
      {chrome ? <TopActions /> : null}
      {children}
      {chrome && route.nav ? <BottomNav active={route.nav} /> : null}
    </>
  );
}

function TopActions() {
  return (
    <div className="top-actions">
      <button className="icon-btn" type="button" title={t("logout")} aria-label={t("logout")}
        onClick={async () => { await signOut(); setMe(null); navigate("/login"); }}>
        <Icon name="logout" className="sm flip-rtl" />
      </button>
      <LanguageButton />
    </div>
  );
}

function BottomNav({ active }: { active: NavKey }) {
  const items: { page: NavKey; href: string; icon: IconName; label: string }[] = [
    { page: "Home", href: "/", icon: "compass", label: t("explore") },
    { page: "MyBookings", href: "/MyBookings", icon: "calendar", label: t("myBookings") },
    { page: "MyClients", href: "/MyClients", icon: "client", label: t("myClients") },
    ...(isAdmin() ? [{ page: "Admin" as const, href: "/Admin", icon: "grid" as const, label: t("adminMenu") }] : []),
  ];
  return (
    <nav className="bottom-nav" aria-label="Main">
      <div className="items">
        {items.map((i) => (
          <Link key={i.page} href={i.href} aria-current={i.page === active ? "page" : undefined}>
            <Icon name={i.icon} /><span>{i.label}</span>
          </Link>
        ))}
      </div>
    </nav>
  );
}

function NotFound() {
  return (
    <div className="fullscreen-center">
      <div className="empty">
        <p style={{ fontSize: "3rem", fontWeight: 800, color: "var(--text-faint)" }}>404</p>
        <h2>{t("pageNotFound")}</h2>
        <Link className="btn" href="/">{t("goHome")}</Link>
      </div>
    </div>
  );
}

/** Shows an error box (with Retry) instead of a blank screen when a page throws while rendering. */
class PageBoundary extends Component<{ children: ReactNode }, { error: unknown }> {
  state = { error: null as unknown };
  static getDerivedStateFromError(error: unknown) { return { error }; }
  render() {
    return this.state.error ? <ErrorBox error={this.state.error} onRetry={reload} /> : this.props.children;
  }
}
