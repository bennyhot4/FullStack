import { get, hasSession, post } from "../api.js";
import { getLang, t } from "../i18n.js";
import { fmtShortDate, loadMe, store } from "../store.js";
import { renderMarkdown } from "../components/markdown.js";
import { clear, errorBox, h, icon, pageHeader, skeletons, toast } from "../ui.js";

export default async function terms(el, { navigate }) {
  const back = store.me ? "/" : "/login";
  clear(el, pageHeader({ title: t("termsTitle"), subtitle: t("termsReadAndAccept"), back }),
    h("div", { class: "container", style: { paddingBlock: "16px" } }, skeletons(4, 80)));

  let doc;
  try { doc = await get("/api/terms/current"); } catch (e) { return clear(el, errorBox(e, () => terms(el, { navigate }))); }

  const user = store.me?.user;
  const signed = user?.termsAccepted;
  const btn = h("button", { class: "btn lg block", type: "button" }, icon("check"), t("termsAccept"));
  btn.addEventListener("click", async () => {
    btn.disabled = true;
    btn.textContent = t("termsAccepting");
    try {
      await post("/api/me/accept-terms", { version: doc.version });
      await loadMe();
      navigate("/", { replace: true });
    } catch (e) {
      toast(e.code === "terms_outdated" ? t("loadingFailed") : (e.message || t("loadingFailed")), "error");
      if (e.code === "terms_outdated") return terms(el, { navigate });
      btn.disabled = false;
      btn.replaceChildren(icon("check"), t("termsAccept"));
    }
  });

  const footer = !hasSession() || !user ? null
    : signed
      ? h("div", { class: "action-bar row", style: { "--gap": "10px", color: "var(--success-fg)" } }, icon("checkCircle"),
        h("span", { class: "bold" }, `${t("termsSignedOn")} ${fmtShortDate(user.termsAcceptedAt)}`))
      : h("div", { class: "action-bar" }, btn);

  clear(el,
    pageHeader({ title: t("termsTitle"), subtitle: t("termsReadAndAccept"), back }),
    h("div", { class: "container", style: { paddingBlock: "16px" } },
      h("article", { class: "card pad", dir: getLang() === "he" ? "rtl" : "ltr" },
        h("p", { class: "xs faint", style: { marginBottom: "12px" } }, `${t("version")} ${doc.version} · ${fmtShortDate(doc.publishedAt)}`),
        renderMarkdown(getLang() === "he" ? doc.contentHe : doc.contentEn)),
      footer));
}
