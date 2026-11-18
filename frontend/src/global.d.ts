// Set by public/config.js, loaded as a plain script before the app (runtime config, not bundled).
interface Window {
  HOUROFFICE_CONFIG?: { apiBaseUrl?: string };
  google?: {
    accounts: {
      id: {
        initialize(options: { client_id: string; callback: (resp: { credential: string }) => void; ux_mode?: string }): void;
        renderButton(el: HTMLElement, options: Record<string, unknown>): void;
      };
    };
  };
}
