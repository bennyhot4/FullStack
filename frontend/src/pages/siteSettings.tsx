import { useState } from "react";
import { get, put } from "../api";
import { t } from "../i18n";
import { applyBranding, loadSettings, settings } from "../store";
import type { Branding, TermsDoc } from "../types/api";
import { useAsync } from "../useAsync";
import { ImageInput } from "../components/imageInput";
import { Field, Icon, PageHeader, confirmSheet, errorText, toast } from "../ui";

const HEX = /^#[0-9a-f]{6}$/i;

/** Runs a save, reloads the public settings and shows a toast. Returns a [busy, run] pair for one button. */
function useSave() {
  const [busy, setBusy] = useState(false);
  const run = async (fn: () => Promise<unknown>) => {
    setBusy(true);
    try { await fn(); await loadSettings(); toast(t("saved"), "success"); }
    catch (e) { toast(errorText(e), "error"); }
    finally { setBusy(false); }
  };
  return [busy, run] as const;
}

export default function SiteSettings() {
  const s = settings();
  const [banner, setBanner] = useState(s.homeBannerImage);
  const [savingBanner, saveBanner] = useSave();

  return (
    <>
      <PageHeader title={t("siteSettings")} back="/AdminOffices" />
      <div className="container stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        <section className="card pad stack">
          <h2 className="row"><Icon name="image" />{t("mainBannerImage")}</h2>
          <ImageInput value={s.homeBannerImage} label={t("mainBannerImage")} onChange={setBanner} />
          <button className="btn" type="button" disabled={savingBanner}
            onClick={() => saveBanner(() => put("/api/admin/settings/home-banner-image", { value: banner }))}>
            {t("saveSettings")}
          </button>
        </section>
        <BrandingSection initial={s.branding} />
        <section className="card pad stack">
          <h2 className="row"><Icon name="file" />{t("editTerms")}</h2>
          <TermsEditor />
        </section>
      </div>
    </>
  );
}

// Branding, with a live preview while editing (colors are applied as you pick them).
function BrandingSection({ initial }: { initial: Branding }) {
  const [b, setB] = useState<Branding>({ ...initial });
  const [saving, save] = useSave();
  const set = (patch: Partial<Branding>) => setB((prev) => ({ ...prev, ...patch }));

  return (
    <section className="card pad stack">
      <h2 className="row"><Icon name="palette" />{t("branding")}</h2>
      <Field label={t("pageTitle")}><input className="input" placeholder="Office Booking" value={b.title} onChange={(e) => set({ title: e.target.value })} /></Field>
      <Field label={t("pageDescription")}>
        <input className="input" placeholder="Find and book your perfect workspace" value={b.description} onChange={(e) => set({ description: e.target.value })} />
      </Field>
      <ImageInput value={b.logo} label={t("logo")} onChange={(logo) => set({ logo })} />
      {(["buttonsColor", "backgroundColor", "pagesColor"] as const).map((key) => (
        <ColorField key={key} label={t(key)} value={b[key]} onChange={(v) => { const next = { ...b, [key]: v }; setB(next); applyBranding(next); }} />
      ))}
      <button className="btn" type="button" disabled={saving} onClick={() => save(() => put("/api/admin/settings/branding", {
        title: b.title.trim(), description: b.description.trim(), logo: b.logo,
        buttonsColor: b.buttonsColor, backgroundColor: b.backgroundColor, pagesColor: b.pagesColor,
      }))}>
        {t("saveBranding")}
      </button>
    </section>
  );
}

function ColorField({ label, value, onChange }: { label: string; value: string; onChange: (v: string) => void }) {
  const [text, setText] = useState(value);
  const sync = (v: string) => { if (HEX.test(v)) { setText(v); onChange(v); } };
  return (
    <div className="field">
      <span className="label">{label}</span>
      <div className="color-field">
        <input type="color" value={value} aria-label={label} onChange={(e) => sync(e.target.value)} />
        <input className="input" dir="ltr" maxLength={7} style={{ maxWidth: "130px" }} value={text}
          onChange={(e) => { setText(e.target.value); sync(e.target.value.trim()); }} />
      </div>
    </div>
  );
}

// Terms of use: publishing a new version makes everyone sign again.
function TermsEditor() {
  const { state } = useAsync(() => get<TermsDoc>("/api/terms/current"));
  if (state.status === "loading") return <div className="stack" />;
  if (state.status === "error") return <div className="stack"><p className="error-text">{errorText(state.error)}</p></div>;
  return <TermsForm doc={state.data} />;
}

function TermsForm({ doc }: { doc: TermsDoc }) {
  const [he, setHe] = useState(doc.contentHe);
  const [en, setEn] = useState(doc.contentEn);
  const [saving, save] = useSave();
  const publish = async () => {
    if (!await confirmSheet({ title: t("publishNewVersion"), message: t("publishTermsConfirm"), confirmText: t("publishNewVersion") })) return;
    await save(() => put("/api/admin/terms", { contentHe: he, contentEn: en }));
  };
  return (
    <div className="stack">
      <p className="xs muted">{`${t("version")} ${doc.version}`}</p>
      <Field label={t("termsHebrew")}><textarea className="textarea" rows={10} dir="rtl" value={he} onChange={(e) => setHe(e.target.value)} /></Field>
      <Field label={t("termsEnglish")}><textarea className="textarea" rows={10} dir="ltr" value={en} onChange={(e) => setEn(e.target.value)} /></Field>
      <button className="btn outline" type="button" disabled={saving} onClick={publish}>{t("publishNewVersion")}</button>
    </div>
  );
}
