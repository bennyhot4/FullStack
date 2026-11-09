// Month calendar limited to [minDate, maxDate] (the booking window). Dates are "yyyy-MM-dd" strings.
import { h, icon } from "../ui.js";
import { locale, t } from "../i18n.js";
import { isoDate } from "../store.js";

export function calendar({ min, max, value, onSelect }) {
  const root = h("div", { class: "calendar" });
  const minD = new Date(`${min}T12:00:00`);
  let view = new Date((value ? new Date(`${value}T12:00:00`) : minD).getFullYear(), (value ? new Date(`${value}T12:00:00`) : minD).getMonth(), 1);
  let selected = value;

  const draw = () => {
    const year = view.getFullYear(), month = view.getMonth();
    const first = new Date(year, month, 1);
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const title = new Intl.DateTimeFormat(locale(), { month: "long", year: "numeric" }).format(first);
    const canPrev = isoDate(new Date(year, month, 0)) >= min;
    const canNext = isoDate(new Date(year, month + 1, 1)) <= max;

    const cells = [];
    for (let i = 0; i < first.getDay(); i++) cells.push(h("span"));
    for (let d = 1; d <= daysInMonth; d++) {
      const iso = isoDate(new Date(year, month, d));
      const disabled = iso < min || iso > max;
      cells.push(h("button", {
        type: "button", class: `cal-day ${iso === min ? "today" : ""}`, disabled,
        "aria-pressed": String(iso === selected), "aria-label": new Intl.DateTimeFormat(locale(), { dateStyle: "full" }).format(new Date(year, month, d)),
        onclick: () => { selected = iso; draw(); onSelect(iso); },
      }, String(d)));
    }

    root.replaceChildren(
      h("div", { class: "cal-head" },
        h("button", { type: "button", class: "icon-btn flat", disabled: !canPrev, "aria-label": "Previous month", onclick: () => { view = new Date(year, month - 1, 1); draw(); } }, icon("chevronLeft", "flip-rtl")),
        h("span", { class: "bold" }, title),
        h("button", { type: "button", class: "icon-btn flat", disabled: !canNext, "aria-label": "Next month", onclick: () => { view = new Date(year, month + 1, 1); draw(); } }, icon("chevronRight", "flip-rtl"))),
      h("div", { class: "cal-grid" },
        t("dayShort").map((d) => h("span", { class: "cal-dow", "aria-hidden": "true" }, d)),
        cells));
  };
  draw();
  return root;
}
