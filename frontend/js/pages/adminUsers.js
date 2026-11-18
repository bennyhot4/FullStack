import { del, get, post, put } from "../api.js";
import { t } from "../i18n.js";
import { fmtShortDate, store } from "../store.js";
import { bookingCard } from "./myBookings.js";
import { clear, confirmSheet, empty, errorBox, field, h, icon, openSheet, pageHeader, skeletons, toast } from "../ui.js";

export default async function adminUsers(el) {
  const count = h("span");
  const body = h("div", { class: "stack", style: { "--gap": "20px" } });
  const meId = store.me.user.id;

  clear(el,
    pageHeader({ title: t("manageUsers"), subtitle: count }),
    h("div", { class: "container wide stack", style: { paddingBlock: "16px", "--gap": "16px" } },
      h("button", { class: "btn block", type: "button", onclick: invite }, icon("plus"), t("inviteUser")),
      body));

  async function load() {
    clear(body, skeletons(3, 120));
    try {
      const users = await get("/api/admin/users");
      count.textContent = `${users.length} ${users.length === 1 ? t("totalUsers") : t("totalUsersPlural")}`;
      const admins = users.filter((u) => u.role === "admin");
      const regular = users.filter((u) => u.role !== "admin");
      clear(body,
        section("shield", `${t("administrators")} (${admins.length})`, admins),
        section("users", `${t("users")} (${regular.length})`, regular));
    } catch (e) { clear(body, errorBox(e, load)); }
  }

  const section = (ic, title, users) => h("section", { class: "stack" },
    h("h2", { class: "section-title" }, icon(ic, "sm"), title),
    users.length ? h("div", { class: "grid-cards" }, users.map(userCard)) : empty("users", t("noUsersYet")));

  const userCard = (u) => h("article", { class: "card pad stack", style: { "--gap": "10px" } },
    h("div", { class: "list-item" },
      h("div", { class: "avatar" }, u.pictureUrl ? h("img", { src: u.pictureUrl, alt: "" }) : icon(u.role === "admin" ? "shield" : "user")),
      h("div", { class: "grow stack", style: { "--gap": "2px" } },
        h("div", { class: "row", style: { "--gap": "6px" } }, h("h3", { class: "truncate" }, u.fullName || u.email),
          u.role === "admin" ? h("span", { class: "badge primary" }, t("admin")) : null,
          u.status === "invited" ? h("span", { class: "badge warning" }, t("invited")) : null),
        h("p", { class: "small muted truncate", dir: "ltr", style: { textAlign: "start" } }, u.email),
        h("p", { class: "xs muted" }, `${t("joined")} ${fmtShortDate(u.createdAt)}`),
        u.termsAccepted
          ? h("span", { class: "xs", style: { color: "var(--success-fg)" } }, icon("checkCircle", "sm"), ` ${t("termsSigned")}${u.termsAcceptedAt ? ` · ${fmtShortDate(u.termsAcceptedAt)}` : ""}`)
          : h("span", { class: "xs faint" }, icon("file", "sm"), ` ${t("termsNotSigned")}`))),
    h("div", { class: "row wrap", style: { justifyContent: "flex-end", "--gap": "6px" } },
      h("button", { class: "btn outline sm", type: "button", onclick: () => showBookings(u) }, icon("calendar", "sm"), t("bookings")),
      h("button", { class: "btn outline sm", type: "button", "aria-label": t("edit"), onclick: () => edit(u) }, icon("edit", "sm")),
      u.id !== meId ? h("button", { class: "btn outline sm", type: "button", onclick: (e) => toggleRole(u, e.currentTarget) },
        u.role === "admin" ? t("setAsUser") : t("setAsAdmin")) : null,
      u.id !== meId ? h("button", { class: "btn danger-outline sm", type: "button", "aria-label": t("delete"), onclick: () => remove(u) }, icon("trash", "sm")) : null));

  function roleSelect(value) {
    return h("select", { class: "select" },
      h("option", { value: "user", selected: value === "user" }, t("user")),
      h("option", { value: "admin", selected: value === "admin" }, t("admin")));
  }

  function invite() {
    const email = h("input", { class: "input", type: "email", required: true, placeholder: "user@example.com", autocomplete: "off", dir: "ltr" });
    const role = roleSelect("user");
    const err = h("p", { class: "error-text", role: "alert" });
    const sheet = openSheet({
      title: t("inviteNewUser"),
      body: h("form", {
        id: "invite-form", class: "stack",
        onsubmit: async (e) => {
          e.preventDefault();
          try { await post("/api/admin/users/invite", { email: email.value.trim(), role: role.value }); sheet.close(); load(); }
          catch (ex) { err.textContent = ex.message; }
        },
      }, field(t("emailAddress"), email), field(t("role"), role), err),
      actions: (close) => [h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", { class: "btn", type: "submit", form: "invite-form" }, t("sendInvite"))],
    });
  }

  function edit(u) {
    const name = h("input", { class: "input", value: u.fullName, required: true });
    const email = h("input", { class: "input", type: "email", value: u.email, required: true, dir: "ltr" });
    const phone = h("input", { class: "input", type: "tel", inputmode: "tel", value: u.phoneNumber || "", dir: "ltr" });
    const role = roleSelect(u.role);
    if (u.id === meId) role.disabled = true;
    const err = h("p", { class: "error-text", role: "alert" });
    const sheet = openSheet({
      title: t("editUser"),
      body: h("form", {
        id: "edit-user", class: "stack",
        onsubmit: async (e) => {
          e.preventDefault();
          try {
            await put(`/api/admin/users/${u.id}`, { fullName: name.value, email: email.value, phoneNumber: phone.value, role: role.value });
            sheet.close(); load();
          } catch (ex) { err.textContent = ex.message; }
        },
      }, field(t("fullName"), name), field(t("emailAddress"), email), field(t("phoneNumber"), phone), field(t("role"), role), err),
      actions: (close) => [h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", { class: "btn", type: "submit", form: "edit-user" }, t("update"))],
    });
  }

  async function toggleRole(u, btn) {
    btn.disabled = true;
    btn.textContent = t("updatingRole");
    try { await put(`/api/admin/users/${u.id}`, { role: u.role === "admin" ? "user" : "admin" }); load(); }
    catch (e) { toast(e.message, "error"); load(); }
  }

  async function remove(u) {
    if (!await confirmSheet({ title: t("deleteUser"), message: `${t("deleteUserConfirm")} ${u.fullName || u.email}? ${t("willRemoveBookings")}`, confirmText: t("delete"), danger: true })) return;
    try { await del(`/api/admin/users/${u.id}`); load(); } catch (e) { toast(e.message, "error"); }
  }

  function showBookings(u) {
    const host = h("div", { class: "stack" }, skeletons(2, 100));
    openSheet({ title: `${t("userBookings")} ${u.fullName || u.email}`, body: host });
    get(`/api/admin/users/${u.id}/bookings`)
      .then((list) => clear(host, list.length ? list.map((b) => bookingCard(b)) : empty("calendar", t("noBookings"))))
      .catch((e) => clear(host, errorBox(e)));
  }

  load();
}
