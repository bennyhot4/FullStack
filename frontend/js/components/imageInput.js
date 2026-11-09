// Image picker: upload a file (stored by the API) or paste a URL; shows a preview.
import { upload } from "../api.js";
import { t } from "../i18n.js";
import { h, icon, toast } from "../ui.js";

export function imageInput({ value = "", onChange, label }) {
  let current = value;
  const preview = h("img", { class: "image-preview", alt: "", hidden: !current, src: current || undefined });
  const url = h("input", { class: "input", type: "url", inputmode: "url", placeholder: t("orPasteUrl"), value: current, dir: "ltr" });
  const status = h("span", { class: "xs muted" });
  const file = h("input", { type: "file", accept: "image/png,image/jpeg,image/webp,image/gif", class: "sr-only" });
  const pickBtn = h("button", { class: "btn outline sm", type: "button", onclick: () => file.click() }, icon("upload", "sm"), t("uploadImage"));

  const set = (v) => {
    current = v;
    url.value = v;
    preview.hidden = !v;
    if (v) preview.src = v;
    onChange?.(v);
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
