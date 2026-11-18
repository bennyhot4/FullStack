import { get, post, put } from "../api.js";
import { t } from "../i18n.js";
import { addDays, fmtDate, money, store, todayInBusinessTz } from "../store.js";
import { clear, empty, errorBox, field, h, icon, openSheet, pageHeader, skeletons, statusBadge, toast } from "../ui.js";

const hh = (n) => `${String(n).padStart(2, "0")}:00`;

export default async function adminBookings(el) {
  const count = h("span");
  const list = h("div", { class: "grid-cards" });
  let offices = [], users = [];
  const { openHour, closeHour } = store.settings.booking;

  clear(el,
    pageHeader({ title: t("manageBookings"), subtitle: count }),
    h("div", { class: "container wide stack", style: { paddingBlock: "16px", "--gap": "16px" } },
      h("button", { class: "btn block", type: "button", onclick: recurring }, icon("repeat"), t("repeatedBooking")),
      list));

  async function load() {
    clear(list, skeletons(4, 120));
    try {
      const [bookings, o, u] = await Promise.all([get("/api/admin/bookings?limit=500"), get("/api/admin/offices"), get("/api/admin/users")]);
      offices = o; users = u;
      count.textContent = `${bookings.length} ${t("allBookings")}`;
      clear(list, bookings.length ? bookings.map(card) : empty("calendar", t("noBookings")));
    } catch (e) { clear(list, errorBox(e, load)); }
  }

  const card = (b) => h("article", { class: "card pad stack", style: { "--gap": "8px" } },
    h("div", { class: "row between" }, h("h3", { class: "truncate" }, b.officeName), statusBadge(b.status)),
    h("div", { class: "meta" },
      h("span", null, icon("calendar", "sm"), fmtDate(b.date)),
      h("span", { dir: "ltr" }, icon("clock", "sm"), `${b.startTime} – ${b.endTime} (${b.durationHours}h)`),
      h("span", null, icon("user", "sm"), b.bookerName || "—"),
      h("span", { class: "bold", style: { color: "var(--text)" } }, money(b.totalPrice, b.currency))),
    b.seriesId ? h("span", { class: "xs faint" }, icon("repeat", "sm"), ` ${t("repeatedBooking")}`) : null,
    h("div", { class: "row", style: { justifyContent: "flex-end" } },
      h("button", { class: "btn outline sm", type: "button", onclick: () => edit(b) }, icon("edit", "sm"), t("edit")),
      b.status === "confirmed" ? h("button", {
        class: "btn danger-outline sm", type: "button",
        onclick: async (e) => { e.currentTarget.disabled = true; try { await post(`/api/admin/bookings/${b.id}/cancel`); load(); } catch (ex) { toast(ex.message, "error"); } },
      }, icon("x", "sm"), t("cancelBookingBtn")) : null));

  const officeSelect = (value) => h("select", { class: "select", required: true },
    h("option", { value: "", disabled: true, selected: !value }, t("selectOffice")),
    offices.map((o) => h("option", { value: o.id, selected: o.id === value }, o.name)));
  const hourSelect = (value, from, to) => h("select", { class: "select" },
    Array.from({ length: to - from + 1 }, (_, i) => from + i).map((x) => h("option", { value: hh(x), selected: hh(x) === value }, hh(x))));

  function edit(b) {
    const office = officeSelect(b.officeId);
    const booker = h("input", { class: "input", value: b.bookerName });
    const date = h("input", { class: "input", type: "date", value: b.date, required: true });
    const start = hourSelect(b.startTime, openHour, closeHour - 1);
    const end = hourSelect(b.endTime, openHour + 1, closeHour);
    const err = h("p", { class: "error-text", role: "alert" });
    const sheet = openSheet({
      title: t("editBooking"),
      body: h("form", {
        id: "edit-booking", class: "stack",
        onsubmit: async (e) => {
          e.preventDefault();
          try {
            await put(`/api/admin/bookings/${b.id}`, { officeId: office.value, bookerName: booker.value, date: date.value, startTime: start.value, endTime: end.value });
            sheet.close(); load();
          } catch (ex) { err.textContent = ex.code === "slot_taken" ? t("rangeHasBooked") : ex.message; }
        },
      }, field(t("office"), office), field(t("bookingName"), booker), field(t("date"), date),
      h("div", { class: "row", style: { "--gap": "10px" } }, h("div", { class: "grow" }, field(t("startTime"), start)), h("div", { class: "grow" }, field(t("endTime"), end))),
      err),
      actions: (close) => [h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", { class: "btn", type: "submit", form: "edit-booking" }, t("update"))],
    });
  }

  function recurring() {
    const today = todayInBusinessTz();
    const office = officeSelect("");
    const user = h("select", { class: "select" },
      h("option", { value: "" }, t("selectUser")),
      users.filter((u) => u.status !== "invited").map((u) => h("option", { value: u.id }, u.fullName || u.email)));
    const booker = h("input", { class: "input", placeholder: t("bookingName") });
    user.addEventListener("change", () => { const u = users.find((x) => x.id === user.value); if (u) booker.value = u.fullName; });
    const start = hourSelect("08:00", openHour, closeHour - 1);
    const end = hourSelect("09:00", openHour + 1, closeHour);
    const from = h("input", { class: "input", type: "date", value: today, required: true });
    const to = h("input", { class: "input", type: "date", value: addDays(today, 30), required: true });
    const notes = h("input", { class: "input" });
    const days = new Set();
    const dayPicker = h("div", { class: "days-picker", role: "group", "aria-label": t("days") },
      t("dayShort").map((d, i) => h("button", {
        class: "chip", type: "button", "aria-pressed": "false",
        onclick: (e) => { days.has(i) ? days.delete(i) : days.add(i); e.currentTarget.setAttribute("aria-pressed", String(days.has(i))); },
      }, d)));
    const err = h("p", { class: "error-text", role: "alert" });
    const host = h("div");

    const form = h("form", {
      id: "recurring", class: "stack",
      onsubmit: async (e) => {
        e.preventDefault();
        err.textContent = "";
        if (!days.size) { err.textContent = t("days"); return; }
        try {
          const r = await post("/api/admin/bookings/recurring", {
            officeId: office.value, userId: user.value || null, bookerName: booker.value, startTime: start.value, endTime: end.value,
            days: [...days], fromDate: from.value, toDate: to.value, notes: notes.value,
          });
          clear(host, h("div", { class: "empty" }, h("div", { style: { fontSize: "2.5rem" } }, "✅"),
            h("p", { class: "bold" }, `${r.created} ${t("bookingsCreated")}`),
            r.skipped ? h("p", { class: "small muted" }, `${r.skipped} ${t("slotsSkipped")}`) : null,
            h("button", { class: "btn", type: "button", onclick: () => sheet.close() }, t("done"))));
          sheet.el.querySelector(".sheet-foot")?.remove();
          load();
        } catch (ex) { err.textContent = ex.message; }
      },
    },
    field(t("office"), office), field(t("bookFor"), user), field(t("bookingName"), booker),
    h("div", { class: "row", style: { "--gap": "10px" } }, h("div", { class: "grow" }, field(t("startTime"), start)), h("div", { class: "grow" }, field(t("endTime"), end))),
    h("div", { class: "field" }, h("span", { class: "label" }, t("days")), dayPicker),
    h("div", { class: "row", style: { "--gap": "10px" } }, h("div", { class: "grow" }, field(t("fromDate"), from)), h("div", { class: "grow" }, field(t("toDate"), to))),
    field(t("notesLabel"), notes), err);
    host.append(form);

    const sheet = openSheet({
      title: t("repeatedBooking"), body: host,
      actions: (close) => [h("button", { class: "btn outline", type: "button", onclick: close }, t("cancel")),
        h("button", { class: "btn", type: "submit", form: "recurring" }, t("createBookings"))],
    });
  }

  load();
}
