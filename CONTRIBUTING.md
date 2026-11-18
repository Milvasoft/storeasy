# Contributing to storeasy

Thanks for helping. This page covers the setup, the tests each package runs, and the house rules. By contributing you
agree that your contribution is licensed under the repository license, AGPL-3.0-only.

## Before you start

- Bugs and features: open an issue first (templates under **New issue**), so the approach is agreed before you write
  code. Small fixes (typos, docs) can go straight to a pull request.
- Security issues: do not open an issue, see [SECURITY.md](SECURITY.md).
- Be kind: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## Setup

Requirements: .NET 11 SDK, Node 22+, Docker (local dependencies and integration tests), Git.

```bash
git clone <repository url> storeasy
cd storeasy

# API dependencies: Redis, PostgreSQL, Seq, SeaweedFS
cd storeasy-api
docker compose up -d
dotnet user-secrets set "Storeasy:Encryption:MasterKey" "$(openssl rand -base64 32)" --project src/Storeasy.Api
dotnet user-secrets set "Milvasoft:Identity:Token:SymmetricPublicKey" "$(openssl rand -hex 32)" --project src/Storeasy.Api
dotnet run --project src/Storeasy.Api      # http://localhost:5000/api/documentation

# Web app (another terminal)
cd storeasy-app
npm install
cp .env.example .env
npm run dev                                # http://localhost:5173

# Client tools
cd tools
npm install
```

Package guides: [storeasy-api](storeasy-api/README.md), [storeasy-app](storeasy-app/README.md),
[tools](tools/README.md), [rules](rules/README.md).

## Tests

Run the checks of every package you touched; CI runs all of them ([.github/workflows/ci.yml](.github/workflows/ci.yml)).

| Package | Commands |
|---|---|
| `storeasy-api` | `dotnet build Storeasy.slnx -c Debug` then `dotnet test --solution Storeasy.slnx --no-build` (Docker must run: the integration tests start PostgreSQL and Redis with Testcontainers). Unit tests only: `dotnet test --project tests/Storeasy.UnitTests` |
| `storeasy-app` | `npm run lint`, `npm run typecheck`, `npm test`, `npm run build` |
| `tools` | `npm run lint`, `npm test`, `npm run build` (commit the rebuilt bundles; `lint` fails on a stale bundle) |
| `rules` | `node rules/validate.mjs` |

New behaviour comes with tests: unit tests for logic, integration tests for endpoints (including "another
organization's record answers 404"), Vitest and Testing Library for the web app. Tests never reach the network:
stores, mail and storage are faked.

## House rules

- **No secrets.** Never commit keys, tokens, passwords, `.p8` files, service account JSON or real `.env` files. Use
  user secrets locally and environment variables in containers; appsettings carry empty values. CI scans every push
  with gitleaks. Use obviously fake values in tests and docs.
- **No private data.** No real app ids, bundle ids, internal host names or customer data in code, tests or docs.
- **No em or en dashes** (U+2014, U+2013) anywhere: code, comments, docs, dictionaries, commit messages. Use a colon,
  a comma, parentheses or a new sentence. CI checks it.
- **Two languages.** Every user facing text exists in Turkish and English: API messages in both resx files
  (`SharedResource.tr-TR.resx`, `SharedResource.en-US.resx`), web texts in `storeasy-app/src/lib/i18n` (`tr.ts`,
  `en.ts`). No hard coded user facing text; tests enforce it.
- **.NET files** (`*.cs`, `*.csproj`, `*.resx`, appsettings) use CRLF line endings and UTF-8 with BOM, like the
  existing files (`.editorconfig` sets CRLF for C#). Shell scripts and Dockerfiles use LF. Follow the conventions in the
  [API README](storeasy-api/README.md#conventions); the architecture tests check most of them.
- **Web and tools** follow ESLint and Prettier (`npm run format` in `storeasy-app`). Source files in `tools/` start with
  `SPDX-License-Identifier: AGPL-3.0-only`.
- **Rules catalog** changes cite the official source and bump `catalogVersion` ([rules/README.md](rules/README.md)).
- **Store safety.** Nothing may write to a store without a change plan, a role check and the audit entry; review
  submission and production stay behind a typed confirmation. Pull requests that bypass this are not accepted.

## Commits and pull requests

- Plain commits, no sign-off line required. Write the subject in the imperative, about what changed and why
  ("Refuse production tracks in the delivery override"), not "fix" or "update".
- Keep a pull request to one topic. Fill in the template: what changed, how it was tested, screenshots for UI.
- Update the docs and the `README` of the package when behaviour or configuration changes.
- CI must be green before review.
