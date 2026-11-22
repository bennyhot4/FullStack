// Shared building blocks. Class names match the CSS in css/components.css (and the e2e smoke test).
import { cloneElement, useId, type ReactElement, type ReactNode } from "react";
import { asApiError } from "../api";
import { hasKey, t } from "../i18n";
import { Link } from "../router";
import type { BookingStatus } from "../types/api";
import { Icon, type IconName } from "./icon";

export { Icon, amenityIcon, type IconName } from "./icon";
export { confirmSheet, openSheet, type SheetApi } from "./sheet";


// ---------- Toasts ----------
// Plain DOM on purpose: a toast outlives the page that raised it. Text only, never HTML.
export function toast(message: string, type: "info" | "success" | "error" = "info", ms = 3500) {
  const host = document.getElementById("toasts");
  if (!host) return;
  const el = document.createElement("div");
  el.className = `toast ${type}`;
  el.setAttribute("role", type === "error" ? "alert" : "status");
  el.textContent = message;
  host.append(el);
  setTimeout(() => el.remove(), ms);
}

// ---------- Errors ----------
/** User-facing text for an API error: the translated text for its `code` when one exists, else the server message. */
export function errorText(err: unknown) {
  const e = asApiError(err);
  return hasKey(e.code) ? t(e.code) : (e.message || t("loadingFailed"));
}

export function ErrorBox({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <div className="empty">
      <Icon name="alert" />
      <p>{asApiError(error).message || t("loadingFailed")}</p>
      {onRetry ? <button className="btn outline" type="button" onClick={onRetry}>{t("retry")}</button> : null}
    </div>
  );
}

// ---------- Small building blocks ----------
export const Spinner = () => (
  <div className="fullscreen-center"><div className="spinner" role="status" aria-label="Loading" /></div>
);

export const Skeletons = ({ n = 3, height = 120 }: { n?: number; height?: number }) => (
  <div className="stack">
    {Array.from({ length: n }, (_, i) => <div key={i} className="skeleton" style={{ height: `${height}px` }} />)}
  </div>
);

export const Empty = ({ icon, text, children }: { icon: IconName; text: string; children?: ReactNode }) => (
  <div className="empty"><Icon name={icon} /><p>{text}</p>{children}</div>
);

export const BackButton = ({ href }: { href: string }) => (
  <Link className="icon-btn flat" href={href} aria-label={t("back")}><Icon name="arrowLeft" className="flip-rtl" /></Link>
);

export function PageHeader({ title, subtitle, back, actions }: { title: string; subtitle?: ReactNode; back?: string; actions?: ReactNode }) {
  return (
    <header className="page-header">
      <div className="container wide">
        <div className="row between" style={{ "--gap": "12px" }}>
          <div className="row grow" style={{ "--gap": "12px" }}>
            {back ? <BackButton href={back} /> : null}
            <div className="header-title">
              <h1 className="truncate">{title}</h1>
              {subtitle ? <p className="small muted truncate">{subtitle}</p> : null}
            </div>
          </div>
          {actions ?? null}
        </div>
      </div>
    </header>
  );
}

export function StatusBadge({ status }: { status: BookingStatus }) {
  const cls = { confirmed: "success", cancelled: "danger", completed: "" }[status] ?? "";
  return <span className={`badge ${cls}`}>{t(status)}</span>;
}

/** A labelled form control: gives the control an id (unless it has one) and points the label at it. */
export function Field({ label, hint, children }: { label: string; hint?: string; children: ReactElement<{ id?: string }> }) {
  const generated = useId();
  const id = children.props.id ?? generated;
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {cloneElement(children, { id })}
      {hint ? <p className="xs muted">{hint}</p> : null}
    </div>
  );
}
