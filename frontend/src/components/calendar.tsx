// Month calendar limited to [min, max] (the booking window). Dates are "yyyy-MM-dd" strings.
import { useState } from "react";
import { dayShort, locale } from "../i18n";
import { isoDate } from "../store";
import { Icon } from "../ui";

export function Calendar({ min, max, value, onSelect }: { min: string; max: string; value: string; onSelect: (iso: string) => void }) {
  const start = new Date(`${value || min}T12:00:00`);
  const [view, setView] = useState(() => new Date(start.getFullYear(), start.getMonth(), 1));

  const year = view.getFullYear(), month = view.getMonth();
  const first = new Date(year, month, 1);
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const title = new Intl.DateTimeFormat(locale(), { month: "long", year: "numeric" }).format(first);
  const canPrev = isoDate(new Date(year, month, 0)) >= min;
  const canNext = isoDate(new Date(year, month + 1, 1)) <= max;
  const full = new Intl.DateTimeFormat(locale(), { dateStyle: "full" });

  const cells = [];
  for (let i = 0; i < first.getDay(); i++) cells.push(<span key={`b${i}`} />);
  for (let d = 1; d <= daysInMonth; d++) {
    const iso = isoDate(new Date(year, month, d));
    cells.push(
      <button key={iso} type="button" className={`cal-day ${iso === min ? "today" : ""}`} disabled={iso < min || iso > max}
        aria-pressed={iso === value} aria-label={full.format(new Date(year, month, d))} onClick={() => onSelect(iso)}>
        {d}
      </button>,
    );
  }

  return (
    <div className="calendar">
      <div className="cal-head">
        <button type="button" className="icon-btn flat" disabled={!canPrev} aria-label="Previous month" onClick={() => setView(new Date(year, month - 1, 1))}>
          <Icon name="chevronLeft" className="flip-rtl" />
        </button>
        <span className="bold">{title}</span>
        <button type="button" className="icon-btn flat" disabled={!canNext} aria-label="Next month" onClick={() => setView(new Date(year, month + 1, 1))}>
          <Icon name="chevronRight" className="flip-rtl" />
        </button>
      </div>
      <div className="cal-grid">
        {dayShort().map((d) => <span key={d} className="cal-dow" aria-hidden="true">{d}</span>)}
        {cells}
      </div>
    </div>
  );
}
