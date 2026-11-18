import { get, put } from "../api.js";
import { t } from "../i18n.js";
import { applyBranding, loadSettings, store } from "../store.js";
import { imageInput } from "../components/imageInput.js";
import { clear, confirmSheet, field, h, icon, pageHeader, toast } from "../ui.js";

export default async function siteSettings(el) {
  const s = store.settings;
  const b = { ...s.branding };

  // Main banner image (home_banner_image)
  const banner = imageInput({ value: s.homeBannerImage, label: t("mainBannerImage") });
  const saveBanner = h("button", { class: "btn", type: "button" }, t("saveSettings"));
  saveBanner.addEventListener("click", () => save(saveBanner, () => put("/api/admin/settings/home-banner-image", { value: banner.getValue() })));

  // Branding (live preview while editing)
  const title = h("input", { class: "input", value: b.title, placeholder: "Office Booking" });
  const description = h("input", { class: "input", value: b.description, placeholder: "Find and book your perfect workspace" });
  const logo = imageInput({ value: b.logo, label: t("logo") });
  const color = (key, label) => {
    const picker = h("input", { type: "color", value: b[key], "aria-label": label });
    const text = h("input", { class: "input", value: b[key], dir: "ltr", maxlength: 7, style: { maxWidth: "130px" } });
    const sync = (v) => { if (/^#[0-9a-f]{6}$/i.test(v)) { b[key] = v; picker.value = v; text.value = v; applyBranding(b); } };
    picker.addEventListener("input", () => sync(picker.value));
    text.addEventListener("change", () => sync(text.value.trim()));
    return h("div", { class: "field" }, h("span", { class: "label" }, label), h("div", { class: "color-field" }, picker, text));
  };
  const saveBrand = h("button", { class: "btn", type: "button" }, t("saveBranding"));
  saveBrand.addEventListener("click", () => save(saveBrand, () => put("/api/admin/settings/branding", {
    title: title.value.trim(), description: description.value.trim(), logo: logo.getValue(),
    buttonsColor: b.buttonsColor, backgroundColor: b.backgroundColor, pagesColor: b.pagesColor,
  })));

  // Terms of use – publishing a new version makes everyone sign again.
  const termsHost = h("div", { class: "stack" });

  clear(el,
    pageHeader({ title: t("siteSettings"), back: "/AdminOffices" }),
    h("div", { class: "container stack", style: { paddingBlock: "16px", "--gap": "16px" } },
      h("section", { class: "card pad stack" }, h("h2", { class: "row" }, icon("image"), t("mainBannerImage")), banner, saveBanner),
      h("section", { class: "card pad stack" }, h("h2", { class: "row" }, icon("palette"), t("branding")),
        field(t("pageTitle"), title), field(t("pageDescription"), description), logo,
        color("buttonsColor", t("buttonsColor")), color("backgroundColor", t("backgroundColor")), color("pagesColor", t("pagesColor")),
        saveBrand),
      h("section", { class: "card pad stack" }, h("h2", { class: "row" }, icon("file"), t("editTerms")), termsHost)));

  async function save(btn, fn) {
    btn.disabled = true;
    try { await fn(); await loadSettings(); toast(t("saved"), "success"); }
    catch (e) { toast(e.message, "error"); }
    finally { btn.disabled = false; }
  }

  try {
    const doc = await get("/api/terms/current");
    const he = h("textarea", { class: "textarea", rows: 10, dir: "rtl" }, doc.contentHe);
    const en = h("textarea", { class: "textarea", rows: 10, dir: "ltr" }, doc.contentEn);
    const publish = h("button", { class: "btn outline", type: "button" }, t("publishNewVersion"));
    publish.addEventListener("click", async () => {
      if (!await confirmSheet({ title: t("publishNewVersion"), message: t("publishTermsConfirm"), confirmText: t("publishNewVersion") })) return;
      await save(publish, () => put("/api/admin/terms", { contentHe: he.value, contentEn: en.value }));
    });
    clear(termsHost, h("p", { class: "xs muted" }, `${t("version")} ${doc.version}`),
      field(t("termsHebrew"), he), field(t("termsEnglish"), en), publish);
  } catch (e) {
    clear(termsHost, h("p", { class: "error-text" }, e.message));
  }
}
