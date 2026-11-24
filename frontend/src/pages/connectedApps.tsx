import { del, get } from "../api";
import { t } from "../i18n";
import { Link } from "../router";
import { fmtShortDate, settings } from "../store";
import type { ConnectedApp } from "../types/api";
import { useAsync } from "../useAsync";
import { SCOPE_LABEL } from "../components/mcpScopes";
import { Empty, ErrorBox, Icon, PageHeader, Skeletons, confirmSheet, errorText, toast } from "../ui";

// Plan 011: the AI assistants the signed-in person connected, with what each may do. Disconnect ends access on the
// assistant's next call.
export default function ConnectedApps() {
  const { state, reload } = useAsync(() => get<ConnectedApp[]>("/api/me/connected-apps"));

  const disconnect = async (app: ConnectedApp) => {
    const ok = await confirmSheet({
      title: t("disconnect"), message: `${t("disconnectConfirm")} ${app.name}?`, confirmText: t("disconnect"), danger: true,
    });
    if (!ok) return;
    try {
      await del(`/api/me/connected-apps/${encodeURIComponent(app.clientId)}`);
      toast(t("disconnected"), "success");
      reload();
    } catch (e) { toast(errorText(e), "error"); }
  };

  let list;
  if (state.status === "loading") list = <Skeletons n={2} height={96} />;
  else if (state.status === "error") list = <ErrorBox error={state.error} onRetry={reload} />;
  else if (!state.data.length) list = <Empty icon="link" text={t("connectedAppsEmpty")} />;
  else {
    list = state.data.map((app) => (
      <article key={app.clientId} className="card pad stack connected-app" style={{ "--gap": "8px" }}>
        <div className="row between" style={{ "--gap": "8px" }}>
          <h2 className="row truncate" style={{ "--gap": "8px", minWidth: "0" }}><Icon name="sparkle" />{app.name}</h2>
          {app.verified ? <span className="badge success">{t("mcpConsentVerified")}</span> : null}
        </div>
        <ul className="small stack" style={{ "--gap": "2px", paddingInlineStart: "18px", margin: "0" }}>
          {app.scopes.map((s) => <li key={s}>{t(SCOPE_LABEL[s])}</li>)}
        </ul>
        <p className="xs muted">
          {`${t("connectedOn")} ${app.connectedAt ? fmtShortDate(app.connectedAt) : "-"} · ${t("lastUsed")} ${app.lastUsedAt ? fmtShortDate(app.lastUsedAt) : t("never")}`}
        </p>
        <button className="btn danger-outline" type="button" onClick={() => disconnect(app)}>
          <Icon name="x" className="sm" />{t("disconnect")}
        </button>
      </article>
    ));
  }

  return (
    <>
      <PageHeader title={t("connectedApps")} subtitle={t("connectedAppsNote")} back="/ConnectAssistant" />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "12px" }}>
        {list}
        {settings().mcp.available
          ? <Link className="btn outline block" href="/ConnectAssistant"><Icon name="plus" className="sm" />{t("connectAnother")}</Link>
          : null}
      </div>
    </>
  );
}
