import { StrictMode } from "react";
import { createRoot, type Root } from "react-dom/client";
import { onSignedOut } from "./api";
import { App } from "./app";
import { applyDocumentLang } from "./i18n";
import { navigate } from "./router";
import { loadSettings, setMe } from "./store";
import { ErrorBox } from "./ui";
import "../css/base.css";
import "../css/components.css";

// Hide images that fail to load (e.g. a deleted upload) instead of showing the broken-image icon.
document.addEventListener("error", (e) => { if (e.target instanceof HTMLImageElement) e.target.classList.add("broken"); }, true);
onSignedOut(() => { setMe(null); navigate("/login", { replace: true }); });

let root: Root | null = null;

async function start() {
  applyDocumentLang();
  root ??= createRoot(document.getElementById("app")!);
  try {
    await loadSettings();
  } catch (e) {
    root.render(<ErrorBox error={e} onRetry={start} />);
    return;
  }
  root.render(<StrictMode><App /></StrictMode>);
  if ("serviceWorker" in navigator && location.protocol === "https:") navigator.serviceWorker.register("/sw.js").catch(() => {});
}

start();
