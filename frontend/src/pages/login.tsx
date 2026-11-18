import { useEffect, useRef, useState, type FormEvent } from "react";
import { api, asApiError, setSession } from "../api";
import { t } from "../i18n";
import { Link, navigate } from "../router";
import { setMe, settings } from "../store";
import type { AuthResponse } from "../types/api";
import { Field, Icon } from "../ui";
import { LanguageButton } from "../ui/language";
import type { PageProps } from "../app";

let gisLoaded: Promise<void> | null = null;
function loadGoogleScript() {
  gisLoaded ??= new Promise<void>((resolve, reject) => {
    const s = document.createElement("script");
    s.src = "https://accounts.google.com/gsi/client";
    s.async = true;
    s.onload = () => resolve();
    s.onerror = () => { gisLoaded = null; reject(new Error("Google sign-in could not be loaded")); };
    document.head.append(s);
  });
  return gisLoaded;
}

export default function Login({ params }: PageProps) {
  const { branding, auth } = settings();
  const title = branding.title || "HourOffice";
  const [error, setError] = useState("");
  const [restricted, setRestricted] = useState(false);
  const googleHost = useRef<HTMLDivElement>(null);
  const from = params.get("from");

  const finish = async (promise: Promise<AuthResponse>) => {
    setError("");
    try {
      setSession(await promise);
      setMe(null);
      navigate(from && from.startsWith("/") && !from.startsWith("//") ? from : "/", { replace: true });
    } catch (e) {
      const err = asApiError(e);
      if (err.code === "user_not_registered") { setRestricted(true); return; }
      setError(err.message || t("loadingFailed"));
    }
  };
  const finishRef = useRef(finish);
  useEffect(() => { finishRef.current = finish; });

  useEffect(() => {
    const clientId = auth.googleClientId;
    const host = googleHost.current;
    if (!clientId || !host) return;
    let live = true;
    loadGoogleScript().then(() => {
      if (!live || !window.google) return;
      window.google.accounts.id.initialize({
        client_id: clientId,
        callback: (resp) => finishRef.current(api<AuthResponse>("/api/auth/google", { method: "POST", body: { credential: resp.credential }, auth: false })),
        ux_mode: "popup",
      });
      window.google.accounts.id.renderButton(host, {
        theme: "outline", size: "large", shape: "pill", text: "continue_with",
        width: Math.min(320, (host.closest("main")?.clientWidth ?? 360) - 72),
        locale: document.documentElement.lang,
      });
    }, (e: Error) => { if (live) setError(e.message); });
    return () => { live = false; };
  }, [auth.googleClientId]);

  if (restricted) return <Restricted />;

  const welcome = t("welcomeTo");
  return (
    <>
      <div className="top-actions"><LanguageButton /></div>
      <div className="fullscreen-center">
        <div style={{ width: "100%", maxWidth: "400px" }}>
          <div className="card pad stack" style={{ "--gap": "16px", padding: "28px 20px" }}>
            {branding.logo ? <img className="logo" src={branding.logo} alt="" /> : <div className="logo" aria-hidden="true">{title[0]}</div>}
            <div className="center stack" style={{ "--gap": "4px" }}>
              <h1>{`${welcome}${welcome.endsWith("ל") ? "" : " "}${title}`}</h1>
              <p className="muted">{branding.description || t("signInToContinue")}</p>
            </div>
            {auth.googleClientId ? <div ref={googleHost} className="row" style={{ justifyContent: "center", minHeight: "44px" }} /> : null}
            {auth.devLoginEnabled ? (
              <DevLoginForm onSubmit={(email, name) => finish(api<AuthResponse>("/api/auth/dev-login", { method: "POST", body: { email, name }, auth: false }))} />
            ) : null}
            <p className="error-text center" role="alert">{error}</p>
            <Link className="small center" href="/TermsOfUse">{t("termsViewTerms")}</Link>
            <Link className="small center" href="/PrivacyPolicy">{t("privacyViewPolicy")}</Link>
          </div>
        </div>
      </div>
    </>
  );
}

function DevLoginForm({ onSubmit }: { onSubmit: (email: string, name: string) => void }) {
  const [email, setEmail] = useState("admin@example.com");
  const [name, setName] = useState("");
  const submit = (e: FormEvent) => { e.preventDefault(); onSubmit(email.trim(), name.trim()); };
  return (
    <form className="stack" style={{ "--gap": "10px" }} onSubmit={submit}>
      <div className="divider">{`${t("devLogin")} · ${t("devLoginHint")}`}</div>
      <Field label={t("emailAddress")}>
        <input className="input" type="email" required autoComplete="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      </Field>
      <Field label={t("name")}>
        <input className="input" type="text" autoComplete="name" value={name} onChange={(e) => setName(e.target.value)} />
      </Field>
      <button className="btn outline block" type="submit">{t("signIn")}</button>
    </form>
  );
}

function Restricted() {
  return (
    <div className="fullscreen-center">
      <div className="card pad stack center" style={{ maxWidth: "440px", "--gap": "12px", padding: "28px" }}>
        <div style={{ color: "var(--warning-fg)" }}><Icon name="alert" className="lg" /></div>
        <h1>{t("accessRestricted")}</h1>
        <p className="muted">{t("notRegistered")}</p>
        <a className="btn outline" href="/login" onClick={() => location.reload()}>{t("back")}</a>
      </div>
    </div>
  );
}
