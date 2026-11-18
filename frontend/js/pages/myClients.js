import { get } from "../api.js";
import { t } from "../i18n.js";
import { clientName, openClientSheet } from "../components/clients.js";
import { isAdmin } from "../store.js";
import { clear, empty, errorBox, h, icon, pageHeader, skeletons } from "../ui.js";

// The signed-in user's private client list. Only its owner can see it (the API enforces this).
export default async function myClients(el) {
  let clients = [];
  const search = h("input", {
    class: "input", type: "search", placeholder: t("filterClients"), "aria-label": t("filterClients"),
    oninput: () => drawList(),
  });
  const list = h("div", { class: "stack", style: { "--gap": "8px" } });
  const add = () => openClientSheet({ onSaved: load });

  // Distribution lists are shown to admins only at this stage (plan 008, Q6).
  // In the body, not the header, so the page title is not squeezed at 360px.
  const addButton = h("button", { class: "btn block", type: "button", onclick: add }, icon("plus"), t("addClient"));
  const actions = isAdmin()
    ? h("div", { class: "row", style: { "--gap": "8px" } },
      h("div", { style: { flex: "1 1 0", minWidth: "0" } }, addButton),
      h("a", { class: "btn outline", style: { flex: "1 1 0", minWidth: "0" }, href: "/DistributionLists", "data-link": "" },
        icon("list", "sm"), t("distributionLists")))
    : addButton;

  clear(el, pageHeader({ title: t("myClients"), subtitle: t("clientsPrivateNote") }),
    h("div", { class: "container stack", style: { paddingBlock: "16px", "--gap": "12px" } },
      actions, search, list));

  const matches = (c, q) => !q || [clientName(c), ...c.phones, ...c.emails].some((v) => v.toLowerCase().includes(q))
    // "050-123..." should find "+97250123..."
    || c.phones.some((p) => q.replace(/\D/g, "").length > 2 && p.replace(/^\+972/, "0").includes(q.replace(/\D/g, "")));

  function drawList() {
    if (!clients.length) {
      return clear(list, empty("client", t("noClients"),
        h("div", { class: "stack center", style: { "--gap": "10px" } }, h("p", { class: "small muted" }, t("clientsPrivateNote")),
          h("button", { class: "btn", type: "button", onclick: add }, icon("plus", "sm"), t("addClient")))));
    }
    const q = search.value.trim().toLowerCase();
    const shown = clients.filter((c) => matches(c, q));
    clear(list, shown.length ? shown.map(row) : h("p", { class: "small muted center" }, t("noClients")));
  }

  const row = (c) => h("button", {
    class: "card pad row between", type: "button",
    style: { width: "100%", border: "0", cursor: "pointer", textAlign: "start", minHeight: "56px", "--gap": "12px" },
    onclick: () => openClientSheet({ client: c, onSaved: load, onDeleted: load, bookingsLink: true }),
  },
  h("span", { class: "row grow", style: { "--gap": "12px" } }, icon("client"),
    h("span", { class: "stack grow", style: { "--gap": "2px", minWidth: "0" } },
      h("span", { class: "bold truncate" }, clientName(c)),
      h("span", { class: "small muted truncate", dir: "ltr", style: { textAlign: "start" } }, c.phones[0] || ""))),
  icon("chevronRight", "sm flip-rtl"));

  async function load() {
    clear(list, skeletons(3, 56));
    try {
      clients = await get("/api/clients");
      drawList();
    } catch (e) { clear(list, errorBox(e, load)); }
  }
  load();
}
