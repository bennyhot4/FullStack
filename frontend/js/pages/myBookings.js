import { get, post } from "../api.js";
import { t } from "../i18n.js";
import { fmtDate, hoursLabel, money } from "../store.js";
import { clear, confirmSheet, empty, errorBox, h, icon, pageHeader, skeletons, statusBadge, toast } from "../ui.js";

export default async function myBookings(el) {
  let tab = "upcoming";
  let data = { upcoming: [], past: [] };
  const tabs = h("div", { class: "tabs", role: "tablist" });
  const list = h("div", { class: "stack" });

  clear(el, pageHeader({ title: t("myBookings") }),
    h("div", { class: "container stack", style: { paddingBlock: "16px", "--gap": "16px" } }, tabs, list));

  const drawTabs = () => clear(tabs, ["upcoming", "past"].map((k) => h("button", {
    class: "tab", role: "tab", type: "button", "aria-selected": String(tab === k),
    onclick: () => { tab = k; drawTabs(); drawList(); },
  }, `${t(k)} (${data[k].length})`)));

  const drawList = () => {
    const items = data[tab];
    if (!items.length) {
      return clear(list, empty("calendar", t(tab === "upcoming" ? "noUpcomingBookings" : "noPastBookings"),
        h("a", { class: "btn", href: "/", "data-link": "" }, t("bookAnOffice"))));
    }
    clear(list, items.map((b) => bookingCard(b, tab === "upcoming" ? () => cancel(b) : null)));
  };

  async function cancel(b) {
    const ok = await confirmSheet({
      title: t("cancelBooking"),
      message: `${t("areYouSure")} ${b.officeName}? ${t("cannotBeUndone")}`,
      confirmText: t("yesCancel"), cancelText: t("keepBooking"), danger: true,
    });
    if (!ok) return;
    try { await post(`/api/bookings/${b.id}/cancel`); load(); } catch (e) { toast(e.message, "error"); }
  }

  async function load() {
    clear(list, skeletons(3, 110));
    try {
      data = await get("/api/bookings/mine");
      drawTabs();
      drawList();
    } catch (e) {
      clear(list, errorBox(e, load));
    }
  }
  drawTabs();
  load();
}

export function bookingCard(b, onCancel, extra) {
  return h("article", { class: "card pad stack", style: { "--gap": "8px" } },
    h("div", { class: "row between" }, h("h3", { class: "truncate" }, b.officeName), statusBadge(b.status)),
    h("div", { class: "meta" },
      h("span", null, icon("calendar", "sm"), fmtDate(b.date)),
      h("span", { dir: "ltr" }, icon("clock", "sm"), `${b.startTime} – ${b.endTime}`),
      h("span", null, hoursLabel(b.durationHours, t)),
      h("span", { class: "bold", style: { color: "var(--text)" } }, money(b.totalPrice, b.currency))),
    b.notes ? h("p", { class: "small muted" }, b.notes) : null,
    b.status === "cancelled" && b.cancelledAt ? h("p", { class: "xs", style: { color: "var(--danger-fg)" } }, `${t("cancelledOn")} ${fmtDate(b.cancelledAt, { dateStyle: "medium" })}`) : null,
    extra || null,
    onCancel ? h("div", { class: "row", style: { justifyContent: "flex-end" } },
      h("button", { class: "btn danger-outline sm", type: "button", onclick: onCancel }, icon("x", "sm"), t("cancelBookingBtn"))) : null);
}
