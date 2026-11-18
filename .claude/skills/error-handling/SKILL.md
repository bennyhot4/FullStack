---
name: error-handling
description: Design or review how errors are detected, classified, logged, and returned. Use when adding an API error response, choosing an HTTP status code or error code, writing retry logic, adding error logging, or surfacing a failure in the UI. Covers the {code, message} error shape, the Api.* helpers, status codes, logging redaction, and required failure-path tests.
---

# Error Handling

## Principles
- Fail fast on invalid input. The server enforces every rule (booking hours, days ahead,
  overlaps, terms, roles) — the browser's checks are only UX.
- Return safe, actionable messages to clients; keep internal details in logs.
- Use one error shape and status-code convention everywhere.

## API response shape
Every non-2xx response from an endpoint is:

```json
{ "code": "slot_taken", "message": "One or more of the selected hours were just booked." }
```

`message` is optional English detail and defaults to the code itself; the UI shows translated
text for the codes it knows.

Produce it only through the helpers in `backend/HourOffice.Api/Endpoints/Api.cs`:
`Api.BadRequest(code)`, `Api.NotFound()`, `Api.Conflict(code)`, `Api.Forbidden(code)`,
`Api.Error(status, code)`. Unhandled exceptions go through `AddProblemDetails()` — never
return exception text, stack traces, Mongo errors or tokens in a response.

`code` is `snake_case` and stable — the frontend switches on it, so renaming a code is a
breaking change.

## Status codes
| Code | Use for |
|---|---|
| `400` | Malformed or invalid input (`invalid_phone`, `invalid_email`) |
| `401` | No/invalid token, or the token's user no longer exists |
| `403` | Signed in but not allowed (`admin_required`, `user_not_registered`, `profile_required`, `terms_required`) |
| `404` | Missing resource (also for ids that are not valid ObjectIds) |
| `409` | State conflict (`slot_taken`, `user_exists`, `cannot_demote_self`) |
| `429` | Rate limit (the `"auth"` policy) |
| `500` | Unexpected internal error |

Catch unique-index violations with `catch (Exception ex) when (MongoContext.IsDuplicateKey(ex))`
and return a `409` — e.g. the `uniq_confirmed_slot` index → `slot_taken`, unique email → `user_exists`.

## Frontend
- `src/api.ts` throws `ApiError(status, code, message)`; `asApiError(e)` narrows a caught value. Network failures become `network_error`.
- Handle the codes a screen can recover from explicitly (`e.code === "slot_taken"` → translated
  toast + reload slots); otherwise show `e.message` or `t("loadingFailed")`.
- Every user-facing message is a `t(key)` with `en` and `he`. Show it via `toast` or an inline
  error under the field; full-page load failures use `errorBox(err, retry)`.

## Logging
- Log unexpected errors with the operation and relevant ids; never log tokens, refresh tokens,
  connection strings or Google credentials.

## Tests
Critical flows need failure-mode tests: validation errors, 401/403 boundaries, conflicts.
Assert the `code` in the body, not just the status.
