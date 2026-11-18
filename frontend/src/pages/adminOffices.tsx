import { useState, type FormEvent } from "react";
import { del, get, post, put } from "../api";
import { t } from "../i18n";
import { Link } from "../router";
import { loadSettings, money, useStore } from "../store";
import type { OfficeDto } from "../types/api";
import { useAsync } from "../useAsync";
import { ImageInput } from "../components/imageInput";
import { Empty, ErrorBox, Field, Icon, PageHeader, Skeletons, confirmSheet, errorText, openSheet, toast } from "../ui";

const FORM_ID = "office-form";

export default function AdminOffices() {
  const { state, reload } = useAsync(() => get<OfficeDto[]>("/api/admin/offices"));

  function edit(o: OfficeDto | null) {
    openSheet({
      title: o ? t("editOffice") : t("addNewOffice"),
      body: ({ close }) => <OfficeForm office={o} onSaved={() => { close(); reload(); }} />,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={close}>{t("cancel")}</button>,
        <button key="save" className="btn" type="submit" form={FORM_ID}>{o ? t("update") : t("create")}</button>,
      ],
    });
  }

  async function remove(o: OfficeDto) {
    if (!await confirmSheet({ title: t("deleteOffice"), message: `${t("deleteOfficeConfirm")} ${o.name}?`, confirmText: t("delete"), danger: true })) return;
    try { await del(`/api/admin/offices/${o.id}`); reload(); } catch (e) { toast(errorText(e), "error"); }
  }

  return (
    <>
      <PageHeader title={t("manageOffices")} subtitle={t("editPricing")}
        actions={<Link className="icon-btn flat" href="/SiteSettingsPage" aria-label={t("siteSettings")} title={t("siteSettings")}><Icon name="settings" /></Link>} />
      <div className="container wide stack" style={{ paddingBlock: "16px", "--gap": "16px" }}>
        <button className="btn block" type="button" onClick={() => edit(null)}><Icon name="plus" />{t("addOffice")}</button>
        <div className="grid-cards">
          {state.status === "loading" ? <Skeletons n={2} height={200} />
            : state.status === "error" ? <ErrorBox error={state.error} onRetry={reload} />
              : state.data.length ? state.data.map((o) => (
                <article key={o.id} className="card office-card">
                  <div className="cover" style={{ aspectRatio: "16 / 7" }}>
                    {o.imageUrl ? <img src={o.imageUrl} alt="" loading="lazy" /> : null}
                    <span className="price-tag"><bdi>{money(o.hourlyRate, o.currency)}</bdi> <span className="xs muted">{t("perHour")}</span></span>
                  </div>
                  <div className="stack" style={{ padding: "14px 16px", "--gap": "8px" }}>
                    <div className="row between">
                      <h3 className="truncate">{o.name}</h3>
                      <span className={`badge ${o.isAvailable ? "success" : ""}`}>{o.isAvailable ? t("available") : t("unavailable")}</span>
                    </div>
                    <div className="meta">
                      <span><Icon name="users" className="sm" />{`${o.capacity} ${t("people")}`}</span>
                      {o.location ? <span><Icon name="pin" className="sm" />{o.location}</span> : null}
                    </div>
                    <div className="row" style={{ justifyContent: "flex-end" }}>
                      <button className="btn outline sm" type="button" onClick={() => edit(o)}><Icon name="edit" className="sm" />{t("edit")}</button>
                      <button className="btn danger-outline sm" type="button" onClick={() => remove(o)}><Icon name="trash" className="sm" />{t("delete")}</button>
                    </div>
                  </div>
                </article>
              )) : <Empty icon="building" text={t("noOfficesFound")} />}
        </div>
        <section className="card pad stack"><h2>{t("homeBanners")}</h2><Banners /></section>
      </div>
    </>
  );
}

function OfficeForm({ office: o, onSaved }: { office: OfficeDto | null; onSaved: () => void }) {
  const [name, setName] = useState(o?.name || "");
  const [description, setDescription] = useState(o?.description || "");
  const [imageUrl, setImageUrl] = useState(o?.imageUrl || "");
  const [capacity, setCapacity] = useState(o ? String(o.capacity) : "");
  const [rate, setRate] = useState(o ? String(o.hourlyRate) : "");
  const [location, setLocation] = useState(o?.location || "");
  const [amenities, setAmenities] = useState((o?.amenities || []).join(", "));
  const [available, setAvailable] = useState(o ? o.isAvailable : true);
  const [error, setError] = useState("");

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const body = {
      name: name.trim(), description: description.trim(), imageUrl,
      capacity: Number(capacity), hourlyRate: Number(rate), location: location.trim(),
      amenities: amenities.split(",").map((s) => s.trim()).filter(Boolean), isAvailable: available,
    };
    try {
      if (o) await put(`/api/admin/offices/${o.id}`, body); else await post("/api/admin/offices", body);
      onSaved();
    } catch (ex) { setError(errorText(ex)); }
  };

  return (
    <form id={FORM_ID} className="stack" style={{ "--gap": "14px" }} onSubmit={submit}>
      <Field label={t("officeName")}><input className="input" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} /></Field>
      <Field label={t("description")}><textarea className="textarea" maxLength={2000} value={description} onChange={(e) => setDescription(e.target.value)} /></Field>
      <ImageInput value={imageUrl} label={t("image")} onChange={setImageUrl} />
      <div className="row" style={{ "--gap": "10px", alignItems: "flex-start" }}>
        <div className="grow"><Field label={t("capacity")}>
          <input className="input" type="number" inputMode="numeric" min={1} max={500} required value={capacity} onChange={(e) => setCapacity(e.target.value)} />
        </Field></div>
        <div className="grow"><Field label={`${t("hourlyRate")} (${money(0).replace(/[\d.,\s]/g, "")})`}>
          <input className="input" type="number" inputMode="decimal" min={0} step="0.5" required value={rate} onChange={(e) => setRate(e.target.value)} />
        </Field></div>
      </div>
      <Field label={t("location")}><input className="input" value={location} onChange={(e) => setLocation(e.target.value)} /></Field>
      <Field label={t("amenities")}><input className="input" placeholder="WiFi, Coffee, Parking" value={amenities} onChange={(e) => setAmenities(e.target.value)} /></Field>
      <label className="switch"><input type="checkbox" checked={available} onChange={(e) => setAvailable(e.target.checked)} />{t("availableForBooking")}</label>
      <p className="error-text" role="alert">{error}</p>
    </form>
  );
}

function Banners() {
  const { settings } = useStore();
  const [pending, setPending] = useState("");
  // A new ImageInput after each add, so the picker starts empty again.
  const [adderKey, setAdderKey] = useState(0);
  const items = settings?.banners ?? [];

  const run = async (fn: () => Promise<unknown>) => {
    try { await fn(); await loadSettings(); } catch (e) { toast(errorText(e), "error"); }
  };

  return (
    <div className="stack">
      {items.length ? (
        <div className="thumbs">
          {items.map((b) => (
            <div key={b.id} className="thumb">
              <img src={b.url} alt="" />
              <button className="icon-btn" type="button" aria-label={t("delete")} onClick={() => run(() => del(`/api/admin/settings/banners/${b.id}`))}>
                <Icon name="trash" className="sm" />
              </button>
            </div>
          ))}
        </div>
      ) : <p className="small muted">{t("noBannersYet")}</p>}
      <ImageInput key={adderKey} onChange={setPending} />
      <button className="btn outline" type="button" onClick={() => {
        if (!pending) return;
        run(async () => {
          await post("/api/admin/settings/banners", { value: pending });
          setPending("");
          setAdderKey((k) => k + 1);
        });
      }}>
        <Icon name="plus" className="sm" />{t("addBanner")}
      </button>
    </div>
  );
}
