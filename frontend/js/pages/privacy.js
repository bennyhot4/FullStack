import { getLang, t } from "../i18n.js";
import { fmtShortDate, store } from "../store.js";
import { renderMarkdown } from "../components/markdown.js";
import { en, he, updated, version } from "../content/privacy.js";
import { clear, h, pageHeader } from "../ui.js";

// Public privacy policy (plan 007). Readable signed out (route has optionalAuth), like /TermsOfUse.
export default async function privacy(el) {
  const back = store.me ? "/" : "/login";
  const isHe = getLang() === "he";
  clear(el,
    pageHeader({ title: t("privacyTitle"), back }),
    h("div", { class: "container", style: { paddingBlock: "16px" } },
      h("article", { class: "card pad", dir: isHe ? "rtl" : "ltr" },
        h("p", { class: "xs faint", style: { marginBottom: "12px" } },
          `${t("version")} ${version} · ${t("privacyLastUpdated")} ${fmtShortDate(updated)}`),
        renderMarkdown(isHe ? he : en))));
}
