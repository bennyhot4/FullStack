// Dialog / bottom sheet (bottom sheet on phones, modal on desktop). Opened imperatively, like before the
// React move, so a click handler can open a form: each sheet is a native <dialog class="sheet"> with its
// own small React root, removed from the DOM when it closes.
import { useState, type ReactNode } from "react";
import { createRoot } from "react-dom/client";
import { flushSync } from "react-dom";
import { t } from "../i18n";
import { Icon } from "./icon";

export interface SheetApi {
  close: () => void;
  /** Removes the footer buttons (e.g. after a form shows its result in the body). */
  hideActions: () => void;
}

interface SheetOptions {
  title: ReactNode;
  body: ReactNode | ((api: SheetApi) => ReactNode);
  actions?: ReactNode[] | ((close: () => void) => ReactNode[]);
  onClose?: () => void;
}

function Sheet({ titleId, title, body, actions, close }: SheetOptions & { titleId: string; close: () => void }) {
  const [showActions, setShowActions] = useState(true);
  const api: SheetApi = { close, hideActions: () => setShowActions(false) };
  const content = typeof body === "function" ? body(api) : body;
  const acts = (typeof actions === "function" ? actions(close) : actions ?? []).filter(Boolean);
  return (
    <>
      <div className="sheet-handle" aria-hidden="true" />
      <div className="sheet-head">
        <h2 id={titleId}>{title}</h2>
        <button className="icon-btn flat" type="button" aria-label={t("cancel")} onClick={close}><Icon name="x" /></button>
      </div>
      <div className="sheet-body">{content}</div>
      {showActions && acts.length ? <div className="sheet-foot">{acts}</div> : null}
    </>
  );
}

export function openSheet(options: SheetOptions) {
  const titleId = `sheet-${Math.random().toString(36).slice(2)}`;
  const dlg = document.createElement("dialog");
  dlg.className = "sheet";
  dlg.setAttribute("aria-labelledby", titleId);
  document.body.append(dlg);
  const root = createRoot(dlg);
  const close = () => { if (dlg.open) dlg.close(); };
  dlg.addEventListener("close", () => {
    // The close event is a separate task, never inside a React render, so unmounting here is safe.
    root.unmount();
    dlg.remove();
    options.onClose?.();
  });
  dlg.addEventListener("click", (e) => { if (e.target === dlg) close(); }); // tap on backdrop
  // Render synchronously so showModal() can move focus into the content.
  flushSync(() => root.render(<Sheet {...options} titleId={titleId} close={close} />));
  dlg.showModal();
  return { close, el: dlg };
}

/** Promise<boolean> confirmation sheet. */
export function confirmSheet({ title, message, confirmText, cancelText = t("cancel"), danger = false }:
  { title: string; message: string; confirmText: string; cancelText?: string; danger?: boolean }) {
  return new Promise<boolean>((resolve) => {
    let answered = false;
    openSheet({
      title,
      body: <p className="muted">{message}</p>,
      actions: (close) => [
        <button key="cancel" className="btn outline" type="button" onClick={() => close()}>{cancelText}</button>,
        <button key="ok" className={`btn ${danger ? "danger" : ""}`} type="button" onClick={() => { answered = true; close(); }}>{confirmText}</button>,
      ],
      onClose: () => resolve(answered),
    });
  });
}
