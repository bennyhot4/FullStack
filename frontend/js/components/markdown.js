// Minimal, safe Markdown renderer for the terms of use: "## heading", "**bold**", "- list", paragraphs.
// Builds DOM nodes directly (no innerHTML), so content can never inject HTML.
import { h } from "../ui.js";

function inline(text) {
  const out = [];
  const re = /\*\*(.+?)\*\*/g;
  let last = 0, m;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(text.slice(last, m.index));
    out.push(h("strong", null, m[1]));
    last = m.index + m[0].length;
  }
  if (last < text.length) out.push(text.slice(last));
  return out;
}

export function renderMarkdown(md) {
  const root = h("div", { class: "prose" });
  let list = null;
  let para = [];
  const flushPara = () => { if (para.length) { root.append(h("p", null, inline(para.join(" ")))); para = []; } };
  const flushList = () => { if (list) { root.append(list); list = null; } };

  for (const raw of (md || "").split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) { flushPara(); flushList(); continue; }
    const heading = /^(#{1,3})\s+(.*)$/.exec(line);
    if (heading) { flushPara(); flushList(); root.append(h(heading[1].length === 1 ? "h2" : "h2", null, inline(heading[2]))); continue; }
    const item = /^[-*]\s+(.*)$/.exec(line);
    if (item) { flushPara(); list ??= h("ul"); list.append(h("li", null, inline(item[1]))); continue; }
    flushList();
    para.push(line);
  }
  flushPara(); flushList();
  return root;
}
