// Static SPA build only (plan 009): no SSR, no server runtime. `npm run build` writes dist/, which is what
// gets deployed to Cloudflare Pages. public/ (config.js, _redirects, _headers, sw.js, manifest, icon) is
// copied as is, so config.js stays a runtime file and one build serves every environment.
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "dist",
    assetsDir: "app", // hashed JS/CSS go to /app/* (immutable cache, see public/_headers)
    target: "es2020",
  },
  server: { port: 5500, strictPort: true },
  preview: { port: 5500, strictPort: true },
});
