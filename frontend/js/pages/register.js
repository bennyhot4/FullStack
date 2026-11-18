import { put } from "../api.js";
import { t } from "../i18n.js";
import { loadMe, store } from "../store.js";
import { clear, field, h, icon } from "../ui.js";

export default function register(el, { navigate }) {
  const u = store.me.user;
  if (!store.me.needsProfile) return navigate("/", { replace: true });

  const first = h("input", { class: "input", required: true, autocomplete: "given-name", value: u.firstName || "", maxlength: 60 });
  const last = h("input", { class: "input", required: true, autocomplete: "family-name", value: u.lastName || "", maxlength: 60 });
  const email = h("input", { class: "input", type: "email", value: u.email, disabled: true });
  const phone = h("input", { class: "input", type: "tel", inputmode: "tel", autocomplete: "tel", required: true, placeholder: "050-1234567", dir: "ltr" });
  // Plan 008, Q4: built but hidden for now (.is-hidden = display: none). Remove the class to show it.
  const consent = h("input", { type: "checkbox", checked: !!u.marketingConsent });
  const consentField = h("div", { class: "is-hidden" }, h("label", { class: "switch" }, consent, t("marketingConsent")));
  const err = h("p", { class: "error-text", role: "alert" });
  const submit = h("button", { class: "btn lg block", type: "submit" }, t("save"));

  const form = h("form", {
    class: "card pad stack", style: { "--gap": "14px", padding: "24px 20px" },
    onsubmit: async (e) => {
      e.preventDefault();
      err.textContent = "";
      if (phone.value.replace(/\D/g, "").length < 9) { err.textContent = t("invalidPhone"); return; }
      submit.disabled = true;
      submit.textContent = t("saving");
      try {
        await put("/api/me", {
          firstName: first.value.trim(), lastName: last.value.trim(), phone: phone.value, marketingConsent: consent.checked,
        });
        await loadMe();
        navigate(store.me.needsTerms ? "/TermsOfUse" : "/", { replace: true });
      } catch (ex) {
        err.textContent = ex.code === "invalid_phone" ? t("invalidPhone") : (ex.message || t("loadingFailed"));
        submit.disabled = false;
        submit.textContent = t("save");
      }
    },
  },
  h("div", { class: "row", style: { "--gap": "12px" } },
    h("div", { class: "avatar" }, u.pictureUrl ? h("img", { src: u.pictureUrl, alt: "" }) : icon("user")),
    h("div", null, h("h1", null, t("profileSetupTitle")), h("p", { class: "small muted" }, t("profileSetupSubtitle")))),
  h("div", { class: "row", style: { "--gap": "10px", alignItems: "flex-start" } },
    h("div", { class: "grow" }, field(t("firstName"), first)),
    h("div", { class: "grow" }, field(t("lastName"), last))),
  field(t("emailAddress"), email),
  field(t("phoneNumber"), phone),
  consentField,
  err,
  submit);

  clear(el, h("div", { class: "fullscreen-center" }, h("div", { style: { width: "100%", maxWidth: "440px" } }, form)));
  first.value ? phone.focus() : first.focus();
}
