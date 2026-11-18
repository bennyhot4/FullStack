import { get, post } from "../api.js";
import { t } from "../i18n.js";
import { addDays, fmtDate, hoursLabel, money, store, todayInBusinessTz } from "../store.js";
import { calendar } from "../components/calendar.js";
import { clientSelect } from "../components/clients.js";
import { amenityIcon, backButton, clear, empty, errorBox, errorText, field, h, icon, skeletons, toast } from "../ui.js";

const FALLBACK_IMG = "https://images.unsplash.com/photo-1497366216548-37526070297c?w=800&auto=format";
const hourOf = (s) => parseInt(s.slice(0, 2), 10);
const hh = (n) => `${String(n).padStart(2, "0")}:00`;

export default async function bookOffice(el, { params, navigate }) {
  const id = params.get("id");
  const clientsLoading = get("/api/clients").catch(() => null); // in parallel with the office
  clear(el, h("div", { class: "container", style: { paddingBlock: "16px" } }, skeletons(1, 220), h("div", { style: { height: "12px" } }), skeletons(2, 140)));

  let office;
  try { office = await get(`/api/offices/${encodeURIComponent(id || "-")}`); } catch (e) {
    if (e.status === 404) return clear(el, empty("building", t("officeNotFound"), h("a", { class: "btn", href: "/", "data-link": "" }, t("goHome"))));
    return clear(el, errorBox(e, () => bookOffice(el, { params, navigate })));
  }

  const { openHour, closeHour, maxDaysAhead } = store.settings.booking;
  const today = todayInBusinessTz();
  const maxDate = addDays(today, maxDaysAhead);
  const state = { date: today, start: null, end: null, booked: new Set(), nowHour: -1, serverToday: today };

  const slotsHost = h("div");
  const summary = h("div");
  const notes = h("textarea", { class: "textarea", placeholder: t("anySpecialReq"), maxlength: 1000, rows: 3 });
  // Optional client from the user's private list. If the list cannot be loaded the picker is simply left out.
  const clients = await clientsLoading;
  let clientId = "";
  const clientPicker = clients ? clientSelect({ clients, onChange: (v) => { clientId = v; } }) : null;

  const header = h("div", { class: "office-card", style: { position: "relative" } },
    h("div", { class: "cover", style: { aspectRatio: "16 / 8", maxHeight: "300px" } }, h("img", { src: office.imageUrl || FALLBACK_IMG, alt: "" })),
    h("div", { style: { position: "absolute", insetBlockStart: "calc(12px + var(--safe-top))", insetInlineStart: "12px" } }, backButton("/")));

  clear(el, header,
    h("div", { class: "container stack", style: { "--gap": "14px", paddingBlock: "16px" } },
      h("div", { class: "stack", style: { "--gap": "6px" } },
        h("h1", null, office.name),
        h("div", { class: "meta" },
          h("span", null, icon("users", "sm"), `${office.capacity} ${t("people")}`),
          office.location ? h("span", null, icon("pin", "sm"), office.location) : null),
        office.description ? h("p", { class: "small muted" }, office.description) : null),
      h("div", { class: "card pad row between" },
        h("div", null, h("p", { class: "xs muted" }, t("hourlyRate")), h("p", { class: "bold", style: { fontSize: "1.35rem" } }, money(office.hourlyRate, office.currency))),
        office.amenities.length ? h("div", { class: "row wrap", style: { "--gap": "6px", justifyContent: "flex-end" } },
          office.amenities.slice(0, 5).map((a) => h("span", { class: "icon-btn flat", title: a, "aria-label": a, style: { boxShadow: "none" } }, icon(amenityIcon(a), "sm")))) : null),
      h("section", { class: "card pad stack" },
        h("div", null, h("h2", null, t("selectDate")), h("p", { class: "xs muted" }, t("maxTwoWeeks"))),
        calendar({ min: today, max: maxDate, value: today, onSelect: (d) => { state.date = d; state.start = state.end = null; loadSlots(); } })),
      h("section", { class: "card pad stack" },
        h("div", null, h("h2", null, t("selectTime")), h("p", { class: "xs muted" }, t("selectTimeRange"))),
        slotsHost),
      clientPicker ? h("section", { class: "card pad stack" }, field(t("client"), clientPicker)) : null,
      h("section", { class: "card pad stack" }, h("label", { class: "label", for: "notes" }, `${t("notesOptional")} *`), Object.assign(notes, { id: "notes" }),
        h("p", { class: "xs muted" }, t("notesVisibleToAdmin"))),
      summary));

  async function loadSlots() {
    clear(slotsHost, skeletons(1, 160));
    drawSummary();
    try {
      const a = await get(`/api/offices/${office.id}/availability?date=${state.date}`);
      state.booked = new Set(a.bookedSlots);
      state.nowHour = a.today === state.date ? a.nowHour : -1;
      drawSlots();
    } catch (e) {
      clear(slotsHost, errorBox(e, loadSlots));
    }
  }

  // Tap a start hour, then the last hour you want (inclusive). Booking runs until the end of that hour.
  function pick(hour) {
    if (state.start === null || state.end !== null) {
      state.start = hour; state.end = null;
    } else if (hour < state.start) {
      state.start = hour;
    } else {
      for (let x = state.start; x <= hour; x++) {
        if (state.booked.has(hh(x))) { toast(t("rangeHasBooked"), "error"); state.start = hour; state.end = null; drawSlots(); return; }
      }
      state.end = hour + 1;
    }
    drawSlots();
  }

  function drawSlots() {
    const buttons = [];
    for (let x = openHour; x < closeHour; x++) {
      const booked = state.booked.has(hh(x));
      const past = x <= state.nowHour;
      const inRange = state.start !== null && x >= state.start && x < (state.end ?? state.start + 1);
      const edge = x === state.start || (state.end !== null && x === state.end - 1);
      buttons.push(h("button", {
        type: "button", class: `slot ${booked ? "booked" : ""} ${inRange ? "in-range" : ""} ${edge ? "edge" : ""}`,
        disabled: booked || past, "aria-pressed": String(inRange), onclick: () => pick(x),
      }, hh(x), h("small", null, booked ? t("bookedSlot") : `– ${hh(x + 1)}`)));
    }
    clear(slotsHost, h("div", { class: "slots" }, buttons));
    drawSummary();
  }

  // `summary` itself is the sticky bar: a sticky element only moves inside its parent, so it must not
  // sit in a wrapper of its own height or it never sticks and ends up below the screen on long pages.
  function drawSummary() {
    if (state.start === null) { summary.className = ""; return clear(summary); }
    const end = state.end ?? state.start + 1;
    const hours = end - state.start;
    const btn = h("button", { class: "btn lg block", type: "button" }, t("confirmBooking"));
    btn.addEventListener("click", async () => {
      btn.disabled = true;
      btn.textContent = t("booking");
      try {
        const b = await post("/api/bookings", { officeId: office.id, date: state.date, startTime: hh(state.start), endTime: hh(end), notes: notes.value, clientId: clientId || null });
        success(b);
      } catch (e) {
        if (e.code === "slot_taken") { toast(t("slotTaken"), "error"); state.start = state.end = null; loadSlots(); return; }
        if (e.code === "terms_required") { toast(t("signedTermsRequired"), "error"); return navigate("/TermsOfUse"); }
        if (e.code === "profile_required") return navigate("/Register");
        toast(errorText(e), "error");
        btn.disabled = false;
        btn.textContent = t("confirmBooking");
      }
    });
    summary.className = "action-bar stack";
    summary.style.setProperty("--gap", "10px");
    clear(summary,
      h("div", { class: "row between" },
        h("div", null,
          h("p", { class: "small bold" }, fmtDate(state.date, { weekday: "short", day: "numeric", month: "short" })),
          h("p", { class: "small muted", dir: "ltr", style: { textAlign: "start" } }, `${hh(state.start)} – ${hh(end)} · ${hoursLabel(hours, t)}`)),
        h("p", { class: "bold", style: { fontSize: "1.25rem" } }, money(hours * office.hourlyRate, office.currency))),
      btn);
  }

  function success(b) {
    clear(el, h("div", { class: "fullscreen-center" }, h("div", { class: "card pad stack center", style: { maxWidth: "420px", width: "100%", "--gap": "14px", padding: "32px 20px" } },
      h("div", { style: { color: "var(--success-fg)" } }, icon("checkCircle", "lg")),
      h("h1", null, t("bookingConfirmed")),
      h("div", { class: "muted" },
        h("p", { class: "bold", style: { color: "var(--text)" } }, b.officeName),
        b.clientName ? h("p", null, `${t("client")}: ${b.clientName}`) : null,
        h("p", null, fmtDate(b.date)),
        h("p", { dir: "ltr" }, `${b.startTime} – ${b.endTime}`),
        h("p", { class: "bold" }, money(b.totalPrice, b.currency))),
      h("a", { class: "btn block", href: "/MyBookings", "data-link": "" }, t("viewMyBookings")),
      h("a", { class: "btn outline block", href: "/", "data-link": "" }, t("bookAnother")))));
  }

  loadSlots();
}
