# Milvasoft hosting (storeasy.milvasoft.com)

These files deploy the instance Milvasoft runs; they are not needed to self-host storeasy. Self-hosters use
`deploy/docker-compose.yml` and [docs/self-host.md](../../docs/self-host.md).

| File | What |
|---|---|
| `azure-pipeline-ci.yml` | Azure DevOps: on a master push, builds the linux/arm64 API and web images on the server's agent (no registry; the server's Docker daemon keeps them). Tests run in GitHub Actions. |
| `azure-pipeline-cd.yml` | Azure DevOps: after CI succeeds, writes the compose files and their environment to Dokploy and deploys the API, then the web app. Settings come from the `storeasy-test` variable group. |
| `docker-compose-api.yml`, `docker-compose-web.yml` | The two services on the shared `container-network` (shared PostgreSQL, Redis and Seq; AWS S3). |
| `dokploy-deploy.sh` | Sends a compose file and its environment to Dokploy, starts the deploy and waits for it. |

The ids in these files (Dokploy compose ids, agent pool) identify Milvasoft's own infrastructure; they grant nothing
without the secrets kept in Azure DevOps. Behind Cloudflare and Traefik the API runs with
`Storeasy__Network__ForwardedHops=3`.
