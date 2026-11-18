# UI and Styling

## Building blocks — reuse, do not add libraries
- Toasts: `toast(message, type)` from `frontend/src/ui` — concise and action-oriented.
- Icons: `<Icon name="..." />` from `src/ui/icon.tsx` (inline SVG paths in `PATHS`) — reuse the same icon name
  for the same concept; add a path there rather than pulling an icon library.
- Dialogs: `openSheet` / `confirmSheet` (bottom sheet on phones, modal on desktop).
- Every visible string goes through `t(key)` with both `en` and `he` filled in `src/i18n.ts`
  (a missing key fails `npm run typecheck`).

## Styling
- Plain CSS in `frontend/css/base.css` (tokens, layout) and `frontend/css/components.css`.
  Do not add a CSS framework or new stylesheet files without a reason in the plan.
- Colors and shared sizes come from CSS variables on `:root`. `--primary`, `--background` and
  `--card` are overwritten at runtime from the admin Branding settings, so never hard-code them.
- **Mobile-first:** base styles for 360px, then `@media (min-width: 640px)` and `(min-width: 1024px)`.
  No horizontal scroll at 360px.
- **RTL:** CSS logical properties only (`margin-inline-start`, `padding-inline`, `inset-inline-end`);
  direction-dependent icons get the `flip-rtl` class.
- Touch targets ≥ 44×44px, form inputs ≥ 16px font, use `100dvh` not `100vh`.
