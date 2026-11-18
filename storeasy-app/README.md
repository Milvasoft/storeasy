# storeasy web app

The React web app of storeasy (AGPL-3.0). It talks to `storeasy-api` on the same origin: REST under `/api/v1.0`, the
live activity hub under `/hubs/activity` (SignalR), remote MCP under `/mcp`. Every response is the Milvasoft envelope
`Response<T>` `{ isSuccess, statusCode, messages[{ key, message, type }], data }` (lists add `currentPageNumber`,
`totalPageCount`, `totalDataCount`); the client turns camelCase keys into PascalCase (`pascalizeKeys`) and enums are
numeric. Built to `app-standarts/02-frontend-standartlari.md`; the design is `docs/storeasy uygulaması tasarımı/`.

## Stack

React 18, Vite 8, TypeScript 5 (strict), React Router 7, TanStack Query 5, Tailwind 3 with the design kit's CSS
variable tokens, lucide-react, `@fontsource` (Bricolage Grotesque, Onest, JetBrains Mono), `@floating-ui/react-dom`
(panel positioning), `@microsoft/signalr` (live activity). Tests: Vitest, Testing Library, MSW. e2e: Playwright.

## Getting started

```bash
npm install
cp .env.example .env
npm run dev                          # http://localhost:5173, API proxied to VITE_API_PROXY (default http://localhost:5000)
VITE_MOCK_API=true npm run dev       # no backend: the in-browser mock (sample data of the design, src/mocks)
```

With the mock, any e-mail signs in; the password `wrong` answers 401 and `busy` answers 429.

## Environment

Baked in at build time; a changed value means a new image.

| Var                | Meaning                                                               | When empty                                         |
| ------------------ | --------------------------------------------------------------------- | -------------------------------------------------- |
| `VITE_API_URL`     | API origin                                                            | Same origin: Vite proxy in dev, nginx in the image |
| `VITE_API_PROXY`   | Dev proxy target                                                      | `http://localhost:5000`                            |
| `VITE_SOURCE_URL`  | AGPL-3.0 section 13 "Source code" link in the footer and account menu | Upstream repository                                |
| `VITE_MOCK_API`    | Dev only: answer the API and the hub in the browser                   | Real API                                           |
| `VITE_APP_VERSION` | Version shown in the menu footer                                      | `package.json` version                             |

## Scripts

`dev`, `build` (`tsc && vite build`), `typecheck`, `lint`, `format`, `format:check`, `test`, `test:watch`,
`test:coverage` (CI thresholds), `e2e`, `e2e:report`, `preview`.

## Project layout

```
src/
  App.tsx                 routes: guest pages, setup, the shell layout (lazy pages), not found
  components/ui           design system primitives (kit classes in index.css)
  components/layout       AppShell, Sidebar, OrgSwitcher, UserMenu, AuthLayout, Logo
  components/<domain>     activity (Hat), connections, members, org, auth
  pages/                  one page per route; SectionPages renders sections whose API is not built yet
  lib/api                 http.ts, endpoints.ts, types.ts, tokenStore.ts, list.ts, textLimits.ts, pascalize.ts
  lib/activity            the live event contract, SignalR / mock / manual sources, the pure Hat reducer
  lib/org                 current organization and permissions, role rules, audit labels
  lib/features            section flags (off until a section's API ships)
  mocks/                  dev-only in-browser API (never in the production bundle)
test/                     mirrors src; support/ has the MSW handlers, state, factories, render helpers
```

## Auth flow

Sign-in by e-mail (`POST Account/login`, `UserName` = e-mail). Tokens live in `localStorage`; every request carries the
bearer token and `Accept-Language`. A 401 or 419 triggers one shared `Account/login/refresh` (the expired token goes
in the header) and the request is replayed with the same `Idempotency-Key`; a failed refresh clears the token, fires
`storeasy:auth-lost`, and the sign-in screen says the session ended and returns to the page afterwards. The React
Query cache is cleared at every session boundary. The current organization is remembered in this browser; its id goes
with every organization request, and the API checks the membership each time.

Registration, e-mail verification and password reset screens use the planned contract (`Account/register`,
`Account/email/verify`, `Account/email/verify/resend`, `Account/password/forgot`, `Account/password/reset`), kept in one
block of `endpoints.ts` with MSW handlers, so adjusting it to the final API is local.

## Live activity

`lib/activity/events.ts` is the typed contract of `/hubs/activity` (`job.created`, `job.step`, `job.finished`,
`webhook.received`, `review.received`, planned `artifact.progress`). The SignalR source connects with the access token
(`access_token` query, K5) and the organization as a query parameter, retries after 0, 2, 10, 30 s and then every 30 s
for five minutes. `hatReducer` turns events into lanes; the Hat scene animates only what is on the way; with reduced
motion a status list replaces it. In development an unreachable hub falls back to a clearly marked demo stream;
production never shows made-up events.

## Storage keys and events

| Key (`storeasy.` prefix) | What                                            |
| ------------------------ | ----------------------------------------------- |
| `auth.tokens`            | Access and refresh token, user id, user type    |
| `device-id`              | Random id per browser (sessions are per device) |
| `lang`, `theme`          | UI preferences                                  |
| `org.current`            | Last used organization                          |
| `auth.last-email`        | Fills the sign-in e-mail after a session ends   |

Window events: `storeasy:auth-lost`, `storeasy:api-refusal`, `storeasy:theme`. No analytics, no tracking, so no consent
banner (design 5.34).

## Deployment

`Dockerfile` builds a linux/arm64 nginx image. nginx serves the SPA (index.html no-cache, hashed assets for a year,
security headers) and proxies `/api`, `/hubs` (WebSocket upgrade) and `/mcp` to `STOREASY_API_UPSTREAM` (runtime env,
default `http://storeasy-api:8080`), so the product is one origin (plan section 10).

## Decisions

Differences from `02-frontend-standartlari.md`, with the reason:

- **Design system from the kit.** Tokens and component classes are ported from the design kit (`kit.css`) into
  `index.css` `@layer components` (`.btn`, `.inp`, `.card`, `.bdg`...), so the app looks exactly like the design;
  React primitives wrap them. Radius scale and motion tokens follow the design (19 section 4), not the 02 example.
- **Theme.** `.dark` on `<html>`, light/dark/system; `--accent-fill` equals `--accent` (Ultramarine is 6.5:1 under white).
- **Feature flags are in code.** There is no anonymous flag endpoint for app users yet; `FeatureFlagsProvider` holds the
  section switches (all off) and pages behind them render the designed first-use state with a "coming" mark.
- **No server-side preferences yet.** Language and theme are kept in the browser; `PUT account/preferences` and
  `PUT account/profile` are planned on the API.
- **`@floating-ui/react-dom`** positions every panel (02 section 9.4 names Floating UI).
- **`@microsoft/signalr`** for the live hub (plan K5).
- **No terms checkbox and no 6-digit code on sign-up / verification.** The legal documents and the code path are not in
  the API contract yet; the design drops them when absent (5.14, 5.33).
- **Coverage thresholds** start at the current coverage (77 / 72 / 77 / 68), below the Dump values, and rise with it.
- **e2e** has one sign-in journey against a seeded account; `e2e:up` / `e2e:down` come with an API image.
