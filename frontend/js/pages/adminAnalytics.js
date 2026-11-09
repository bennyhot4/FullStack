import { get } from "../api.js";
import { t } from "../i18n.js";
import { fmtShortDate, money } from "../store.js";
import { clear, empty, errorBox, h, pageHeader, skeletons } from "../ui.js";

export default async function adminAnalytics(el) {
  let period = "week";
  const range = h("span");
  const tabs = h("div", { class: "tabs", role: "tablist" });
  const body = h("div", { class: "stack", style: { "--gap": "16px" } });

  clear(el, pageHeader({ title: t("usageAnalytics"), subtitle: range }),
    h("div", { class: "container stack", style: { paddingBlock: "16px", "--gap": "16px" } }, tabs, body));

  const drawTabs = () => clear(tabs, [["week", t("thisWeek")], ["month", t("thisMonth")]].map(([k, label]) =>
    h("button", { class: "tab", role: "tab", type: "button", "aria-selected": String(period === k), onclick: () => { period = k; drawTabs(); load(); } }, label)));

  async function load() {
    clear(body, skeletons(1, 90), skeletons(3, 70));
    try {
      const a = await get(`/api/admin/analytics?period=${period}`);
      range.textContent = `${fmtShortDate(a.from)} – ${fmtShortDate(a.to)}`;
      const stat = (value, label) => h("div", { class: "stat" }, h("div", { class: "value" }, value), h("div", { class: "label" }, label));
      clear(body,
        h("div", { class: "stats" }, stat(a.totalBookings, t("totalBookings")), stat(a.totalHours, t("totalHours")), stat(money(a.revenue), t("revenue"))),
        h("section", { class: "stack" }, h("h2", null, t("usageByUser")),
          a.byUser.length ? a.byUser.map((u, i) => h("article", { class: "card pad list-item" },
            h("div", { class: "avatar bold" }, String(i + 1)),
            h("div", { class: "grow" }, h("h3", { class: "truncate" }, u.fullName || u.email), h("p", { class: "xs muted truncate", dir: "ltr", style: { textAlign: "start" } }, u.email),
              h("div", { class: "meta", style: { marginTop: "6px" } },
                h("span", null, `${t("bookings")}: `, h("b", null, u.bookings)),
                h("span", null, `${t("hours")}: `, h("b", null, u.hours)),
                h("span", null, `${t("spent")}: `, h("b", null, money(u.spent)))))))
            : empty("chart", t("noBookingsInPeriod"))));
    } catch (e) { clear(body, errorBox(e, load)); }
  }
  drawTabs();
  load();
}
