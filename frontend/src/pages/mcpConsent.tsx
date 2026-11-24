import { useState } from "react";
import { asApiError, get, post, signOut } from "../api";
import { t } from "../i18n";
import { navigate } from "../router";
import { setMe } from "../store";
import type { ConsentRequest, DecisionResponse, McpScope } from "../types/api";
import { useAsync } from "../useAsync";
import { SCOPE_LABEL, SCOPE_WARNING } from "../components/mcpScopes";
import { Empty, ErrorBox, Icon, PageHeader, Skeletons, errorText } from "../ui";
import type { PageProps } from "../app";

// Plan 011: an AI assistant asks for access. The API sent the browser here with ?request=<id>; the person sees who
// asks and for what, ticks permissions, and Allow / Deny sends them back to the assistant through the API.
export default function McpConsent({ params }: PageProps) {
  const id = params.get("request") ?? "";
  const { state, reload } = useAsync(() => get<ConsentRequest>(`/oauth/authorize/request/${encodeURIComponent(id)}`), id);

  // Defence in depth against clickjacking: never show the Allow button inside a frame, whatever the headers.
  const framed = window.top !== window.self;

  let body;
  if (framed) body = <Empty icon="alert" text={t("consent_not_found")} />;
  else if (!id) body = <Empty icon="alert" text={t("consent_not_found")} />;
  else if (state.status === "loading") body = <Skeletons n={2} height={120} />;
  else if (state.status === "error") {
    body = asApiError(state.error).code === "consent_not_found"
      ? <Empty icon="alert" text={t("consent_not_found")} />
      : <ErrorBox error={state.error} onRetry={reload} />;
  } else body = <ConsentForm request={state.data} />;

  return (
    <>
      <PageHeader title={t("mcpConsentTitle")} />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "16px", maxWidth: "560px" }}>{body}</div>
    </>
  );
}

function ConsentForm({ request }: { request: ConsentRequest }) {
  const [chosen, setChosen] = useState<Set<McpScope>>(
    () => new Set(request.scopes.filter((s) => s.defaultOn).map((s) => s.scope)));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [needsSignIn, setNeedsSignIn] = useState(false);
  const sensitiveChosen = request.scopes.some((s) => s.sensitive && chosen.has(s.scope));

  const toggle = (scope: McpScope, on: boolean) =>
    setChosen((prev) => {
      const next = new Set(prev);
      if (on) next.add(scope);
      else next.delete(scope);
      return next;
    });

  const decide = async (approve: boolean) => {
    if (approve && chosen.size === 0) { setError(t("mcpChooseOne")); return; }
    setBusy(true);
    setError("");
    try {
      const res = await post<DecisionResponse>("/oauth/authorize/decision",
        { requestId: request.requestId, approve, scopes: [...chosen] });
      // A full navigation: the API (approve) or the assistant (deny) takes it from here.
      window.location.assign(res.redirectUrl);
    } catch (e) {
      if (asApiError(e).code === "reauth_required") setNeedsSignIn(true);
      setError(errorText(e));
      setBusy(false);
    }
  };

  // Signing in again keeps the request: the login page brings the person back here.
  const signInAgain = async () => {
    const back = `/McpConsent?request=${encodeURIComponent(request.requestId)}`;
    await signOut();
    setMe(null);
    navigate(`/login?from=${encodeURIComponent(back)}`);
  };

  return (
    <>
      <section className="card pad stack" style={{ "--gap": "10px" }}>
        <div className="row" style={{ "--gap": "12px" }}>
          <Icon name="sparkle" className="lg" />
          <div className="stack grow" style={{ "--gap": "4px", minWidth: "0" }}>
            <h2 className="truncate">{request.clientName}</h2>
            <p className="small muted">{t("mcpConsentIntro")}</p>
          </div>
        </div>
        {request.verified
          ? <span className="badge success" style={{ alignSelf: "flex-start" }}><Icon name="checkCircle" className="sm" />{t("mcpConsentVerified")}</span>
          : <p className="small" role="note" style={{ color: "var(--warning-fg)" }}><Icon name="alert" className="sm" /> {t("mcpConsentUnverified")}</p>}
        <p className="xs muted">{`${t("mcpConsentRedirect")} `}<span dir="ltr" className="bold">{request.redirectHost}</span></p>
      </section>

      <section className="card pad stack" style={{ "--gap": "4px" }}>
        <h2 className="row" style={{ marginBottom: "6px" }}><Icon name="shield" />{t("mcpConsentChoose")}</h2>
        {request.scopes.map((s) => {
          const warning = SCOPE_WARNING[s.scope];
          return (
            <label key={s.scope} className="check-row" style={{ alignItems: "flex-start" }}>
              <input type="checkbox" checked={chosen.has(s.scope)} disabled={busy}
                onChange={(e) => toggle(s.scope, e.target.checked)} />
              <span className="stack" style={{ "--gap": "2px" }}>
                <span>{t(SCOPE_LABEL[s.scope])}</span>
                {warning && chosen.has(s.scope) ? <span className="xs" style={{ color: "var(--danger-fg)" }}>{t(warning)}</span> : null}
              </span>
            </label>
          );
        })}
      </section>

      <p className="small muted">{t("mcpConsentProviderNotice")}</p>

      {needsSignIn || (request.needsFreshSignIn && sensitiveChosen) ? (
        <div className="card pad stack" role="note" style={{ "--gap": "10px" }}>
          <p className="small">{t("mcpFreshSignIn")}</p>
          <button className="btn outline" type="button" onClick={signInAgain}>{t("mcpSignInAgain")}</button>
        </div>
      ) : null}

      <p className="error-text" role="alert">{error}</p>
      <div className="row" style={{ "--gap": "8px" }}>
        <button className="btn outline grow" type="button" disabled={busy} onClick={() => decide(false)}>{t("mcpDeny")}</button>
        <button className="btn grow" type="button" disabled={busy} onClick={() => decide(true)}>{t("mcpAllow")}</button>
      </div>
    </>
  );
}
