// Image picker: upload a file (stored by the API) or paste a URL; shows a preview.
import { upload } from "../api.js";
import { t } from "../i18n.js";
import { h, icon, toast } from "../ui.js";

// Only http(s) image URLs (absolute, or relative to the site) may reach <img src>.
// Anything else (javascript:, data:, garbage) is rejected so typed text is never treated as markup or script.
export function safeImageUrl(v) {
  if (!v) return "";
  let u;
  try { u = new URL(v, location.href); } catch { return null; }
  return u.protocol === "https:" || u.protocol === "http:" ? u.href : null;
}

export function imageInput({ value = "", onChange, label }) {
  let current = value;
  const preview = h("img", { class: "image-preview", alt: "", hidden: !current, src: current || undefined });
  const url = h("input", { class: "input", type: "url", inputmode: "url", placeholder: t("orPasteUrl"), value: current, dir: "ltr" });
  const status = h("span", { class: "xs muted" });
  const file = h("input", { type: "file", accept: "image/png,image/jpeg,image/webp,image/gif", class: "sr-only" });
  const pickBtn = h("button", { class: "btn outline sm", type: "button", onclick: () => file.click() }, icon("upload", "sm"), t("uploadImage"));

  const set = (v) => {
    const safe = safeImageUrl(v);
    if (safe === null) { toast(t("invalidImageUrl"), "error"); url.value = current; return; }
    current = safe;
    url.value = safe;
    preview.hidden = !safe;
    if (safe) preview.src = safe;
    onChange?.(safe);
  };
  url.addEventListener("change", () => set(url.value.trim()));
  file.addEventListener("change", async () => {
    const f = file.files?.[0];
    if (!f) return;
    pickBtn.disabled = true;
    status.textContent = t("uploading");
    try { set(await upload(f)); } catch (e) { toast(e.message || t("loadingFailed"), "error"); }
    finally { pickBtn.disabled = false; status.textContent = ""; file.value = ""; }
  });

  const root = h("div", { class: "field" }, label ? h("span", { class: "label" }, label) : null, preview,
    h("div", { class: "row", style: { "--gap": "8px" } }, pickBtn, status), file, url);
  root.getValue = () => current;
  return root;
}
