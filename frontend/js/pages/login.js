import { api, setSession } from "../api.js";
import { t } from "../i18n.js";
import { languageButton } from "../app.js";
import { store } from "../store.js";
import { clear, field, h, icon } from "../ui.js";

let gisLoaded = null;
function loadGoogleScript() {
  gisLoaded ??= new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = "https://accounts.google.com/gsi/client";
    s.async = true;
    s.onload = resolve;
    s.onerror = () => { gisLoaded = null; reject(new Error("Google sign-in could not be loaded")); };
    document.head.append(s);
  });
  return gisLoaded;
}

export default async function login(el, { navigate, params }) {
  const { branding, auth } = store.settings;
  const title = branding.title || "HourOffice";
  const errorEl = h("p", { class: "error-text center", role: "alert" });

  const finish = async (promise) => {
    errorEl.textContent = "";
    try {
      setSession(await promise);
      store.me = null;
      const from = params.get("from");
      navigate(from && from.startsWith("/") && !from.startsWith("//") ? from : "/", { replace: true });
    } catch (e) {
      if (e.code === "user_not_registered") return clear(el, restricted());
      errorEl.textContent = e.message || t("loadingFailed");
    }
  };

  const googleHost = h("div", { class: "row", style: { justifyContent: "center", minHeight: "44px" } });
  const card = h("div", { class: "card pad stack", style: { "--gap": "16px", padding: "28px 20px" } },
    branding.logo ? h("img", { class: "logo", src: branding.logo, alt: "" }) : h("div", { class: "logo", "aria-hidden": "true" }, title[0]),
    h("div", { class: "center stack", style: { "--gap": "4px" } },
      h("h1", null, `${t("welcomeTo")}${t("welcomeTo").endsWith("ל") ? "" : " "}${title}`),
      h("p", { class: "muted" }, branding.description || t("signInToContinue"))),
    auth.googleClientId ? googleHost : null,
    auth.devLoginEnabled ? devLoginForm((email, name) => finish(api("/api/auth/dev-login", { method: "POST", body: { email, name }, auth: false }))) : null,
    errorEl,
    h("a", { class: "small center", href: "/TermsOfUse", "data-link": "" }, t("termsViewTerms")));

  clear(el, h("div", { class: "top-actions" }, languageButton()),
    h("div", { class: "fullscreen-center" }, h("div", { style: { width: "100%", maxWidth: "400px" } }, card)));

  if (auth.googleClientId) {
    try {
      await loadGoogleScript();
      window.google.accounts.id.initialize({
        client_id: auth.googleClientId,
        callback: (resp) => finish(api("/api/auth/google", { method: "POST", body: { credential: resp.credential }, auth: false })),
        ux_mode: "popup",
      });
      window.google.accounts.id.renderButton(googleHost, {
        theme: "outline", size: "large", shape: "pill", text: "continue_with", width: Math.min(320, el.clientWidth - 72),
        locale: document.documentElement.lang,
      });
    } catch (e) {
      errorEl.textContent = e.message;
    }
  }
}

function devLoginForm(onSubmit) {
  const email = h("input", { class: "input", type: "email", required: true, autocomplete: "email", value: "admin@example.com" });
  const name = h("input", { class: "input", type: "text", autocomplete: "name", value: "" });
  return h("form", {
    class: "stack", style: { "--gap": "10px" },
    onsubmit: (e) => { e.preventDefault(); onSubmit(email.value.trim(), name.value.trim()); },
  },
  h("div", { class: "divider" }, `${t("devLogin")} · ${t("devLoginHint")}`),
  field(t("emailAddress"), email),
  field(t("name"), name),
  h("button", { class: "btn outline block", type: "submit" }, t("signIn")));
}

function restricted() {
  return h("div", { class: "fullscreen-center" }, h("div", { class: "card pad stack center", style: { maxWidth: "440px", "--gap": "12px", padding: "28px" } },
    h("div", { style: { color: "var(--warning-fg)" } }, icon("alert", "lg")),
    h("h1", null, t("accessRestricted")),
    h("p", { class: "muted" }, t("notRegistered")),
    h("a", { class: "btn outline", href: "/login", onclick: () => location.reload() }, t("back"))));
}
