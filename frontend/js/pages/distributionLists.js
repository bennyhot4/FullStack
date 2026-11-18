import { del, get, post, put } from "../api.js";
import { t } from "../i18n.js";
import { clientName } from "../components/clients.js";
import { clear, confirmSheet, empty, errorBox, errorText, field, h, icon, openSheet, pageHeader, skeletons, toast } from "../ui.js";

// The signed-in user's private distribution lists (plan 008). The API is open to every user; for now the
// route is admin-only and the entry button on /MyClients is shown to admins only.
export default async function distributionLists(el) {
  let lists = [];
  let clients = [];
  const body = h("div", { class: "stack", style: { "--gap": "8px" } });
  const add = () => openListSheet({ list: null });

  clear(el, pageHeader({ title: t("distributionLists"), subtitle: t("distributionListsNote"), back: "/MyClients" }),
    h("div", { class: "container stack", style: { paddingBlock: "16px", "--gap": "12px" } },
      h("button", { class: "btn block", type: "button", onclick: add }, icon("plus"), t("newDistributionList")),
      body));

  function draw() {
    if (!lists.length) {
      return clear(body, empty("list", t("noDistributionLists"),
        h("button", { class: "btn", type: "button", onclick: add }, icon("plus", "sm"), t("newDistributionList"))));
    }
    clear(body, lists.map(row));
  }

  const row = (l) => h("button", {
    class: "card pad row between", type: "button",
    style: { width: "100%", border: "0", cursor: "pointer", textAlign: "start", minHeight: "56px", "--gap": "12px" },
    onclick: () => openExisting(l),
  },
  h("span", { class: "row grow", style: { "--gap": "12px" } }, icon("list"),
    h("span", { class: "stack grow", style: { "--gap": "2px", minWidth: "0" } },
      h("span", { class: "bold truncate" }, l.name),
      h("span", { class: "small muted truncate" }, `${l.clientCount} ${t("clientsCount")}`))),
  icon("chevronRight", "sm flip-rtl"));

  async function openExisting(summary) {
    try {
      openListSheet({ list: await get(`/api/distribution-lists/${summary.distributionListId}`) });
    } catch (ex) {
      toast(errorText(ex), "error");
      if (ex.status === 404) load();
    }
  }

  /** Add/edit sheet: name, description and a filterable checkbox list of the user's clients. */
  function openListSheet({ list }) {
    const selected = new Set(list?.clientIds || []);
    const name = h("input", { class: "input", required: true, maxlength: 60, autocomplete: "off", value: list?.name || "" });
    const description = h("textarea", { class: "textarea", maxlength: 300, rows: 2 }, list?.description || "");
    const search = h("input", {
      class: "input", type: "search", placeholder: t("filterClients"), "aria-label": t("filterClients"),
      oninput: () => drawMembers(),
    });
    const count = h("span", { class: "small muted" });
    const members = h("div", { class: "stack", style: { "--gap": "0" } });
    const err = h("p", { class: "error-text", role: "alert" });
    const formId = "distribution-list-form";

    const shown = () => {
      const q = search.value.trim().toLowerCase();
      return clients.filter((c) => !q || [clientName(c), ...c.phones, ...c.emails].some((v) => v.toLowerCase().includes(q)));
    };
    const updateCount = () => { count.textContent = `${selected.size} ${t("selectedCount")}`; };

    function drawMembers() {
      const visible = shown();
      updateCount();
      if (!clients.length) return clear(members, h("p", { class: "small muted" }, t("noClients")));
      clear(members, visible.length ? visible.map((c) => {
        const box = h("input", {
          type: "checkbox", checked: selected.has(c.clientId),
          onchange: () => { box.checked ? selected.add(c.clientId) : selected.delete(c.clientId); updateCount(); },
        });
        return h("label", { class: "check-row" }, box,
          h("span", { class: "stack grow", style: { "--gap": "0", minWidth: "0" } },
            h("span", { class: "truncate" }, clientName(c)),
            h("span", { class: "xs muted truncate", dir: "ltr", style: { textAlign: "start" } }, c.phones[0] || "")));
      }) : h("p", { class: "small muted" }, t("noClients")));
    }

    const setShown = (on) => {
      for (const c of shown()) on ? selected.add(c.clientId) : selected.delete(c.clientId);
      drawMembers();
    };

    const form = h("form", {
      id: formId, class: "stack", style: { "--gap": "14px" },
      onsubmit: async (e) => {
        e.preventDefault();
        err.textContent = "";
        // Keep only ids of clients that still exist, in client-list order.
        const ids = clients.map((c) => c.clientId).filter((id) => selected.has(id));
        const payload = { name: name.value.trim(), description: description.value.trim(), clientIds: ids };
        try {
          if (list) await put(`/api/distribution-lists/${list.distributionListId}`, payload);
          else await post("/api/distribution-lists", payload);
          toast(t("distributionListSaved"), "success");
          sheet.close();
          load();
        } catch (ex) { err.textContent = errorText(ex); }
      },
    },
    field(t("listName"), name),
    field(t("listDescription"), description),
    h("div", { class: "stack", style: { "--gap": "8px" } },
      h("div", { class: "row between", style: { "--gap": "8px" } }, h("span", { class: "bold" }, t("listMembers")), count),
      search,
      h("div", { class: "row", style: { "--gap": "8px", flexWrap: "wrap" } },
        h("button", { class: "btn ghost sm", type: "button", onclick: () => setShown(true) }, t("selectAllShown")),
        h("button", { class: "btn ghost sm", type: "button", onclick: () => setShown(false) }, t("clearSelection"))),
      members),
    err);

    async function remove() {
      const ok = await confirmSheet({
        title: t("deleteDistributionList"), message: `${t("deleteDistributionListConfirm")} ${list.name}?`,
        confirmText: t("delete"), danger: true,
      });
      if (!ok) return;
      try {
        await del(`/api/distribution-lists/${list.distributionListId}`);
        toast(t("distributionListDeleted"), "success");
        sheet.close();
        load();
      } catch (ex) { toast(errorText(ex), "error"); }
    }

    const sheet = openSheet({
      title: list ? t("editDistributionList") : t("newDistributionList"), body: form,
      actions: (close) => [
        list ? h("button", { class: "btn danger-outline", type: "button", onclick: remove }, t("delete")) : null,
        h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", { class: "btn", type: "submit", form: formId }, t("saveClient")),
      ].filter(Boolean),
    });
    drawMembers();
    if (!list) name.focus();
  }

  async function load() {
    clear(body, skeletons(3, 56));
    try {
      [lists, clients] = await Promise.all([get("/api/distribution-lists"), get("/api/clients")]);
      draw();
    } catch (e) { clear(body, errorBox(e, load)); }
  }
  load();
}
