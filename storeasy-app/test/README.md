# storeasy-app tests

Vitest + Testing Library + MSW. `npm run test`, `npm run test:coverage` (thresholds in `vitest.config.ts`).

1. Assert what the user sees: `getByRole`, `getByLabelText`, `findByText`; `data-testid` only for the location probe.
2. The API is always MSW: `test/support/handlers.ts` answers every endpoint of `src/lib/api/endpoints.ts`
   (`apiContract.test.ts` fails otherwise). One endpoint changes with `server.use(endpoint(...))`, which keeps the
   request recording. `fetch` is never mocked.
3. Data changes through `testState` (`testState.connections = []`), reset before every test.
4. DTOs come from `factories.ts`, typed with the app's own types.
5. The real provider stack: `renderWithProviders` (pages), `renderApp` (routing and the shell). Pages inside the shell
   wait for the organization list; the live stream is a manual source (`createManualSource`) the test pushes events to.
6. Test names describe behaviour. Turkish UI text is fine to assert (default language tr); some tests render English.
7. One behaviour per test; Arrange, Act, Assert separated by a blank line.
8. No em dash or en dash anywhere (`codeStyle.test.ts`).
9. A real bug found by a test is not hidden by weakening the test.
10. No "call and expect 200" tests: a test must break when its behaviour breaks.
