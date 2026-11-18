// The user's private client list: the add/edit sheet and the client picker, shared by
// /MyClients, /BookOffice and /MyBookings.
import { del, post, put } from "../api.js";
import { t } from "../i18n.js";
import { confirmSheet, errorText, field, h, icon, openSheet, toast } from "../ui.js";

export const clientName = (c) => `${c.firstName} ${c.lastName}`.trim();

/** An input plus an "add another" link that reveals a second input (phones, emails). */
function pair(type, values, firstLabel, secondLabel, addLabel, attrs) {
  const first = h("input", { class: "input", type, value: values[0] || "", ...attrs });
  const second = h("input", { class: "input", type, value: values[1] || "", ...attrs });
  const secondField = field(secondLabel, second);
  secondField.hidden = !values[1];
  const add = h("button", {
    class: "btn ghost sm", type: "button", hidden: !!values[1],
    onclick: () => { secondField.hidden = false; add.hidden = true; second.focus(); },
  }, addLabel);
  return {
    nodes: [field(firstLabel, first), secondField, h("div", null, add)],
    values: () => [first.value, second.value].map((v) => v.trim()).filter(Boolean),
  };
}

/**
 * Opens the add/edit sheet. `client` is null for a new client.
 * onSaved(client) after a save, onDeleted(client) after a delete.
 * With `bookingsLink`, an existing client gets a "Bookings with this client" link.
 */
export function openClientSheet({ client = null, onSaved, onDeleted, bookingsLink = false } = {}) {
  const c = client || { firstName: "", lastName: "", phones: [], emails: [], language: "he", whatsAppConsent: false, notes: "" };
  const firstName = h("input", { class: "input", required: true, maxlength: 60, autocomplete: "off", value: c.firstName });
  const lastName = h("input", { class: "input", maxlength: 60, autocomplete: "off", value: c.lastName });
  const phones = pair("tel", c.phones, t("phoneNumber"), t("secondPhone"), t("addPhone"),
    { dir: "ltr", inputmode: "tel", autocomplete: "off", maxlength: 30 });
  const emails = pair("email", c.emails, t("email"), t("secondEmail"), t("addEmail"),
    { dir: "ltr", autocomplete: "off", maxlength: 200 });
  const language = h("select", { class: "select" },
    h("option", { value: "he", selected: c.language === "he" }, t("langHe")),
    h("option", { value: "en", selected: c.language === "en" }, t("langEn")));
  const consent = h("input", { type: "checkbox", checked: c.whatsAppConsent });
  const notes = h("textarea", { class: "textarea", maxlength: 500, rows: 3 }, c.notes);
  const err = h("p", { class: "error-text", role: "alert" });
  const formId = "client-form";

  const form = h("form", {
    id: formId, class: "stack", style: { "--gap": "14px" },
    onsubmit: async (e) => {
      e.preventDefault();
      err.textContent = "";
      const body = {
        firstName: firstName.value.trim(), lastName: lastName.value.trim(),
        phones: phones.values(), emails: emails.values(), language: language.value,
        whatsAppConsent: consent.checked, notes: notes.value.trim(),
      };
      try {
        const saved = client ? await put(`/api/clients/${client.clientId}`, body) : await post("/api/clients", body);
        toast(t("clientSaved"), "success");
        sheet.close();
        onSaved?.(saved);
      } catch (ex) { err.textContent = errorText(ex); }
    },
  },
  h("div", { class: "row", style: { "--gap": "10px", alignItems: "flex-start" } },
    h("div", { class: "grow" }, field(t("firstName"), firstName)),
    h("div", { class: "grow" }, field(t("lastName"), lastName))),
  // A copy of a registered user (plan 008): their own profile changes update the name, first phone and email.
  client?.sourceUserId ? h("p", { class: "row small muted", style: { "--gap": "6px" } }, icon("user", "sm"), t("registeredUser")) : null,
  phones.nodes, emails.nodes,
  field(t("clientLanguage"), language),
  h("div", null, h("label", { class: "switch" }, consent, t("whatsAppConsent")), h("p", { class: "xs muted" }, t("whatsAppConsentHint"))),
  field(t("clientNotes"), notes, t("clientNotesHint")),
  client && bookingsLink ? h("a", {
    class: "btn ghost sm", href: `/MyBookings?clientId=${encodeURIComponent(client.clientId)}`, "data-link": "",
    onclick: () => sheet.close(),
  }, t("bookingsWithClient")) : null,
  err);

  async function remove() {
    const ok = await confirmSheet({
      title: t("deleteClient"), message: `${t("deleteClientConfirm")} ${clientName(client)}?`,
      confirmText: t("delete"), danger: true,
    });
    if (!ok) return;
    try {
      await del(`/api/clients/${client.clientId}`);
      toast(t("clientDeleted"), "success");
      sheet.close();
      onDeleted?.(client);
    } catch (ex) { toast(errorText(ex), "error"); }
  }

  const sheet = openSheet({
    title: client ? t("editClient") : t("addClient"), body: form,
    actions: (close) => [
      client ? h("button", { class: "btn danger-outline", type: "button", onclick: remove }, t("delete")) : null,
      h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
      h("button", { class: "btn", type: "submit", form: formId }, t("saveClient")),
    ].filter(Boolean),
  });
  return sheet;
}

/**
 * A <select> of the user's clients: "No client", each client, and "+ New client", which opens the
 * client sheet and selects the new client. `clients` is updated in place when one is added.
 */
export function clientSelect({ clients, value = "", onChange } = {}) {
  const NEW = "__new";
  const select = h("select", { class: "select" });
  let current = value || "";
  const draw = () => select.replaceChildren(
    h("option", { value: "", selected: !current }, t("noClient")),
    ...clients.map((c) => h("option", { value: c.clientId, selected: c.clientId === current }, clientName(c))),
    h("option", { value: NEW }, t("newClient")));
  draw();
  select.addEventListener("change", () => {
    if (select.value !== NEW) { current = select.value; onChange?.(current); return; }
    select.value = current;
    openClientSheet({
      onSaved: (saved) => {
        clients.push(saved);
        clients.sort((a, b) => a.lastName.localeCompare(b.lastName) || a.firstName.localeCompare(b.firstName));
        current = saved.clientId;
        draw();
        onChange?.(current);
      },
    });
  });
  return select;
}
