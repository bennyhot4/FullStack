import { useState } from "react";
import { get } from "../api";
import { t } from "../i18n";
import { Link } from "../router";
import { isAdmin } from "../store";
import type { ClientDto } from "../types/api";
import { useAsync } from "../useAsync";
import { clientName, openClientSheet } from "../components/clients";
import { Empty, ErrorBox, Icon, PageHeader, Skeletons } from "../ui";

const matches = (c: ClientDto, q: string) => !q || [clientName(c), ...c.phones, ...c.emails].some((v) => v.toLowerCase().includes(q))
  // "050-123..." should find "+97250123..."
  || c.phones.some((p) => q.replace(/\D/g, "").length > 2 && p.replace(/^\+972/, "0").includes(q.replace(/\D/g, "")));

// The signed-in user's private client list. Only its owner can see it (the API enforces this).
export default function MyClients() {
  const [search, setSearch] = useState("");
  const { state, reload } = useAsync(() => get<ClientDto[]>("/api/clients"));
  const add = () => openClientSheet({ onSaved: reload });

  let list;
  if (state.status === "loading") list = <Skeletons n={3} height={56} />;
  else if (state.status === "error") list = <ErrorBox error={state.error} onRetry={reload} />;
  else if (!state.data.length) {
    list = (
      <Empty icon="client" text={t("noClients")}>
        <div className="stack center" style={{ "--gap": "10px" }}>
          <p className="small muted">{t("clientsPrivateNote")}</p>
          <button className="btn" type="button" onClick={add}><Icon name="plus" className="sm" />{t("addClient")}</button>
        </div>
      </Empty>
    );
  } else {
    const q = search.trim().toLowerCase();
    const shown = state.data.filter((c) => matches(c, q));
    list = shown.length
      ? shown.map((c) => (
        <button key={c.clientId} className="card pad row between" type="button"
          style={{ width: "100%", border: "0", cursor: "pointer", textAlign: "start", minHeight: "56px", "--gap": "12px" }}
          onClick={() => openClientSheet({ client: c, onSaved: reload, onDeleted: reload, bookingsLink: true })}>
          <span className="row grow" style={{ "--gap": "12px" }}>
            <Icon name="client" />
            <span className="stack grow" style={{ "--gap": "2px", minWidth: "0" }}>
              <span className="bold truncate">{clientName(c)}</span>
              <span className="small muted truncate" dir="ltr" style={{ textAlign: "start" }}>{c.phones[0] || ""}</span>
            </span>
          </span>
          <Icon name="chevronRight" className="sm flip-rtl" />
        </button>
      ))
      : <p className="small muted center">{t("noClients")}</p>;
  }

  return (
    <>
      <PageHeader title={t("myClients")} subtitle={t("clientsPrivateNote")} />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "12px" }}>
        {/* Distribution lists are shown to admins only at this stage (plan 008, Q6). In the body, not the
            header, so the page title is not squeezed at 360px. */}
        {isAdmin() ? (
          <div className="row" style={{ "--gap": "8px" }}>
            <div style={{ flex: "1 1 0", minWidth: "0" }}>
              <button className="btn block" type="button" onClick={add}><Icon name="plus" />{t("addClient")}</button>
            </div>
            <Link className="btn outline" style={{ flex: "1 1 0", minWidth: "0" }} href="/DistributionLists">
              <Icon name="list" className="sm" />{t("distributionLists")}
            </Link>
          </div>
        ) : <button className="btn block" type="button" onClick={add}><Icon name="plus" />{t("addClient")}</button>}
        <input className="input" type="search" placeholder={t("filterClients")} aria-label={t("filterClients")}
          value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="stack" style={{ "--gap": "8px" }}>{list}</div>
      </div>
    </>
  );
}
