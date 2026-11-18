import { useEffect, useState, type FormEvent } from "react";
import { asApiError, put } from "../api";
import { t } from "../i18n";
import { navigate } from "../router";
import { loadMe, me } from "../store";
import { Field, Icon } from "../ui";

export default function Register() {
  const { user: u } = me();
  // Checked once when the page opens; after saving, submit() decides where to go.
  const [alreadyDone] = useState(() => !me().needsProfile);
  const [first, setFirst] = useState(u.firstName || "");
  const [last, setLast] = useState(u.lastName || "");
  const [phone, setPhone] = useState("");
  const [consent, setConsent] = useState(!!u.marketingConsent);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => { if (alreadyDone) navigate("/", { replace: true }); }, [alreadyDone]);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError("");
    if (phone.replace(/\D/g, "").length < 9) { setError(t("invalidPhone")); return; }
    setSaving(true);
    try {
      await put("/api/me", { firstName: first.trim(), lastName: last.trim(), phone, marketingConsent: consent });
      const updated = await loadMe();
      navigate(updated.needsTerms ? "/TermsOfUse" : "/", { replace: true });
    } catch (ex) {
      const err = asApiError(ex);
      setError(err.code === "invalid_phone" ? t("invalidPhone") : (err.message || t("loadingFailed")));
      setSaving(false);
    }
  };

  return (
    <div className="fullscreen-center">
      <div style={{ width: "100%", maxWidth: "440px" }}>
        <form className="card pad stack" style={{ "--gap": "14px", padding: "24px 20px" }} onSubmit={submit}>
          <div className="row" style={{ "--gap": "12px" }}>
            <div className="avatar">{u.pictureUrl ? <img src={u.pictureUrl} alt="" /> : <Icon name="user" />}</div>
            <div><h1>{t("profileSetupTitle")}</h1><p className="small muted">{t("profileSetupSubtitle")}</p></div>
          </div>
          <div className="row" style={{ "--gap": "10px", alignItems: "flex-start" }}>
            <div className="grow"><Field label={t("firstName")}>
              <input className="input" required autoComplete="given-name" maxLength={60} autoFocus={!u.firstName}
                value={first} onChange={(e) => setFirst(e.target.value)} />
            </Field></div>
            <div className="grow"><Field label={t("lastName")}>
              <input className="input" required autoComplete="family-name" maxLength={60} value={last} onChange={(e) => setLast(e.target.value)} />
            </Field></div>
          </div>
          <Field label={t("emailAddress")}><input className="input" type="email" value={u.email} disabled /></Field>
          <Field label={t("phoneNumber")}>
            <input className="input" type="tel" inputMode="tel" autoComplete="tel" required placeholder="050-1234567" dir="ltr"
              autoFocus={!!u.firstName} value={phone} onChange={(e) => setPhone(e.target.value)} />
          </Field>
          {/* Plan 008, Q4: built but hidden for now (.is-hidden = display: none). Remove the class to show it. */}
          <div className="is-hidden">
            <label className="switch"><input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} />{t("marketingConsent")}</label>
          </div>
          <p className="error-text" role="alert">{error}</p>
          <button className="btn lg block" type="submit" disabled={saving}>{saving ? t("saving") : t("save")}</button>
        </form>
      </div>
    </div>
  );
}
