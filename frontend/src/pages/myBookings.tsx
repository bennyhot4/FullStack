import { useEffect, useState } from "react";
import { get, post, put } from "../api";
import { t } from "../i18n";
import { Link } from "../router";
import { fmtDate } from "../store";
import type { ClientDto, MyBookingDto, MyBookings as MyBookingsData } from "../types/api";
import { useAsync } from "../useAsync";
import { BookingCard } from "../components/bookingCard";
import { ClientSelect, clientName } from "../components/clients";
import { Empty, ErrorBox, Field, Icon, PageHeader, Skeletons, confirmSheet, errorText, openSheet, toast } from "../ui";
import type { PageProps } from "../app";

type Tab = "upcoming" | "past";

export default function MyBookings({ params }: PageProps) {
  const [tab, setTab] = useState<Tab>("upcoming");
  // Client filter, kept in the URL (?clientId=) so /MyClients can link here already filtered.
  const [filter, setFilterState] = useState(params.get("clientId") || "");
  const { state, reload } = useAsync(() => Promise.all([
    get<MyBookingsData>("/api/bookings/mine"),
    get<ClientDto[]>("/api/clients").catch(() => [] as ClientDto[]),
  ]));

  const [data, clients] = state.status === "done" ? state.data : [{ upcoming: [], past: [] } as MyBookingsData, [] as ClientDto[]];
  // An unknown or deleted client in the URL falls back to "All clients".
  const active = clients.some((c) => c.clientId === filter) ? filter : "";
  const loaded = state.status === "done";

  useEffect(() => {
    if (loaded && filter && !active) history.replaceState(history.state, "", "/MyBookings");
  }, [loaded, filter, active]);

  const setFilter = (v: string) => {
    setFilterState(v);
    history.replaceState(history.state, "", v ? `/MyBookings?clientId=${encodeURIComponent(v)}` : "/MyBookings");
  };

  const shown = (k: Tab) => (active ? data[k].filter((b) => b.clientId === active) : data[k]);

  async function cancel(b: MyBookingDto) {
    const ok = await confirmSheet({
      title: t("cancelBooking"),
      message: `${t("areYouSure")} ${b.officeName}? ${t("cannotBeUndone")}`,
      confirmText: t("yesCancel"), cancelText: t("keepBooking"), danger: true,
    });
    if (!ok) return;
    try { await post(`/api/bookings/${b.id}/cancel`); reload(); } catch (e) { toast(errorText(e), "error"); }
  }

  // Set, change or remove (choose "No client") the booking's client.
  function pickClient(b: MyBookingDto) {
    const formId = "booking-client";
    openSheet({
      title: t("client"),
      body: ({ close }) => <BookingClientForm id={formId} booking={b} clients={clients} onSaved={() => { close(); reload(); }} />,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form={formId}>{t("saveClient")}</button>,
      ],
    });
  }

  const items = shown(tab);
  return (
    <>
      <PageHeader title={t("myBookings")} />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        <div>
          {clients.length ? (
            <Field label={t("filterByClient")}>
              <select className="select" value={active} onChange={(e) => setFilter(e.target.value)}>
                <option value="">{t("allClients")}</option>
                {clients.map((c) => <option key={c.clientId} value={c.clientId}>{clientName(c)}</option>)}
              </select>
            </Field>
          ) : null}
        </div>
        <div className="tabs" role="tablist">
          {(["upcoming", "past"] as const).map((k) => (
            <button key={k} className="tab" role="tab" type="button" aria-selected={tab === k} onClick={() => setTab(k)}>
              {`${t(k)} (${shown(k).length})`}
            </button>
          ))}
        </div>
        <div className="stack">
          {state.status === "loading" ? <Skeletons n={3} height={110} />
            : state.status === "error" ? <ErrorBox error={state.error} onRetry={reload} />
              : !items.length ? (
                <Empty icon="calendar" text={t(tab === "upcoming" ? "noUpcomingBookings" : "noPastBookings")}>
                  <Link className="btn" href="/">{t("bookAnOffice")}</Link>
                </Empty>
              )
                : items.map((b) => (
                  <BookingCard key={b.id} booking={b} onCancel={tab === "upcoming" ? () => cancel(b) : undefined}
                    extra={(
                      <div className="row" style={{ justifyContent: "flex-end" }}>
                        <button className="btn outline sm" type="button" onClick={() => pickClient(b)}>
                          <Icon name="client" className="sm" />{t(b.clientId ? "changeClient" : "chooseClient")}
                        </button>
                      </div>
                    )} />
                ))}
        </div>
        {/* Plan 011: AI assistants connected to this account. */}
        <Link className="small center row" style={{ justifyContent: "center", "--gap": "6px", minHeight: "44px" }} href="/ConnectedApps">
          <Icon name="link" className="sm" />{t("connectedApps")}
        </Link>
      </div>
    </>
  );
}

function BookingClientForm({ id, booking: b, clients, onSaved }: { id: string; booking: MyBookingDto; clients: ClientDto[]; onSaved: () => void }) {
  const [value, setValue] = useState(clients.some((c) => c.clientId === b.clientId) ? b.clientId ?? "" : "");
  return (
    <form id={id} className="stack" onSubmit={async (e) => {
      e.preventDefault();
      try {
        await put(`/api/bookings/${b.id}/client`, { clientId: value || null });
        onSaved();
      } catch (ex) { toast(errorText(ex), "error"); }
    }}>
      <p className="small muted">{`${b.officeName} · ${fmtDate(b.date)}`}</p>
      <Field label={t("client")}><ClientSelect clients={clients} value={value} onChange={setValue} /></Field>
    </form>
  );
}
