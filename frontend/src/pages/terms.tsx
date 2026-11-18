import { useState } from "react";
import { asApiError, get, hasSession, post } from "../api";
import { getLang, t } from "../i18n";
import { Link, navigate } from "../router";
import { fmtShortDate, loadMe, store } from "../store";
import type { TermsDoc } from "../types/api";
import { useAsync } from "../useAsync";
import { Markdown } from "../components/markdown";
import { ErrorBox, Icon, PageHeader, Skeletons, toast } from "../ui";

export default function Terms() {
  const { state, reload } = useAsync(() => get<TermsDoc>("/api/terms/current"));
  const [accepting, setAccepting] = useState(false);
  const back = store.me ? "/" : "/login";
  const user = store.me?.user;
  const header = <PageHeader title={t("termsTitle")} subtitle={t("termsReadAndAccept")} back={back} />;

  if (state.status === "error") return <ErrorBox error={state.error} onRetry={reload} />;
  if (state.status === "loading") {
    return <>{header}<div className="container" style={{ paddingBlock: "16px" }}><Skeletons n={4} height={80} /></div></>;
  }
  const doc = state.data;

  const accept = async () => {
    setAccepting(true);
    try {
      await post("/api/me/accept-terms", { version: doc.version });
      await loadMe();
      navigate("/", { replace: true });
    } catch (e) {
      const err = asApiError(e);
      toast(err.code === "terms_outdated" ? t("loadingFailed") : (err.message || t("loadingFailed")), "error");
      setAccepting(false);
      if (err.code === "terms_outdated") reload();
    }
  };

  const footer = !hasSession() || !user ? null
    : user.termsAccepted
      ? (
        <div className="action-bar row" style={{ "--gap": "10px", color: "var(--success-fg)" }}>
          <Icon name="checkCircle" />
          <span className="bold">{`${t("termsSignedOn")} ${user.termsAcceptedAt ? fmtShortDate(user.termsAcceptedAt) : ""}`}</span>
        </div>
      )
      : (
        <div className="action-bar">
          <button className="btn lg block" type="button" disabled={accepting} onClick={accept}>
            {accepting ? t("termsAccepting") : <><Icon name="check" />{t("termsAccept")}</>}
          </button>
        </div>
      );

  const isHe = getLang() === "he";
  return (
    <>
      {header}
      <div className="container" style={{ paddingBlock: "16px" }}>
        <article className="card pad" dir={isHe ? "rtl" : "ltr"}>
          <p className="xs faint" style={{ marginBottom: "12px" }}>{`${t("version")} ${doc.version} · ${fmtShortDate(doc.publishedAt)}`}</p>
          <Markdown source={isHe ? doc.contentHe : doc.contentEn} />
          <p className="small" style={{ marginTop: "16px" }}><Link href="/PrivacyPolicy">{t("privacyViewPolicy")}</Link></p>
        </article>
        {footer}
      </div>
    </>
  );
}
