# Code Style

Match the surrounding code. The existing conventions are:

## TypeScript / React (`frontend/`)
- React 19 function components + hooks, TypeScript `strict`, built by Vite (plan 009). Runtime
  dependencies are only `react` and `react-dom`; anything else needs a reason in a plan.
- Semicolons at the end of statements, double quotes, 2-space indent.
- No `dangerouslySetInnerHTML` / `innerHTML` anywhere (XSS). Markdown is rendered by
  `src/components/markdown.tsx`, which builds React elements.
- No `any` (ESLint enforces it). API response shapes live in `src/types/api.ts`.
- `npm run typecheck`, `npm run lint` and `npm run build` must pass (the CI gate for the frontend).

## JavaScript (`e2e/`)
- Native ES modules, semicolons, double quotes, 2-space indent; every file passes `node --check`.

## C# (`backend/`)
- .NET 8, nullable enabled, file-scoped namespaces, `sealed` classes, primary constructors.
- Minimal API endpoints grouped in `Endpoints/*Endpoints.cs` with a static `Map(WebApplication app)`.
- Time comes only from the injected `TimeProvider` — never `DateTime.Now` / `DateTime.UtcNow`.
- Pass the request `CancellationToken` to every MongoDB call.

## Hook scripts (`.claude/hooks/`)
- CommonJS (`require`), no trailing semicolons — kept as they came from the template.
