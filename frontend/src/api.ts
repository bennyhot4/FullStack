// HTTP client for the backend. The frontend is deployed separately, so every call goes to config.apiBaseUrl.
import { t } from "./i18n";
import type { AuthResponse } from "./types/api";

const BASE = (window.HOUROFFICE_CONFIG?.apiBaseUrl || "").replace(/\/$/, "");
const REFRESH_KEY = "ho.refreshToken";

let accessToken: string | null = null;
let refreshing: Promise<boolean> | null = null;
const listeners = new Set<() => void>();

export class ApiError extends Error {
  status: number;
  code?: string;
  constructor(status: number, code?: string, message?: string) {
    super(message || code || `HTTP ${status}`);
    this.status = status;
    this.code = code;
  }
}

/** Narrows an unknown catch value: anything that is not an ApiError becomes a generic one. */
export const asApiError = (e: unknown): ApiError =>
  e instanceof ApiError ? e : new ApiError(0, undefined, e instanceof Error ? e.message : String(e));

const storage = {
  get(): string | null { try { return localStorage.getItem(REFRESH_KEY); } catch { return null; } },
  set(v: string | null) { try { if (v) localStorage.setItem(REFRESH_KEY, v); else localStorage.removeItem(REFRESH_KEY); } catch { /* ignore */ } },
};

export const hasSession = () => !!(accessToken || storage.get());
export const onSignedOut = (fn: () => void) => { listeners.add(fn); };

export function setSession(auth: Pick<AuthResponse, "accessToken" | "refreshToken"> | null) {
  accessToken = auth?.accessToken ?? null;
  storage.set(auth?.refreshToken ?? null);
}

export function clearSession() {
  accessToken = null;
  storage.set(null);
}

async function refresh(): Promise<boolean> {
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
export async function ensureSession(): Promise<boolean> {
  if (accessToken) return true;
  return refresh();
}

interface ApiOptions {
  method?: string;
  body?: unknown;
  form?: FormData;
  auth?: boolean;
  retry?: boolean;
}

export async function api<T = unknown>(path: string, { method = "GET", body, form, auth = true, retry = true }: ApiOptions = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`;
  let payload: BodyInit | undefined;
  if (form) payload = form;
  else if (body !== undefined) { headers["Content-Type"] = "application/json"; payload = JSON.stringify(body); }

  let res: Response;
  try {
    res = await fetch(`${BASE}${path}`, { method, headers, body: payload });
  } catch {
    throw new ApiError(0, "network_error", t("loadingFailed"));
  }

  if (res.status === 401 && auth && retry) {
    if (await refresh()) return api<T>(path, { method, body, form, auth, retry: false });
    clearSession();
    listeners.forEach((fn) => fn());
    throw new ApiError(401, "unauthorized");
  }
  if (res.status === 204) return null as T;
  const data = res.headers.get("content-type")?.includes("json") ? await res.json() : null;
  if (!res.ok) throw new ApiError(res.status, data?.code, data?.message);
  return data as T;
}

export const get = <T>(p: string) => api<T>(p);
export const post = <T = unknown>(p: string, body?: unknown) => api<T>(p, { method: "POST", body: body ?? {} });
export const put = <T = unknown>(p: string, body: unknown) => api<T>(p, { method: "PUT", body });
export const del = <T = unknown>(p: string) => api<T>(p, { method: "DELETE" });

export async function upload(file: File): Promise<string> {
  const form = new FormData();
  form.append("file", file);
  const res = await api<{ fileUrl: string }>("/api/admin/upload", { method: "POST", form });
  return res.fileUrl;
}

export async function signOut() {
  const token = storage.get();
  clearSession();
  if (token) {
    try { await api("/api/auth/logout", { method: "POST", body: { refreshToken: token }, auth: false }); } catch { /* ignore */ }
  }
}
