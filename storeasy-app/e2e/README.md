# storeasy-app e2e

Playwright against the real API: no `page.route`, no fake tokens. The app is built and served by `vite preview`.

1. Start the API with its dependencies (see `../storeasy-api/README` or the root README: `docker compose up -d`, then
   `dotnet run --project src/Storeasy.Api`).
2. Seed or create an account and pass it in the environment.
3. `npx playwright install chromium` once, then `npm run e2e`.

| Variable                              | Meaning                                                     | Default                 |
| ------------------------------------- | ----------------------------------------------------------- | ----------------------- |
| `E2E_API_URL`                         | API origin the build talks to                               | `http://localhost:5000` |
| `E2E_PORT`                            | Preview server port                                         | `4173`                  |
| `E2E_USER_EMAIL`, `E2E_USER_PASSWORD` | A seeded account; specs that need it are skipped without it | -                       |

Rules: selectors by role and label, Turkish UI (`locale: tr-TR`), one worker. Each test makes its own data once the
API can register accounts (the register endpoint is being built).
