import { useState, type FormEvent } from "react";
import { asApiError, del, get, post, put } from "../api";
import { t } from "../i18n";
import type { ClientDto, DistributionList, DistributionListSummary } from "../types/api";
import { useAsync } from "../useAsync";
import { clientName } from "../components/clients";
import { Empty, ErrorBox, Field, Icon, PageHeader, Skeletons, confirmSheet, errorText, openSheet, toast } from "../ui";

const FORM_ID = "distribution-list-form";

// The signed-in user's private distribution lists (plan 008). The API is open to every user; for now the
// route is admin-only and the entry button on /MyClients is shown to admins only.
export default function DistributionLists() {
  const { state, reload } = useAsync(() => Promise.all([
    get<DistributionListSummary[]>("/api/distribution-lists"),
    get<ClientDto[]>("/api/clients"),
  ]));
  const clients = state.status === "done" ? state.data[1] : [];

  async function openExisting(summary: DistributionListSummary) {
    try {
      openListSheet(await get<DistributionList>(`/api/distribution-lists/${summary.distributionListId}`));
    } catch (ex) {
      toast(errorText(ex), "error");
      if (asApiError(ex).status === 404) reload();
    }
  }

  /** Add/edit sheet: name, description and a filterable checkbox list of the user's clients. */
  function openListSheet(list: DistributionList | null) {
    async function remove(l: DistributionList) {
      const ok = await confirmSheet({
        title: t("deleteDistributionList"), message: `${t("deleteDistributionListConfirm")} ${l.name}?`,
        confirmText: t("delete"), danger: true,
      });
      if (!ok) return;
      try {
        await del(`/api/distribution-lists/${l.distributionListId}`);
        toast(t("distributionListDeleted"), "success");
        sheet.close();
        reload();
      } catch (ex) { toast(errorText(ex), "error"); }
    }

    const sheet = openSheet({
      title: list ? t("editDistributionList") : t("newDistributionList"),
      body: ({ close }) => <ListForm list={list} clients={clients} onSaved={() => { close(); reload(); }} />,
      actions: (close) => [
        list ? <button key="del" className="btn danger-outline" type="button" onClick={() => remove(list)}>{t("delete")}</button> : null,
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form={FORM_ID}>{t("saveClient")}</button>,
      ],
    });
  }

  let body;
  if (state.status === "loading") body = <Skeletons n={3} height={56} />;
  else if (state.status === "error") body = <ErrorBox error={state.error} onRetry={reload} />;
  else if (!state.data[0].length) {
    body = (
      <Empty icon="list" text={t("noDistributionLists")}>
        <button className="btn" type="button" onClick={() => openListSheet(null)}><Icon name="plus" className="sm" />{t("newDistributionList")}</button>
      </Empty>
    );
  } else {
    body = state.data[0].map((l) => (
      <button key={l.distributionListId} className="card pad row between" type="button"
        style={{ width: "100%", border: "0", cursor: "pointer", textAlign: "start", minHeight: "56px", "--gap": "12px" }}
        onClick={() => openExisting(l)}>
        <span className="row grow" style={{ "--gap": "12px" }}>
          <Icon name="list" />
          <span className="stack grow" style={{ "--gap": "2px", minWidth: "0" }}>
            <span className="bold truncate">{l.name}</span>
            <span className="small muted truncate">{`${l.clientCount} ${t("clientsCount")}`}</span>
          </span>
        </span>
        <Icon name="chevronRight" className="sm flip-rtl" />
      </button>
    ));
  }

  return (
    <>
      <PageHeader title={t("distributionLists")} subtitle={t("distributionListsNote")} back="/MyClients" />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "12px" }}>
        <button className="btn block" type="button" onClick={() => openListSheet(null)}><Icon name="plus" />{t("newDistributionList")}</button>
        <div className="stack" style={{ "--gap": "8px" }}>{body}</div>
      </div>
    </>
  );
}

function ListForm({ list, clients, onSaved }: { list: DistributionList | null; clients: ClientDto[]; onSaved: () => void }) {
  const [name, setName] = useState(list?.name ?? "");
  const [description, setDescription] = useState(list?.description ?? "");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState(() => new Set(list?.clientIds ?? []));
  const [error, setError] = useState("");

  const q = search.trim().toLowerCase();
  const shown = clients.filter((c) => !q || [clientName(c), ...c.phones, ...c.emails].some((v) => v.toLowerCase().includes(q)));

  const toggle = (id: string, on: boolean) => setSelected((prev) => {
    const next = new Set(prev);
    if (on) next.add(id); else next.delete(id);
    return next;
  });
  const setShown = (on: boolean) => setSelected((prev) => {
    const next = new Set(prev);
    for (const c of shown) { if (on) next.add(c.clientId); else next.delete(c.clientId); }
    return next;
  });

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    // Keep only ids of clients that still exist, in client-list order.
    const clientIds = clients.map((c) => c.clientId).filter((id) => selected.has(id));
    const payload = { name: name.trim(), description: description.trim(), clientIds };
    try {
      if (list) await put(`/api/distribution-lists/${list.distributionListId}`, payload);
      else await post("/api/distribution-lists", payload);
      toast(t("distributionListSaved"), "success");
      onSaved();
    } catch (ex) { setError(errorText(ex)); }
  };

  return (
    <form id={FORM_ID} className="stack" style={{ "--gap": "14px" }} onSubmit={submit}>
      <Field label={t("listName")}>
        <input className="input" required maxLength={60} autoComplete="off" autoFocus={!list} value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <Field label={t("listDescription")}>
        <textarea className="textarea" maxLength={300} rows={2} value={description} onChange={(e) => setDescription(e.target.value)} />
      </Field>
      <div className="stack" style={{ "--gap": "8px" }}>
        <div className="row between" style={{ "--gap": "8px" }}>
          <span className="bold">{t("listMembers")}</span>
          <span className="small muted">{`${selected.size} ${t("selectedCount")}`}</span>
        </div>
        <input className="input" type="search" placeholder={t("filterClients")} aria-label={t("filterClients")}
          value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="row" style={{ "--gap": "8px", flexWrap: "wrap" }}>
          <button className="btn ghost sm" type="button" onClick={() => setShown(true)}>{t("selectAllShown")}</button>
          <button className="btn ghost sm" type="button" onClick={() => setShown(false)}>{t("clearSelection")}</button>
        </div>
        <div className="stack" style={{ "--gap": "0" }}>
          {clients.length && shown.length ? shown.map((c) => (
            <label key={c.clientId} className="check-row">
              <input type="checkbox" checked={selected.has(c.clientId)} onChange={(e) => toggle(c.clientId, e.target.checked)} />
              <span className="stack grow" style={{ "--gap": "0", minWidth: "0" }}>
                <span className="truncate">{clientName(c)}</span>
                <span className="xs muted truncate" dir="ltr" style={{ textAlign: "start" }}>{c.phones[0] || ""}</span>
              </span>
            </label>
          )) : <p className="small muted">{t("noClients")}</p>}
        </div>
      </div>
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}
