import { useEffect, useRef, useState } from "react";
import { get } from "../api";
import { t } from "../i18n";
import { Link } from "../router";
import { settings } from "../store";
import type { ConnectedApp } from "../types/api";
import { ASSISTANTS, type Assistant, type AssistantId } from "../components/assistants";
import { Empty, Icon, PageHeader, toast } from "../ui";

const POLL_MS = 5000;
const POLL_FOR_MS = 10 * 60 * 1000;

// Plan 012: connect the assistant you already use in about a minute. Pick it, copy the address (or press the one-click
// button), allow on the consent page; this page notices the new connection by itself.
export default function ConnectAssistant() {
  const mcp = settings().mcp;
  const [picked, setPicked] = useState<AssistantId | null>(null);
  const connected = useNewConnection(mcp.available);

  if (!mcp.available) {
    return (
      <>
        <PageHeader title={t("connectAssistant")} back="/MyBookings" />
        <div className="container" style={{ paddingBlock: "16px" }}><Empty icon="sparkle" text={t("connectUnavailable")} /></div>
      </>
    );
  }

  const assistant = ASSISTANTS.find((a) => a.id === picked);
  return (
    <>
      <PageHeader title={t("connectAssistant")} subtitle={t("connectAssistantIntro")} back="/MyBookings" />
      <div className="container stack connect-assistant" style={{ paddingBlock: "16px", "--gap": "16px", maxWidth: "640px" }}>
        {connected ? <ConnectedBanner name={connected} /> : null}

        <section className="stack" style={{ "--gap": "8px" }}>
          <h2>{t("connectPick")}</h2>
          <div className="assistant-grid" role="group" aria-label={t("connectPick")}>
            {ASSISTANTS.map((a) => (
              <button key={a.id} type="button" className="chip assistant-choice" aria-pressed={a.id === picked}
                onClick={() => setPicked(a.id)}>
                {a.name || t("connectOther")}
              </button>
            ))}
          </div>
        </section>

        {assistant ? <Guide key={assistant.id} assistant={assistant} url={mcp.url} /> : null}

        {!connected && assistant ? (
          <p className="small muted row" role="status" style={{ "--gap": "8px" }}>
            <span className="spinner inline" aria-hidden="true" />{t("connectWaiting")}
          </p>
        ) : null}

        <Link className="small center row" style={{ justifyContent: "center", "--gap": "6px", minHeight: "44px" }} href="/ConnectedApps">
          <Icon name="link" className="sm" />{t("connectedApps")}
        </Link>
      </div>
    </>
  );
}

function Guide({ assistant: a, url }: { assistant: Assistant; url: string }) {
  const steps = t(a.steps).split("\n");
  const open = a.open?.(url);
  const command = a.command?.(url);
  return (
    <section className="card pad stack" style={{ "--gap": "12px" }}>
      <h2>{a.name ? `${t("connectStepsFor")} ${a.name}` : t("connectOther")}</h2>
      {a.note ? <p className="small" role="note" style={{ color: "var(--warning-fg)" }}><Icon name="alert" className="sm" /> {t(a.note)}</p> : null}
      {open ? (
        <a className="btn block" href={open.href} target={open.href.startsWith("http") ? "_blank" : undefined} rel="noopener noreferrer">
          <Icon name="link" className="sm" />{`${t(open.label)} ${a.name}`}
        </a>
      ) : null}
      <ol className="stack connect-steps" style={{ "--gap": "8px", paddingInlineStart: "22px", margin: "0" }}>
        {steps.map((s) => <li key={s}>{s}</li>)}
      </ol>
      {command
        ? <CopyBox label={t("connectCommand")} value={command} />
        : <CopyBox label={t("connectAddress")} value={url} />}
    </section>
  );
}

/** A read-only value with a big Copy button (the address or the Codex command). */
function CopyBox({ label, value }: { label: string; value: string }) {
  const input = useRef<HTMLInputElement>(null);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(value);
    } catch {
      // Old browsers / no permission: select the text so a long-press or Ctrl+C copies it.
      input.current?.select();
      return;
    }
    toast(t("connectCopied"), "success");
  };
  return (
    <div className="field copy-box">
      <label>{label}</label>
      <div className="row" style={{ "--gap": "8px" }}>
        <input ref={input} className="grow" dir="ltr" readOnly value={value} onFocus={(e) => e.currentTarget.select()} aria-label={label} />
        <button className="btn" type="button" onClick={copy}><Icon name="copy" className="sm" />{t("connectCopy")}</button>
      </div>
    </div>
  );
}

function ConnectedBanner({ name }: { name: string }) {
  return (
    <section className="card pad stack connect-done" role="status" style={{ "--gap": "8px" }}>
      <h2 className="row" style={{ "--gap": "8px" }}><Icon name="checkCircle" />{`${t("connectDone")} ${name}`}</h2>
      <p className="small">{t("connectTryIntro")}</p>
      <ul className="small stack" style={{ "--gap": "4px", paddingInlineStart: "18px", margin: "0" }}>
        <li>{t("connectTry1")}</li>
        <li>{t("connectTry2")}</li>
        <li>{t("connectTry3")}</li>
      </ul>
    </section>
  );
}

/** Reads the person's connections every few seconds while the page is open (at most 10 minutes) and returns the name
 *  of an assistant that was connected, or reconnected, after the page opened. */
function useNewConnection(enabled: boolean): string | null {
  const [found, setFound] = useState<string | null>(null);
  useEffect(() => {
    if (!enabled) return;
    let before: Map<string, string | null> | null = null;
    let stopped = false;
    // Called only after the timers below exist (from a later tick).
    const stop = () => { stopped = true; clearInterval(timer); clearTimeout(deadline); };
    const check = async () => {
      try {
        const apps = await get<ConnectedApp[]>("/api/me/connected-apps");
        if (stopped) return;
        if (!before) { before = new Map(apps.map((a) => [a.clientId, a.connectedAt])); return; }
        const fresh = apps.find((a) => !before!.has(a.clientId) || before!.get(a.clientId) !== a.connectedAt);
        if (fresh) { setFound(fresh.name); stop(); }
      } catch { /* a failed check is simply tried again */ }
    };
    const timer = setInterval(check, POLL_MS);
    const deadline = setTimeout(stop, POLL_FOR_MS);
    void check();
    return stop;
  }, [enabled]);
  return found;
}
