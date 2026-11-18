import type { ReactNode } from "react";
import { t } from "../i18n";
import { fmtDate, hoursLabel, money } from "../store";
import type { BookingDto, MyBookingDto } from "../types/api";
import { Icon, StatusBadge } from "../ui";

/** A booking as the user sees it (My bookings, and an admin's view of one user's bookings). */
export function BookingCard({ booking: b, onCancel, extra }: { booking: BookingDto | MyBookingDto; onCancel?: () => void; extra?: ReactNode }) {
  // Only the owner's /api/bookings/mine returns clientName; admin routes never do.
  const clientName = "clientName" in b ? b.clientName : null;
  return (
    <article className="card pad stack" style={{ "--gap": "8px" }}>
      <div className="row between"><h3 className="truncate">{b.officeName}</h3><StatusBadge status={b.status} /></div>
      <div className="meta">
        <span><Icon name="calendar" className="sm" />{fmtDate(b.date)}</span>
        <span dir="ltr"><Icon name="clock" className="sm" />{`${b.startTime} – ${b.endTime}`}</span>
        <span>{hoursLabel(b.durationHours, t)}</span>
        <span className="bold" style={{ color: "var(--text)" }}>{money(b.totalPrice, b.currency)}</span>
      </div>
      {clientName ? (
        <p className="small row" style={{ "--gap": "6px" }}><Icon name="client" className="sm" /><span className="truncate">{clientName}</span></p>
      ) : null}
      {b.notes ? <p className="small muted">{b.notes}</p> : null}
      {b.status === "cancelled" && b.cancelledAt ? (
        <p className="xs" style={{ color: "var(--danger-fg)" }}>{`${t("cancelledOn")} ${fmtDate(b.cancelledAt, { dateStyle: "medium" })}`}</p>
      ) : null}
      {extra ?? null}
      {onCancel ? (
        <div className="row" style={{ justifyContent: "flex-end" }}>
          <button className="btn danger-outline sm" type="button" onClick={onCancel}><Icon name="x" className="sm" />{t("cancelBookingBtn")}</button>
        </div>
      ) : null}
    </article>
  );
}
