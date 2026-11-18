# Self-hosting storeasy

> Draft. The full Docker guide arrives with phase 6 (see `urun-plani.md`). This page keeps the rules that already apply.

## License and network use

storeasy is licensed under AGPL-3.0-only (see `LICENSE` and `NOTICE`).

- Running an unmodified copy for your team needs nothing extra.
- If you change the code and let people use your version over a network, you must offer them the source of your
  version (AGPL section 13). The simplest way: publish your fork and point the "Source" link in the web app footer at it
  (`VITE_SOURCE_URL`, defaults to the upstream repository).

## Secrets you must provide

| Variable | What | How to create |
|---|---|---|
| `Storeasy__Encryption__MasterKey` | Seals stored App Store Connect keys and Play service accounts | `openssl rand -base64 32` |
| `Milvasoft__Identity__Token__SymmetricPublicKey` | Signs access tokens (at least 32 characters) | `openssl rand -hex 32` |
| `ConnectionStrings__DefaultConnectionString` | PostgreSQL 17+ | your database |
| `Milvasoft__Caching__Redis__ConnectionString` | Redis 7 (`host:port`) | your Redis |

Back up the master key outside the database. If it is lost, every stored store credential has to be entered again.
To rotate it, set a new key with a higher `Storeasy__Encryption__KeyVersion` and keep the old one under
`Storeasy__Encryption__RetiredKeys__<oldVersion>` until every connection has been re-saved.
