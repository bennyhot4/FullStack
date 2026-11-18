import { get, post, put } from "../api.js";
import { t } from "../i18n.js";
import { fmtDate, hoursLabel, money } from "../store.js";
import { clientName, clientSelect } from "../components/clients.js";
import { clear, confirmSheet, empty, errorBox, errorText, field, h, icon, openSheet, pageHeader, skeletons, statusBadge, toast } from "../ui.js";

export default async function myBookings(el, { params } = {}) {
  let tab = "upcoming";
  let data = { upcoming: [], past: [] };
  let clients = [];
  // Client filter, kept in the URL (?clientId=) so /MyClients can link here already filtered.
  let filter = params?.get("clientId") || "";
  const tabs = h("div", { class: "tabs", role: "tablist" });
  const filterHost = h("div");
  const list = h("div", { class: "stack" });

  clear(el, pageHeader({ title: t("myBookings") }),
    h("div", { class: "container stack", style: { paddingBlock: "16px", "--gap": "16px" } }, filterHost, tabs, list));

  const shown = (k) => (filter ? data[k].filter((b) => b.clientId === filter) : data[k]);

  const drawTabs = () => clear(tabs, ["upcoming", "past"].map((k) => h("button", {
    class: "tab", role: "tab", type: "button", "aria-selected": String(tab === k),
    onclick: () => { tab = k; drawTabs(); drawList(); },
  }, `${t(k)} (${shown(k).length})`)));

  function drawFilter() {
    // An unknown or deleted client in the URL falls back to "All clients".
    if (filter && !clients.some((c) => c.clientId === filter)) setFilter("");
    if (!clients.length) return clear(filterHost);
    const select = h("select", {
      class: "select", onchange: () => { setFilter(select.value); drawTabs(); drawList(); },
    },
    h("option", { value: "", selected: !filter }, t("allClients")),
    clients.map((c) => h("option", { value: c.clientId, selected: c.clientId === filter }, clientName(c))));
    clear(filterHost, field(t("filterByClient"), select));
  }

  function setFilter(v) {
    filter = v;
    const url = v ? `/MyBookings?clientId=${encodeURIComponent(v)}` : "/MyBookings";
    history.replaceState(history.state, "", url);
  }

  const drawList = () => {
    const items = shown(tab);
    if (!items.length) {
      return clear(list, empty("calendar", t(tab === "upcoming" ? "noUpcomingBookings" : "noPastBookings"),
        h("a", { class: "btn", href: "/", "data-link": "" }, t("bookAnOffice"))));
    }
    clear(list, items.map((b) => bookingCard(b, tab === "upcoming" ? () => cancel(b) : null,
      h("div", { class: "row", style: { justifyContent: "flex-end" } },
        h("button", { class: "btn outline sm", type: "button", onclick: () => pickClient(b) },
          icon("client", "sm"), t(b.clientId ? "changeClient" : "chooseClient"))))));
  };

  async function cancel(b) {
    const ok = await confirmSheet({
      title: t("cancelBooking"),
      message: `${t("areYouSure")} ${b.officeName}? ${t("cannotBeUndone")}`,
      confirmText: t("yesCancel"), cancelText: t("keepBooking"), danger: true,
    });
    if (!ok) return;
    try { await post(`/api/bookings/${b.id}/cancel`); load(); } catch (e) { toast(errorText(e), "error"); }
  }

  // Set, change or remove (choose "No client") the booking's client.
  function pickClient(b) {
    let value = clients.some((c) => c.clientId === b.clientId) ? b.clientId : "";
    const select = clientSelect({ clients, value, onChange: (v) => { value = v; } });
    openSheet({
      title: t("client"),
      body: h("div", { class: "stack" }, h("p", { class: "small muted" }, `${b.officeName} · ${fmtDate(b.date)}`), field(t("client"), select)),
      actions: (close) => [
        h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", {
          class: "btn", type: "button",
          onclick: async () => {
            try {
              await put(`/api/bookings/${b.id}/client`, { clientId: value || null });
              close();
              load();
            } catch (e) { toast(errorText(e), "error"); }
          },
        }, t("saveClient")),
      ],
    });
  }

  async function load() {
    clear(list, skeletons(3, 110));
    try {
      [data, clients] = await Promise.all([get("/api/bookings/mine"), get("/api/clients").catch(() => [])]);
      drawFilter();
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
    // Only the owner's /api/bookings/mine returns clientName; admin routes never do.
    b.clientName ? h("p", { class: "small row", style: { "--gap": "6px" } }, icon("client", "sm"), h("span", { class: "truncate" }, b.clientName)) : null,
    b.notes ? h("p", { class: "small muted" }, b.notes) : null,
    b.status === "cancelled" && b.cancelledAt ? h("p", { class: "xs", style: { color: "var(--danger-fg)" } }, `${t("cancelledOn")} ${fmtDate(b.cancelledAt, { dateStyle: "medium" })}`) : null,
    extra || null,
    onCancel ? h("div", { class: "row", style: { justifyContent: "flex-end" } },
      h("button", { class: "btn danger-outline sm", type: "button", onclick: onCancel }, icon("x", "sm"), t("cancelBookingBtn"))) : null);
}
