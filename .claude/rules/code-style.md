# Code Style

Match the surrounding code. The existing conventions are:

## JavaScript (`frontend/`, `e2e/`)
- Native ES modules, no framework, no bundler, no npm dependencies in `frontend/`.
- Semicolons at the end of statements, double quotes, 2-space indent.
- Build DOM with `h(tag, attrs, ...children)` / `clear(el, ...)` from `js/ui.js` — no
  `innerHTML` anywhere (XSS). Markdown is rendered by `components/markdown.js`, which
  builds DOM nodes directly.
- Every file must pass `node --check` (that is the CI gate for the frontend).

## C# (`backend/`)
- .NET 8, nullable enabled, file-scoped namespaces, `sealed` classes, primary constructors.
- Minimal API endpoints grouped in `Endpoints/*Endpoints.cs` with a static `Map(WebApplication app)`.
- Time comes only from the injected `TimeProvider` — never `DateTime.Now` / `DateTime.UtcNow`.
- Pass the request `CancellationToken` to every MongoDB call.

## Hook scripts (`.claude/hooks/`)
- CommonJS (`require`), no trailing semicolons — kept as they came from the template.
