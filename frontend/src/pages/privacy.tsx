import { getLang, t } from "../i18n";
import { fmtShortDate, store } from "../store";
import { Markdown } from "../components/markdown";
import { en, he, updated, version } from "../content/privacy";
import { PageHeader } from "../ui";

// Public privacy policy (plan 007). Readable signed out (route has optionalAuth), like /TermsOfUse.
export default function Privacy() {
  const isHe = getLang() === "he";
  return (
    <>
      <PageHeader title={t("privacyTitle")} back={store.me ? "/" : "/login"} />
      <div className="container" style={{ paddingBlock: "16px" }}>
        <article className="card pad" dir={isHe ? "rtl" : "ltr"}>
          <p className="xs faint" style={{ marginBottom: "12px" }}>
            {`${t("version")} ${version} · ${t("privacyLastUpdated")} ${fmtShortDate(updated)}`}
          </p>
          <Markdown source={isHe ? he : en} />
        </article>
      </div>
    </>
  );
}
