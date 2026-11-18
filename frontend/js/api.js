// HTTP client for the backend. The frontend is deployed separately, so every call goes to config.apiBaseUrl.
import { t } from "./i18n.js";

const BASE = (window.HOUROFFICE_CONFIG?.apiBaseUrl || "").replace(/\/$/, "");
const REFRESH_KEY = "ho.refreshToken";

let accessToken = null;
let refreshing = null;
const listeners = new Set();

export class ApiError extends Error {
  constructor(status, code, message) {
    super(message || code || `HTTP ${status}`);
    this.status = status;
    this.code = code;
  }
}

const storage = {
  get() { try { return localStorage.getItem(REFRESH_KEY); } catch { return null; } },
  set(v) { try { v ? localStorage.setItem(REFRESH_KEY, v) : localStorage.removeItem(REFRESH_KEY); } catch { /* ignore */ } },
};

export const hasSession = () => !!(accessToken || storage.get());
export const onSignedOut = (fn) => listeners.add(fn);

export function setSession(auth) {
  accessToken = auth?.accessToken ?? null;
  storage.set(auth?.refreshToken ?? null);
}

export function clearSession() {
  accessToken = null;
  storage.set(null);
}

async function refresh() {
  const token = storage.get();
  if (!token) return false;
  refreshing ??= (async () => {
    try {
      const res = await fetch(`${BASE}/api/auth/refresh`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: token }),
      });
      if (!res.ok) { clearSession(); return false; }
      setSession(await res.json());
      return true;
    } catch {
      return false; // network error: keep the refresh token for next time
    } finally {
      queueMicrotask(() => { refreshing = null; });
    }
  })();
  return refreshing;
}

/** Ensures we hold an access token (uses the stored refresh token after a page reload). */
export async function ensureSession() {
  if (accessToken) return true;
  return refresh();
}

export async function api(path, { method = "GET", body, form, auth = true, retry = true } = {}) {
  const headers = {};
  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`;
  let payload;
  if (form) payload = form;
  else if (body !== undefined) { headers["Content-Type"] = "application/json"; payload = JSON.stringify(body); }

  let res;
  try {
    res = await fetch(`${BASE}${path}`, { method, headers, body: payload });
  } catch {
    throw new ApiError(0, "network_error", t("loadingFailed"));
  }

  if (res.status === 401 && auth && retry) {
    if (await refresh()) return api(path, { method, body, form, auth, retry: false });
    clearSession();
    listeners.forEach((fn) => fn());
    throw new ApiError(401, "unauthorized");
  }
  if (res.status === 204) return null;
  const data = res.headers.get("content-type")?.includes("json") ? await res.json() : null;
  if (!res.ok) throw new ApiError(res.status, data?.code, data?.message);
  return data;
}

export const get = (p) => api(p);
export const post = (p, body) => api(p, { method: "POST", body: body ?? {} });
export const put = (p, body) => api(p, { method: "PUT", body });
export const del = (p) => api(p, { method: "DELETE" });

export async function upload(file) {
  const form = new FormData();
  form.append("file", file);
  const res = await api("/api/admin/upload", { method: "POST", form });
  return res.fileUrl;
}

export async function signOut() {
  const token = storage.get();
  clearSession();
  if (token) {
    try { await api("/api/auth/logout", { method: "POST", body: { refreshToken: token }, auth: false }); } catch { /* ignore */ }
  }
}
