// Minimal, safe Markdown renderer for the terms of use and the privacy policy:
// "## heading", "**bold**", "- list", paragraphs. Builds React elements (never HTML strings), so the
// content can never inject markup.
import type { ReactNode } from "react";

function inline(text: string): ReactNode[] {
  const out: ReactNode[] = [];
  const re = /\*\*(.+?)\*\*/g;
  let last = 0;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    if (m.index > last) out.push(text.slice(last, m.index));
    out.push(<strong key={m.index}>{m[1]}</strong>);
    last = m.index + m[0].length;
  }
  if (last < text.length) out.push(text.slice(last));
  return out;
}

export function Markdown({ source }: { source: string | null | undefined }) {
  const blocks: ReactNode[] = [];
  let list: ReactNode[] | null = null;
  let para: string[] = [];
  const flushPara = () => { if (para.length) { blocks.push(<p key={blocks.length}>{inline(para.join(" "))}</p>); para = []; } };
  const flushList = () => { if (list) { blocks.push(<ul key={blocks.length}>{list}</ul>); list = null; } };

  for (const raw of (source || "").split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) { flushPara(); flushList(); continue; }
    const heading = /^(#{1,3})\s+(.*)$/.exec(line);
    if (heading) { flushPara(); flushList(); blocks.push(<h2 key={blocks.length}>{inline(heading[2])}</h2>); continue; }
    const item = /^[-*]\s+(.*)$/.exec(line);
    if (item) { flushPara(); list ??= []; list.push(<li key={list.length}>{inline(item[1])}</li>); continue; }
    flushList();
    para.push(line);
  }
  flushPara(); flushList();
  return <div className="prose">{blocks}</div>;
}
