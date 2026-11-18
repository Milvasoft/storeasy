# storeasy

Manage App Store Connect and Google Play from one place, through the official APIs: listings, screenshots,
builds, releases, reviews. Every write is diffed, dry-run first, role checked and audited, and you can watch it
happen live. An MCP endpoint lets Claude (or any MCP client) do everything the web app does, with the same checks.

> Status: early development (phase 1 of the plan in `docs/urun-plani.md`). Not ready for use.

## Layout

| Folder | What |
|---|---|
| `storeasy-api/` | .NET API: auth, organizations and roles, encrypted store connections, store engine, jobs, SignalR, webhooks, MCP |
| `storeasy-app/` | React web app (see `storeasy-app/README.md`) |
| `docs/` | Product plan and decisions (Turkish) |

## Local development

Requirements: .NET 11 SDK, Docker.

```bash
cd storeasy-api
docker compose up -d                      # Redis, PostgreSQL, Seq
dotnet user-secrets set "Storeasy:Encryption:MasterKey" "$(openssl rand -base64 32)" --project src/Storeasy.Api
dotnet user-secrets set "Milvasoft:Identity:Token:SymmetricPublicKey" "$(openssl rand -hex 32)" --project src/Storeasy.Api
dotnet run --project src/Storeasy.Api
```

API docs: `http://localhost:5000/api/documentation`.

## Security

- Store credentials (App Store Connect `.p8` keys, Google service account JSON) are encrypted at rest with
  AES-256-GCM envelope encryption. The master key comes from the environment, never from a file in the repo.
- Credentials are never returned by any endpoint; only a fingerprint (key id, service account e-mail) is shown.
- Submitting for review and production releases need an explicit confirmation and a role that allows it.

## License

GNU Affero General Public License v3.0 (AGPL-3.0-only), see [LICENSE](LICENSE). If you run a modified storeasy as a
network service, you must offer its source to the users of that service.
