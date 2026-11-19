# storeasy-app e2e

Playwright against the real product: no `page.route`, no fake tokens, no mocks. The usual target is the full self-host
stack (`deploy/docker-compose.yml` plus the e2e override `deploy/e2e/docker-compose.e2e.yml`): the production API and
web images, nginx with its single origin proxy, PostgreSQL, Redis, SeaweedFS, and Mailpit (MIT) catching every e-mail.

```bash
npx playwright install chromium         # once
npm run e2e:up                          # builds, starts, initializes the database (root password into the state dir)
E2E_BASE_URL=http://localhost:8080 E2E_ROOT_PASSWORD_FILE=<state dir>/root-password npm run e2e
npm run e2e:down                        # stops the stack and deletes its volumes
```

`e2e:up` runs `node ../deploy/e2e/stack.mjs up`: it writes a throwaway `.env` with random secrets into the state
directory (`E2E_STATE_DIR`, default `<os temp>/storeasy-e2e`, never the repository), starts the stack with registration
open and the schedules on, and initializes the database once like docs/self-host.md section 5 (a temporary
`MILVA_ENV=dev` container on `127.0.0.1:5080` with a bootstrap key generated at runtime, which `developer/database/init`
requires in the `X-Storeasy-Init-Key` header; the key is never written anywhere and dies with the container). `node ../deploy/e2e/stack.mjs env` prints the variables.

No store is ever called: the override resolves the App Store Connect, Google and Microsoft hosts to a refusing address
inside the API container, so a connection check or a sync fails fast. Store keys used by the specs are generated per run
(`support/stores.ts`) and belong to no account.

Without `E2E_BASE_URL` the app is built and served by `vite preview` against the API at `E2E_API_URL` (a `dotnet run`
API); the specs that read e-mails still need Mailpit at `E2E_MAILPIT_URL`.

| Variable                 | Meaning                                                                                               | Default                  |
| ------------------------ | ----------------------------------------------------------------------------------------------------- | ------------------------ |
| `E2E_BASE_URL`           | Web origin of the running stack (tests drive it directly)                                             | -                        |
| `E2E_API_URL`            | API origin of the `vite preview` build (when `E2E_BASE_URL` is unset)                                 | `http://localhost:5000`  |
| `E2E_PORT`               | Preview server port                                                                                   | `4173`                   |
| `E2E_MAILPIT_URL`        | Mailpit web/API address                                                                               | `http://localhost:8025`  |
| `E2E_ROOT_PASSWORD_FILE` | File with the root user's password; the platform admin spec skips without it (or `E2E_ROOT_PASSWORD`) | -                        |
| `E2E_STATE_DIR`          | Where `stack.mjs` keeps `.env` and `root-password`                                                    | `<os temp>/storeasy-e2e` |
| `E2E_MODE`               | Which suite runs: `multi` (`e2e/specs`), `single` (`e2e/single-team`) or `upgrade` (`e2e/upgrade`)    | `multi`                  |

## Deployment modes

`stack.mjs up --mode <mode>` starts the stack in one deployment mode (`Storeasy:Mode`); a mode other than the running
one, or `--fresh`, starts from a clean database. The compose project is `storeasy-e2e`, apart from a real install.

| Mode              | Stack                                                                                     | Suite             |
| ----------------- | ----------------------------------------------------------------------------------------- | ----------------- |
| `multi` (default) | MultiTenant, open registration, initialized by `developer/database/init` (root user)      | `e2e/specs`       |
| `single`          | SingleTeam as a real install starts: no user, the API logs a one time setup code          | `e2e/single-team` |
| `upgrade`         | initialized as MultiTenant (only the root user, no organization), restarted as SingleTeam | `e2e/upgrade`     |

```bash
node ../deploy/e2e/stack.mjs up --mode single
E2E_MODE=single E2E_BASE_URL=http://localhost:8080 npm run e2e
node ../deploy/e2e/stack.mjs up --mode upgrade
E2E_MODE=upgrade E2E_BASE_URL=http://localhost:8080 E2E_ROOT_PASSWORD_FILE=<state dir>/root-password npm run e2e
```

The single team suite reads the setup code where an operator does, from `docker compose logs storeasy-api`
(`stack.mjs setup-code`); it is never written to a file. It runs the setup in the UI (a wrong code first), then checks
one team without a switcher, no sign-up, the merged Settings menu, every sidebar link (address, heading and breadcrumb,
also after the page loaded), a second setup (409) and a second organization (409) over the API, an invitation accepted
by a new account in a second browser, the English UI with the system status, the health probes and that the API
documentation is 404 with `MILVA_ENV=prod`. The upgrade suite signs the root user in, creates the team in the one-step
screen and opens every sidebar link.

What the multi tenant specs cover: sign-up, the verification e-mail and the first organization (Turkish UI); invitation by e-mail
accepted in a second browser, role changes, a custom role, organization settings and the audit log (English UI);
personal access token in the UI, `/mcp` initialize and tools/list (139 tools) and `tokens/current`; CI token; the store
connection form and a check that fails without breaking anything (the key is never returned); the studio (template from
a starter, server preview, export plan); rules catalog, notification bell, live activity over the SignalR hub; the
root user on the system status page; health probes and webhook receivers through the web origin.

Rules: selectors by role, label and visible text; one worker; each test makes its own users and organization over the
API (`support/fixtures.ts`), the flow under test goes through the UI. Every API call and the browser send their own
`X-Forwarded-For` address (`support/api.ts`), so the per-address rate limits of sign-up and sign-in never mix tests; this
works because the e2e stack has no proxy in front of nginx. Screenshots, videos and traces of failures land in
`e2e/.artifacts`.
