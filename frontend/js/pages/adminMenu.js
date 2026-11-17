import { t } from "../i18n.js";
import { clear, h, icon, pageHeader } from "../ui.js";

// The bottom nav's single "Admin" item opens this page, which links to each admin page.
const LINKS = [
  { href: "/AdminOffices", icon: "building", label: "offices" },
  { href: "/AdminUsers", icon: "users", label: "users" },
  { href: "/AdminAnalytics", icon: "chart", label: "analytics" },
  { href: "/AdminBookings", icon: "list", label: "bookings" },
];

export default async function adminMenu(el) {
  clear(el, pageHeader({ title: t("adminMenu") }),
    h("div", { class: "container", style: { paddingBlock: "16px" } },
      h("nav", { class: "admin-menu", "aria-label": t("adminMenu") },
        LINKS.map((l) => h("a", { class: "card pad", href: l.href, "data-link": "" }, icon(l.icon, "lg"), h("span", { class: "bold" }, t(l.label)))))));
}
