import { useState } from "react";
import { asApiError, get, post } from "../api";
import { t } from "../i18n";
import { Link, navigate } from "../router";
import { addDays, fmtDate, hoursLabel, money, settings, todayInBusinessTz } from "../store";
import type { Availability, ClientDto, MyBookingDto, OfficeDto } from "../types/api";
import { useAsync } from "../useAsync";
import { Calendar } from "../components/calendar";
import { ClientSelect } from "../components/clients";
import { BackButton, Empty, ErrorBox, Field, Icon, Skeletons, amenityIcon, errorText, toast } from "../ui";
import type { PageProps } from "../app";

const FALLBACK_IMG = "https://images.unsplash.com/photo-1497366216548-37526070297c?w=800&auto=format";
const hh = (n: number) => `${String(n).padStart(2, "0")}:00`;

export default function BookOffice({ params }: PageProps) {
  const id = params.get("id") || "-";
  // The client list loads in parallel with the office; if it fails the picker is simply left out.
  const { state, reload } = useAsync(() => Promise.all([
    get<OfficeDto>(`/api/offices/${encodeURIComponent(id)}`),
    get<ClientDto[]>("/api/clients").catch(() => null),
  ]), id);
  const [done, setDone] = useState<MyBookingDto | null>(null);

  if (state.status === "loading") {
    return (
      <div className="container" style={{ paddingBlock: "16px" }}>
        <Skeletons n={1} height={220} /><div style={{ height: "12px" }} /><Skeletons n={2} height={140} />
      </div>
    );
  }
  if (state.status === "error") {
    if (asApiError(state.error).status === 404) {
      return <Empty icon="building" text={t("officeNotFound")}><Link className="btn" href="/">{t("goHome")}</Link></Empty>;
    }
    return <ErrorBox error={state.error} onRetry={reload} />;
  }
  if (done) return <Success booking={done} />;
  const [office, clients] = state.data;
  return <Booker office={office} clients={clients} onBooked={setDone} />;
}

function Booker({ office, clients, onBooked }: { office: OfficeDto; clients: ClientDto[] | null; onBooked: (b: MyBookingDto) => void }) {
  const { openHour, closeHour, maxDaysAhead } = settings().booking;
  const today = todayInBusinessTz();
  const maxDate = addDays(today, maxDaysAhead);
  const [date, setDate] = useState(today);
  const [start, setStart] = useState<number | null>(null);
  const [end, setEnd] = useState<number | null>(null);
  const [notes, setNotes] = useState("");
  const [clientId, setClientId] = useState("");
  const [booking, setBooking] = useState(false);
  const slots = useAsync(() => get<Availability>(`/api/offices/${office.id}/availability?date=${date}`), date);

  const booked = new Set(slots.state.status === "done" ? slots.state.data.bookedSlots : []);
  const nowHour = slots.state.status === "done" && slots.state.data.today === date ? slots.state.data.nowHour : -1;

  const clearRange = () => { setStart(null); setEnd(null); };

  // Tap a start hour, then the last hour you want (inclusive). Booking runs until the end of that hour.
  function pick(hour: number) {
    if (start === null || end !== null) { setStart(hour); setEnd(null); return; }
    if (hour < start) { setStart(hour); return; }
    for (let x = start; x <= hour; x++) {
      if (booked.has(hh(x))) { toast(t("rangeHasBooked"), "error"); setStart(hour); setEnd(null); return; }
    }
    setEnd(hour + 1);
  }

  async function confirm() {
    if (start === null) return;
    const until = end ?? start + 1;
    setBooking(true);
    try {
      onBooked(await post<MyBookingDto>("/api/bookings", {
        officeId: office.id, date, startTime: hh(start), endTime: hh(until), notes, clientId: clientId || null,
      }));
    } catch (e) {
      setBooking(false);
      const err = asApiError(e);
      if (err.code === "slot_taken") { toast(t("slotTaken"), "error"); clearRange(); slots.reload(); return; }
      if (err.code === "terms_required") { toast(t("signedTermsRequired"), "error"); navigate("/TermsOfUse"); return; }
      if (err.code === "profile_required") { navigate("/Register"); return; }
      toast(errorText(e), "error");
    }
  }

  const slotButtons = [];
  for (let x = openHour; x < closeHour; x++) {
    const isBooked = booked.has(hh(x));
    const inRange = start !== null && x >= start && x < (end ?? start + 1);
    const edge = x === start || (end !== null && x === end - 1);
    slotButtons.push(
      <button key={x} type="button" className={`slot ${isBooked ? "booked" : ""} ${inRange ? "in-range" : ""} ${edge ? "edge" : ""}`}
        disabled={isBooked || x <= nowHour} aria-pressed={inRange} onClick={() => pick(x)}>
        {hh(x)}<small>{isBooked ? t("bookedSlot") : `– ${hh(x + 1)}`}</small>
      </button>,
    );
  }

  const until = start === null ? null : end ?? start + 1;
  const hours = start === null || until === null ? 0 : until - start;

  return (
    <>
      <div className="office-card" style={{ position: "relative" }}>
        <div className="cover" style={{ aspectRatio: "16 / 8", maxHeight: "300px" }}><img src={office.imageUrl || FALLBACK_IMG} alt="" /></div>
        <div style={{ position: "absolute", insetBlockStart: "calc(12px + var(--safe-top))", insetInlineStart: "12px" }}><BackButton href="/" /></div>
      </div>
      <div className="container stack" style={{ "--gap": "14px", paddingBlock: "16px" }}>
        <div className="stack" style={{ "--gap": "6px" }}>
          <h1>{office.name}</h1>
          <div className="meta">
            <span><Icon name="users" className="sm" />{`${office.capacity} ${t("people")}`}</span>
            {office.location ? <span><Icon name="pin" className="sm" />{office.location}</span> : null}
          </div>
          {office.description ? <p className="small muted">{office.description}</p> : null}
        </div>
        <div className="card pad row between">
          <div>
            <p className="xs muted">{t("hourlyRate")}</p>
            <p className="bold" style={{ fontSize: "1.35rem" }}>{money(office.hourlyRate, office.currency)}</p>
          </div>
          {office.amenities.length ? (
            <div className="row wrap" style={{ "--gap": "6px", justifyContent: "flex-end" }}>
              {office.amenities.slice(0, 5).map((a) => (
                <span key={a} className="icon-btn flat" title={a} aria-label={a} style={{ boxShadow: "none" }}><Icon name={amenityIcon(a)} className="sm" /></span>
              ))}
            </div>
          ) : null}
        </div>
        <section className="card pad stack">
          <div><h2>{t("selectDate")}</h2><p className="xs muted">{t("maxTwoWeeks")}</p></div>
          <Calendar min={today} max={maxDate} value={date} onSelect={(d) => { setDate(d); clearRange(); }} />
        </section>
        <section className="card pad stack">
          <div><h2>{t("selectTime")}</h2><p className="xs muted">{t("selectTimeRange")}</p></div>
          <div>
            {slots.state.status === "loading" ? <Skeletons n={1} height={160} />
              : slots.state.status === "error" ? <ErrorBox error={slots.state.error} onRetry={slots.reload} />
                : <div className="slots">{slotButtons}</div>}
          </div>
        </section>
        {clients ? (
          <section className="card pad stack">
            <Field label={t("client")}><ClientSelect clients={clients} value={clientId} onChange={setClientId} /></Field>
          </section>
        ) : null}
        <section className="card pad stack">
          <label className="label" htmlFor="notes">{`${t("notesOptional")} *`}</label>
          <textarea id="notes" className="textarea" placeholder={t("anySpecialReq")} maxLength={1000} rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} />
          <p className="xs muted">{t("notesVisibleToAdmin")}</p>
        </section>
        {/* The summary itself is the sticky bar: a sticky element only moves inside its parent, so it must
            not sit in a wrapper of its own height or it never sticks and ends up below the screen. */}
        {start !== null && until !== null ? (
          <div className="action-bar stack" style={{ "--gap": "10px" }}>
            <div className="row between">
              <div>
                <p className="small bold">{fmtDate(date, { weekday: "short", day: "numeric", month: "short" })}</p>
                <p className="small muted" dir="ltr" style={{ textAlign: "start" }}>{`${hh(start)} – ${hh(until)} · ${hoursLabel(hours, t)}`}</p>
              </div>
              <p className="bold" style={{ fontSize: "1.25rem" }}>{money(hours * office.hourlyRate, office.currency)}</p>
            </div>
            <button className="btn lg block" type="button" disabled={booking} onClick={confirm}>{booking ? t("booking") : t("confirmBooking")}</button>
          </div>
        ) : null}
      </div>
    </>
  );
}

function Success({ booking: b }: { booking: MyBookingDto }) {
  return (
    <div className="fullscreen-center">
      <div className="card pad stack center" style={{ maxWidth: "420px", width: "100%", "--gap": "14px", padding: "32px 20px" }}>
        <div style={{ color: "var(--success-fg)" }}><Icon name="checkCircle" className="lg" /></div>
        <h1>{t("bookingConfirmed")}</h1>
        <div className="muted">
          <p className="bold" style={{ color: "var(--text)" }}>{b.officeName}</p>
          {b.clientName ? <p>{`${t("client")}: ${b.clientName}`}</p> : null}
          <p>{fmtDate(b.date)}</p>
          <p dir="ltr">{`${b.startTime} – ${b.endTime}`}</p>
          <p className="bold">{money(b.totalPrice, b.currency)}</p>
        </div>
        <Link className="btn block" href="/MyBookings">{t("viewMyBookings")}</Link>
        <Link className="btn outline block" href="/">{t("bookAnother")}</Link>
      </div>
    </div>
  );
}
