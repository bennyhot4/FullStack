// Loads data for a page or section: { status: "loading" | "error" | "done" } plus reload().
import { useCallback, useEffect, useState } from "react";

export type AsyncState<T> =
  | { status: "loading" }
  | { status: "error"; error: unknown }
  | { status: "done"; data: T };

type Stored<T> = AsyncState<T> & { key: unknown; run: number };

/**
 * Runs `load` on mount and whenever `key` changes. `reload()` shows the loading state again and re-runs it.
 * While a new key or a reload is loading, the state is "loading" (old data is not shown for the new key).
 * A result that arrives after unmount or after a newer run is dropped.
 */
export function useAsync<T>(load: () => Promise<T>, key: unknown = null) {
  const [run, setRun] = useState(0);
  const [stored, setStored] = useState<Stored<T>>({ status: "loading", key, run });

  useEffect(() => {
    let live = true;
    load().then(
      (data) => { if (live) setStored({ status: "done", data, key, run }); },
      (error: unknown) => { if (live) setStored({ status: "error", error, key, run }); },
    );
    return () => { live = false; };
    // `load` is a new function on every render; only `key` and reload() decide when to run it again.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, run]);

  const reload = useCallback(() => setRun((n) => n + 1), []);

  /** Replaces the loaded data without a request (e.g. after a local change). */
  const setData = useCallback((data: T) => setStored((s) => ({ status: "done", data, key: s.key, run: s.run })), []);

  const state: AsyncState<T> = Object.is(stored.key, key) && stored.run === run ? stored : { status: "loading" };
  return { state, reload, setData };
}
