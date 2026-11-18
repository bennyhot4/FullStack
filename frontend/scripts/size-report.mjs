// Prints the size of every file in dist/ (raw and gzip). Informational only: always exits 0 (plan 009, Q7).
import { readdir, readFile } from "node:fs/promises";
import { join, relative } from "node:path";
import { gzipSync } from "node:zlib";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../dist/", import.meta.url));

async function* walk(dir) {
  for (const e of await readdir(dir, { withFileTypes: true })) {
    const p = join(dir, e.name);
    if (e.isDirectory()) yield* walk(p);
    else yield p;
  }
}

const kb = (n) => `${(n / 1024).toFixed(1)} KB`;
const rows = [];
try {
  for await (const file of walk(root)) {
    const buf = await readFile(file);
    rows.push({ file: relative(root, file).replaceAll("\\", "/"), raw: buf.length, gz: gzipSync(buf).length });
  }
} catch {
  console.log("dist/ not found - run `npm run build` first");
  process.exit(0);
}
rows.sort((a, b) => b.gz - a.gz);
for (const r of rows) console.log(`${kb(r.gz).padStart(10)} gz ${kb(r.raw).padStart(10)} raw  ${r.file}`);
const sum = (k, f = () => true) => rows.filter(f).reduce((s, r) => s + r[k], 0);
const js = (r) => r.file.endsWith(".js") && r.file.startsWith("app/");
console.log(`\nfiles: ${rows.length}   total: ${kb(sum("raw"))} raw / ${kb(sum("gz"))} gz   JS: ${kb(sum("gz", js))} gz`);
