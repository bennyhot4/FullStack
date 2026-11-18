// Tiny history router (plan 009, Q3): no dependency. The route table and guards live in app.tsx.
import { useSyncExternalStore, type AnchorHTMLAttributes, type MouseEvent } from "react";

export interface Loc {
  path: string;
  search: string;
  /** Bumps on every navigation, also to the same URL, so the page re-renders like a fresh visit. */
  key: number;
}

const listeners = new Set<() => void>();
const read = (key: number): Loc => ({ path: location.pathname.replace(/\/+$/, "") || "/", search: location.search, key });
let current = read(0);

function emit() {
  current = read(current.key + 1);
  listeners.forEach((fn) => fn());
}

window.addEventListener("popstate", emit);

export function navigate(to: string, { replace = false }: { replace?: boolean } = {}) {
  const url = new URL(to, location.origin);
  history[replace ? "replaceState" : "pushState"]({}, "", url.pathname + url.search + url.hash);
  emit();
}

/** Re-runs the current route (e.g. a "Retry" button). */
export const reload = () => emit();

function subscribe(fn: () => void) {
  listeners.add(fn);
  return () => { listeners.delete(fn); };
}

export const useLocation = () => useSyncExternalStore(subscribe, () => current);

/** Internal link: navigates without a page load; modified clicks (new tab etc.) keep the browser default. */
export function Link({ href, onClick, ...rest }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string }) {
  const click = (e: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(e);
    if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey || rest.target === "_blank") return;
    e.preventDefault();
    navigate(href);
  };
  return <a href={href} onClick={click} {...rest} />;
}
