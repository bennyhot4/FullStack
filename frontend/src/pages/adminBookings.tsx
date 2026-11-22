import { useState, type FormEvent } from "react";
import { asApiError, get, post, put } from "../api";
import { dayShort, t } from "../i18n";
import { addDays, fmtDate, money, settings, todayInBusinessTz } from "../store";
import type { BookingDto, OfficeDto, RecurringResult, UserDto } from "../types/api";
import { useAsync } from "../useAsync";
import { Empty, ErrorBox, Field, Icon, PageHeader, Skeletons, StatusBadge, errorText, openSheet, toast, type SheetApi } from "../ui";

const hh = (n: number) => `${String(n).padStart(2, "0")}:00`;

export default function AdminBookings() {
  const { state, reload } = useAsync(() => Promise.all([
    get<BookingDto[]>("/api/admin/bookings?limit=500"), get<OfficeDto[]>("/api/admin/offices"), get<UserDto[]>("/api/admin/users"),
  ]));
  const [bookings, offices, users] = state.status === "done" ? state.data : [null, [], []];

  function edit(b: BookingDto) {
    openSheet({
      title: t("editBooking"),
      body: ({ close }) => <EditBookingForm booking={b} offices={offices} onSaved={() => { close(); reload(); }} />,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form="edit-booking">{t("update")}</button>,
      ],
    });
  }

  function recurring() {
    openSheet({
      title: t("repeatedBooking"),
      body: (api) => <RecurringForm offices={offices} users={users} sheet={api} onCreated={reload} />,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form="recurring">{t("createBookings")}</button>,
      ],
    });
  }

  async function cancel(b: BookingDto, btn: HTMLButtonElement) {
    btn.disabled = true;
    try { await post(`/api/admin/bookings/${b.id}/cancel`); reload(); } catch (e) { btn.disabled = false; toast(errorText(e), "error"); }
  }

  return (
    <>
      <PageHeader title={t("manageBookings")} subtitle={bookings ? `${bookings.length} ${t("allBookings")}` : undefined} />
      <div className="container wide stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        <button className="btn block" type="button" onClick={recurring}><Icon name="repeat" />{t("repeatedBooking")}</button>
        <div className="grid-cards">
          {state.status === "loading" ? <Skeletons n={4} height={120} />
            : state.status === "error" ? <ErrorBox error={state.error} onRetry={reload} />
              : bookings?.length ? bookings.map((b) => (
                <article key={b.id} className="card pad stack" style={{ "--gap": "8px" }}>
                  <div className="row between"><h3 className="truncate">{b.officeName}</h3><StatusBadge status={b.status} /></div>
                  <div className="meta">
                    <span><Icon name="calendar" className="sm" />{fmtDate(b.date)}</span>
                    <span dir="ltr"><Icon name="clock" className="sm" />{`${b.startTime} – ${b.endTime} (${b.durationHours}h)`}</span>
                    <span><Icon name="user" className="sm" />{b.bookerName || "—"}</span>
                    <span className="bold" style={{ color: "var(--text)" }}>{money(b.totalPrice, b.currency)}</span>
                  </div>
                  {b.seriesId ? <span className="xs faint"><Icon name="repeat" className="sm" />{` ${t("repeatedBooking")}`}</span> : null}
                  <div className="row" style={{ justifyContent: "flex-end" }}>
                    <button className="btn outline sm" type="button" onClick={() => edit(b)}><Icon name="edit" className="sm" />{t("edit")}</button>
                    {b.status === "confirmed" ? (
                      <button className="btn danger-outline sm" type="button" onClick={(e) => cancel(b, e.currentTarget)}>
                        <Icon name="x" className="sm" />{t("cancelBookingBtn")}
                      </button>
                    ) : null}
                  </div>
                </article>
              )) : <Empty icon="calendar" text={t("noBookings")} />}
        </div>
      </div>
    </>
  );
}

function OfficeSelect({ offices, value, onChange, id }: { offices: OfficeDto[]; value: string; onChange: (v: string) => void; id?: string }) {
  return (
    <select id={id} className="select" required value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="" disabled>{t("selectOffice")}</option>
      {offices.map((o) => <option key={o.id} value={o.id}>{o.name}</option>)}
    </select>
  );
}

function HourSelect({ value, from, to, onChange, id }: { value: string; from: number; to: number; onChange: (v: string) => void; id?: string }) {
  return (
    <select id={id} className="select" value={value} onChange={(e) => onChange(e.target.value)}>
      {Array.from({ length: to - from + 1 }, (_, i) => hh(from + i)).map((x) => <option key={x} value={x}>{x}</option>)}
    </select>
  );
}

function EditBookingForm({ booking: b, offices, onSaved }: { booking: BookingDto; offices: OfficeDto[]; onSaved: () => void }) {
  const { openHour, closeHour } = settings().booking;
  const [officeId, setOfficeId] = useState(b.officeId);
  const [booker, setBooker] = useState(b.bookerName);
  const [date, setDate] = useState(b.date);
  const [start, setStart] = useState(b.startTime);
  const [end, setEnd] = useState(b.endTime);
  const [error, setError] = useState("");

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    try {
      await put(`/api/admin/bookings/${b.id}`, { officeId, bookerName: booker, date, startTime: start, endTime: end });
      onSaved();
    } catch (ex) { setError(asApiError(ex).code === "slot_taken" ? t("rangeHasBooked") : errorText(ex)); }
  };

  return (
    <form id="edit-booking" className="stack" onSubmit={submit}>
      <Field label={t("office")}><OfficeSelect offices={offices} value={officeId} onChange={setOfficeId} /></Field>
      <Field label={t("bookingName")}><input className="input" value={booker} onChange={(e) => setBooker(e.target.value)} /></Field>
      <Field label={t("date")}><input className="input" type="date" required value={date} onChange={(e) => setDate(e.target.value)} /></Field>
      <div className="row" style={{ "--gap": "10px" }}>
        <div className="grow"><Field label={t("startTime")}><HourSelect value={start} from={openHour} to={closeHour - 1} onChange={setStart} /></Field></div>
        <div className="grow"><Field label={t("endTime")}><HourSelect value={end} from={openHour + 1} to={closeHour} onChange={setEnd} /></Field></div>
      </div>
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}

function RecurringForm({ offices, users, sheet, onCreated }: { offices: OfficeDto[]; users: UserDto[]; sheet: SheetApi; onCreated: () => void }) {
  const { openHour, closeHour } = settings().booking;
  const today = todayInBusinessTz();
  const [officeId, setOfficeId] = useState("");
  const [userId, setUserId] = useState("");
  const [booker, setBooker] = useState("");
  const [start, setStart] = useState("08:00");
  const [end, setEnd] = useState("09:00");
  const [from, setFrom] = useState(today);
  const [to, setTo] = useState(addDays(today, 30));
  const [notes, setNotes] = useState("");
  const [days, setDays] = useState<number[]>([]);
  const [error, setError] = useState("");
  const [result, setResult] = useState<RecurringResult | null>(null);

  const toggleDay = (i: number) => setDays((d) => (d.includes(i) ? d.filter((x) => x !== i) : [...d, i]));

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    if (!days.length) { setError(t("days")); return; }
    try {
      const r = await post<RecurringResult>("/api/admin/bookings/recurring", {
        officeId, userId: userId || null, bookerName: booker, startTime: start, endTime: end,
        days: [...days].sort((a, b) => a - b), fromDate: from, toDate: to, notes,
      });
      setResult(r);
      sheet.hideActions();
      onCreated();
    } catch (ex) { setError(errorText(ex)); }
  };

  if (result) {
    return (
      <div className="empty">
        <div style={{ fontSize: "2.5rem" }}>✅</div>
        <p className="bold">{`${result.created} ${t("bookingsCreated")}`}</p>
        {result.skipped ? <p className="small muted">{`${result.skipped} ${t("slotsSkipped")}`}</p> : null}
        <button className="btn" type="button" onClick={sheet.close}>{t("done")}</button>
      </div>
    );
  }

  return (
    <form id="recurring" className="stack" onSubmit={submit}>
      <Field label={t("office")}><OfficeSelect offices={offices} value={officeId} onChange={setOfficeId} /></Field>
      <Field label={t("bookFor")}>
        <select className="select" value={userId} onChange={(e) => {
          setUserId(e.target.value);
          const u = users.find((x) => x.id === e.target.value);
          if (u) setBooker(u.fullName);
        }}>
          <option value="">{t("selectUser")}</option>
          {users.filter((u) => u.status !== "invited").map((u) => <option key={u.id} value={u.id}>{u.fullName || u.email}</option>)}
        </select>
      </Field>
      <Field label={t("bookingName")}><input className="input" placeholder={t("bookingName")} value={booker} onChange={(e) => setBooker(e.target.value)} /></Field>
      <div className="row" style={{ "--gap": "10px" }}>
        <div className="grow"><Field label={t("startTime")}><HourSelect value={start} from={openHour} to={closeHour - 1} onChange={setStart} /></Field></div>
        <div className="grow"><Field label={t("endTime")}><HourSelect value={end} from={openHour + 1} to={closeHour} onChange={setEnd} /></Field></div>
      </div>
      <div className="field">
        <span className="label">{t("days")}</span>
        <div className="days-picker" role="group" aria-label={t("days")}>
          {dayShort().map((d, i) => (
            <button key={d} className="chip" type="button" aria-pressed={days.includes(i)} onClick={() => toggleDay(i)}>{d}</button>
          ))}
        </div>
      </div>
      <div className="row" style={{ "--gap": "10px" }}>
        <div className="grow"><Field label={t("fromDate")}><input className="input" type="date" required value={from} onChange={(e) => setFrom(e.target.value)} /></Field></div>
        <div className="grow"><Field label={t("toDate")}><input className="input" type="date" required value={to} onChange={(e) => setTo(e.target.value)} /></Field></div>
      </div>
      <Field label={t("notesLabel")}><input className="input" value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}
