import { useState } from "react";
import { get } from "../api";
import { t } from "../i18n";
import { fmtShortDate, money } from "../store";
import type { Analytics } from "../types/api";
import { useAsync } from "../useAsync";
import { Empty, ErrorBox, PageHeader, Skeletons } from "../ui";

type Period = "week" | "month";

const Stat = ({ value, label }: { value: string | number; label: string }) => (
  <div className="stat"><div className="value">{value}</div><div className="label">{label}</div></div>
);

export default function AdminAnalytics() {
  const [period, setPeriod] = useState<Period>("week");
  const { state, reload } = useAsync(() => get<Analytics>(`/api/admin/analytics?period=${period}`), period);
  // Keep the last known range in the header while the next period loads.
  const [range, setRange] = useState("");
  const a = state.status === "done" ? state.data : null;
  const shownRange = a ? `${fmtShortDate(a.from)} – ${fmtShortDate(a.to)}` : range;
  if (a && shownRange !== range) setRange(shownRange);

  return (
    <>
      <PageHeader title={t("usageAnalytics")} subtitle={shownRange} />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        <div className="tabs" role="tablist">
          {([["week", t("thisWeek")], ["month", t("thisMonth")]] as const).map(([k, label]) => (
            <button key={k} className="tab" role="tab" type="button" aria-selected={period === k} onClick={() => setPeriod(k)}>{label}</button>
          ))}
        </div>
        <div className="stack" style={{ "--gap": "16px" }}>
          {state.status === "loading" ? <><Skeletons n={1} height={90} /><Skeletons n={3} height={70} /></>
            : state.status === "error" ? <ErrorBox error={state.error} onRetry={reload} />
              : (
                <>
                  <div className="stats">
                    <Stat value={state.data.totalBookings} label={t("totalBookings")} />
                    <Stat value={state.data.totalHours} label={t("totalHours")} />
                    <Stat value={money(state.data.revenue)} label={t("revenue")} />
                  </div>
                  <section className="stack">
                    <h2>{t("usageByUser")}</h2>
                    {state.data.byUser.length ? state.data.byUser.map((u, i) => (
                      <article key={u.userId} className="card pad list-item">
                        <div className="avatar bold">{i + 1}</div>
                        <div className="grow">
                          <h3 className="truncate">{u.fullName || u.email}</h3>
                          <p className="xs muted truncate" dir="ltr" style={{ textAlign: "start" }}>{u.email}</p>
                          <div className="meta" style={{ marginTop: "6px" }}>
                            <span>{`${t("bookings")}: `}<b>{u.bookings}</b></span>
                            <span>{`${t("hours")}: `}<b>{u.hours}</b></span>
                            <span>{`${t("spent")}: `}<b>{money(u.spent)}</b></span>
                          </div>
                        </div>
                      </article>
                    )) : <Empty icon="chart" text={t("noBookingsInPeriod")} />}
                  </section>
                </>
              )}
        </div>
      </div>
    </>
  );
}
