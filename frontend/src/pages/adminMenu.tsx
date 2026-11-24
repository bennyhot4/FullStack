import { t, type TranslationKey } from "../i18n";
import { Link } from "../router";
import { Icon, PageHeader, type IconName } from "../ui";

// The bottom nav's single "Admin" item opens this page, which links to each admin page.
const LINKS: { href: string; icon: IconName; label: TranslationKey }[] = [
  { href: "/AdminOffices", icon: "building", label: "offices" },
  { href: "/AdminUsers", icon: "users", label: "users" },
  { href: "/AdminAnalytics", icon: "chart", label: "analytics" },
  { href: "/AdminBookings", icon: "list", label: "bookings" },
  { href: "/ConnectAssistant", icon: "sparkle", label: "aiAssistants" },
];

export default function AdminMenu() {
  return (
    <>
      <PageHeader title={t("adminMenu")} />
      <div className="container" style={{ paddingBlock: "16px" }}>
        <nav className="admin-menu" aria-label={t("adminMenu")}>
          {LINKS.map((l) => (
            <Link key={l.href} className="card pad" href={l.href}><Icon name={l.icon} className="lg" /><span className="bold">{t(l.label)}</span></Link>
          ))}
        </nav>
      </div>
    </>
  );
}
