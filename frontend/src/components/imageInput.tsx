// Image picker: upload a file (stored by the API) or paste a URL; shows a preview.
import { useRef, useState } from "react";
import { upload } from "../api";
import { t } from "../i18n";
import { Icon, errorText, toast } from "../ui";

export function ImageInput({ value = "", onChange, label }: { value?: string; onChange?: (url: string) => void; label?: string }) {
  const [current, setCurrent] = useState(value);
  const [text, setText] = useState(value);
  const [uploading, setUploading] = useState(false);
  const file = useRef<HTMLInputElement>(null);

  const toSafeImageUrl = (raw: string) => {
    const trimmed = raw.trim();
    if (!trimmed) return "";
    // Allow relative paths and only http(s) absolute URLs.
    if (trimmed.startsWith("/")) return trimmed;
    try {
      const u = new URL(trimmed);
      return u.protocol === "http:" || u.protocol === "https:" ? trimmed : "";
    } catch {
      return "";
    }
  };

  const set = (v: string) => {
    setCurrent(v);
    setText(v);
    onChange?.(v);
  };
  // Like the native "change" event: the URL is taken when the field is left (or Enter), not on every key.
  const commit = () => {
    const safe = toSafeImageUrl(text);
    if (safe !== current) set(safe);
  };

  const pick = async () => {
    const f = file.current?.files?.[0];
    if (!f) return;
    setUploading(true);
    try { set(await upload(f)); } catch (e) { toast(errorText(e), "error"); }
    finally {
      setUploading(false);
      if (file.current) file.current.value = "";
    }
  };

  return (
    <div className="field">
      {label ? <span className="label">{label}</span> : null}
      <img className="image-preview" alt="" hidden={!current} src={current || undefined} />
      <div className="row" style={{ "--gap": "8px" }}>
        <button className="btn outline sm" type="button" disabled={uploading} onClick={() => file.current?.click()}>
          <Icon name="upload" className="sm" />{t("uploadImage")}
        </button>
        <span className="xs muted">{uploading ? t("uploading") : ""}</span>
      </div>
      <input ref={file} type="file" accept="image/png,image/jpeg,image/webp,image/gif" className="sr-only" onChange={pick} />
      <input className="input" type="url" inputMode="url" placeholder={t("orPasteUrl")} dir="ltr" value={text}
        onChange={(e) => setText(e.target.value)} onBlur={commit} onKeyDown={(e) => { if (e.key === "Enter") commit(); }} />
    </div>
  );
}
