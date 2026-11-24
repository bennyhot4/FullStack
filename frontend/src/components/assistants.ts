// Plan 012: how each AI assistant adds HourOffice. The step texts live in i18n (vendors rename their menus often, so
// fixing a guide is a text change); this file holds only what is built from the MCP address.
import type { TranslationKey } from "../i18n";

export type AssistantId = "claude" | "chatgpt" | "gemini" | "codex" | "cursor" | "vscode" | "other";

export interface Assistant {
  id: AssistantId;
  /** Shown as text: no vendor logos in the repo. */
  name: string;
  steps: TranslationKey;
  /** A limit worth knowing before starting (paid plan, country...). */
  note?: TranslationKey;
  /** A button that opens the assistant itself: the install link, or the screen where connectors are added. */
  open?: (url: string) => { href: string; label: TranslationKey };
  /** One line to copy and paste in a terminal. */
  command?: (url: string) => string;
}

const SERVER_NAME = "HourOffice";

/** Base64 of UTF-8 JSON (Cursor's install link). */
const base64Json = (value: unknown) => btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))));

export const ASSISTANTS: Assistant[] = [
  {
    id: "claude", name: "Claude", steps: "connectStepsClaude", note: "connectNoteClaude",
    open: () => ({ href: "https://claude.ai/settings/connectors", label: "connectOpenSettings" }),
  },
  {
    id: "chatgpt", name: "ChatGPT", steps: "connectStepsChatGPT", note: "connectNoteChatGPT",
    open: () => ({ href: "https://chatgpt.com/#settings/Connectors", label: "connectOpenSettings" }),
  },
  { id: "gemini", name: "Gemini", steps: "connectStepsGemini", note: "connectNoteGemini" },
  {
    id: "codex", name: "Codex", steps: "connectStepsCodex",
    command: (url) => `codex mcp add houroffice --url ${url}; codex mcp login houroffice`,
  },
  {
    id: "cursor", name: "Cursor", steps: "connectStepsCursor",
    open: (url) => ({
      href: `cursor://anysphere.cursor-deeplink/mcp/install?name=${SERVER_NAME}&config=${encodeURIComponent(base64Json({ url }))}`,
      label: "connectOpenApp",
    }),
  },
  {
    id: "vscode", name: "VS Code", steps: "connectStepsVSCode",
    open: (url) => ({
      href: `vscode:mcp/install?${encodeURIComponent(JSON.stringify({ name: SERVER_NAME, type: "http", url }))}`,
      label: "connectOpenApp",
    }),
  },
  { id: "other", name: "", steps: "connectStepsOther", note: "connectNoteOther" },
];
