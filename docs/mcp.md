# MCP

storeasy exposes its features to MCP clients (Claude Code, Claude Desktop and others). Every tool sends the same
application request as the matching REST endpoint, so roles, validation, diffs, dry runs, confirmations and the audit
log are identical; audit entries record the channel as MCP.

## Two ways to connect

| | Remote MCP | Local bridge `storeasy-mcp` |
|---|---|---|
| Transport | Streamable HTTP at `https://<your storeasy>/mcp` (stateless) | stdio, runs on your machine, forwards to the remote `/mcp` |
| Tools | 141 | The same 141, plus `artifact_upload` |
| Local files | No: file arguments take asset ids | Yes: file and folder arguments take local paths; the bridge uploads them first |
| Needs | A personal access token | Node 22+ and a personal access token (or `storeasy login`) |

Use the bridge when you work with screenshot folders or build files on your machine; otherwise remote is enough. The
bridge is the `storeasy-mcp` command of the `storeasy` npm package ([CLI README](../tools/storeasy-cli/README.md)):
`npx -y -p storeasy storeasy-mcp`.

## Personal access token

Create one in storeasy under **Developer > MCP and tokens**, from a signed in web session (a token cannot create or
revoke tokens). A token:

- starts with `sea_` and is shown once; storeasy keeps only its SHA-256 hash and a short prefix;
- is bound to you and one organization; on each request its permission is your current role in that organization
  (re-read per request), so a role change or removal applies at once, and the token's organization replaces any
  organization id in a call;
- can be read only (capped to the Viewer permissions; Viewers can only create read only tokens) and expires after 30,
  90 or 365 days;
- can be revoked at any time; a revoked or expired token answers 401.

`/mcp` accepts personal access tokens only: a web session token, a CI token or an upload token is refused. Keep the
token in your environment or a password manager, never in a committed file.

## Claude Code

Remote:

```bash
claude mcp add --transport http storeasy https://storeasy.example.com/mcp \
  --header "Authorization: Bearer $STOREASY_TOKEN"
```

Local bridge:

```bash
claude mcp add storeasy-local -e STOREASY_URL=https://storeasy.example.com -e STOREASY_TOKEN="$STOREASY_TOKEN" \
  -- npx -y -p storeasy storeasy-mcp
```

Or in a project `.mcp.json` (Claude Code expands `${STOREASY_TOKEN}` from your environment, so the file holds no
secret):

```json
{
  "mcpServers": {
    "storeasy": {
      "type": "http",
      "url": "https://storeasy.example.com/mcp",
      "headers": { "Authorization": "Bearer ${STOREASY_TOKEN}" }
    },
    "storeasy-local": {
      "command": "npx",
      "args": ["-y", "-p", "storeasy", "storeasy-mcp"],
      "env": { "STOREASY_URL": "https://storeasy.example.com", "STOREASY_TOKEN": "${STOREASY_TOKEN}" }
    }
  }
}
```

Use one of the two entries, not both.

## Claude Desktop

Claude Desktop starts local servers from `claude_desktop_config.json` (macOS:
`~/Library/Application Support/Claude/claude_desktop_config.json`, Windows: `%APPDATA%\Claude\claude_desktop_config.json`).
Use the bridge, and sign in once with the CLI so the file holds no token:

```bash
npx -y storeasy login --url https://storeasy.example.com
```

```json
{
  "mcpServers": {
    "storeasy": {
      "command": "npx",
      "args": ["-y", "-p", "storeasy", "storeasy-mcp"],
      "env": { "STOREASY_URL": "https://storeasy.example.com" }
    }
  }
}
```

Without `STOREASY_TOKEN` the bridge uses the token saved by `storeasy login` (owner-only file, see the
[CLI README](../tools/storeasy-cli/README.md#sign-in)). Restart Claude Desktop after editing the file.

## Tools

Start with `apps_summary`: one call answers the state, version, readiness and what is missing of every app in every
store. The full list with input schemas comes from the server (`tools/list`); the catalog is
`storeasy-api/src/Storeasy.Api/Mcp/McpToolCatalog.cs` and `McpCustomTools.cs`.

| Area | Tools | Examples |
|---|---|---|
| Apps and overview | 10 | `apps_summary`, `apps_list`, `apps_get`, `apps_sync`, `apps_add_google`, `apps_link_store_record`, `apps_profile_update`, `apps_hide`, `apps_unhide` |
| Readiness and compliance | 3 | `readiness_check`, `compliance_app`, `compliance_list` |
| Listings, media, categories, age rating, pricing, review info | 18 | `listings_get`, `listings_plan`, `screenshots_sets`, `screenshots_plan`, `screenshots_upload_dir`, `previews_plan`, `categories_plan`, `age_rating_plan`, `pricing_plan`, `review_info_plan` |
| Builds and releases | 10 | `builds_list`, `builds_link`, `builds_export_compliance`, `bundle_upload_plan`, `releases_plan`, `rollout_plan`, `testers_plan`, `submit_for_review` |
| Saved answers and assisted steps | 8 | `answers_get`, `answers_update`, `data_safety_plan`, `assisted_steps_list`, `assisted_step_handoff`, `assisted_step_complete` |
| Change plans and jobs | 8 | `plans_list`, `plans_get`, `plans_approve`, `plans_reject`, `plans_discard`, `jobs_list`, `jobs_get`, `jobs_cancel` |
| Rules, connections, audit, members (read only) | 6 | `rules_catalog`, `rules_list`, `rules_get`, `connections_list`, `audit_list`, `members_list` |
| Package intake | 5 | `artifacts_summary`, `artifacts_sessions`, `artifacts_session_get`, `artifacts_deliver_held`, `delivery_rules_get` |
| Reviews | 9 | `reviews_list`, `reviews_sync`, `reviews_reply_draft`, `reviews_reply`, `reviews_saved_replies` |
| Rejections | 9 | `rejections_list`, `rejections_get`, `rejections_paste_text`, `rejections_checklist_update`, `rejections_reply_draft`, `rejections_close` |
| TestFlight | 12 | `testflight_groups`, `testflight_testers_update`, `testflight_add_build`, `testflight_what_to_test`, `testflight_beta_review` |
| Microsoft Store | 11 | `ms_product_add`, `ms_product_get`, `ms_content_get`, `ms_change_plan`, `ms_submissions_list`, `ms_flights_list`, `ms_installer_check`, `ms_submit` |
| Subscriptions, in-app purchases, purchase settings | 19 | `subscriptions_groups`, `subscriptions_prices_convert`, `subscriptions_plan`, `subscriptions_apply`, `iap_list`, `iap_plan`, `iap_apply`, `products_sync`, `sandbox_testers_plan`, `server_notifications_plan` |
| Studio | 13 | `mockups_templates_list`, `mockups_template_create`, `mockups_captions_save`, `mockups_preview`, `mockups_export`, `mockups_export_manifest`, `mockups_export_to_screenshot_plans` |

Not in MCP on purpose: adding, verifying or deleting store connections, creating or revoking tokens (personal, CI,
upload), webhooks, delivery rules and intake settings, member and role management, and the instance admin endpoints. Those stay in the web app.

## How file arguments work

A remote tool that takes files marks those arguments with `x-storeasy-upload` (kind `file`, `files` or `folder`), for
example `screenshots_plan`, `screenshots_upload_dir`, `previews_plan` and `bundle_upload_plan`. The bridge shows them to
your client as paths, uploads the files to storeasy's storage with presigned URLs, and calls the remote tool with the
asset ids. Folders are read in natural name order (`2.png` before `10.png`), not recursively. The bridge's own
`artifact_upload` sends an IPA, AAB, APK or MSIX from disk through [CI intake](ci-intake.md). Details:
[CONTRACT section 6](../tools/CONTRACT.md#6-remote-mcp-mcp-plan-26-and-117-design-1615).

## Confirmation rules

The same rules as the web app, enforced on the server:

- **Reads** (apps, readiness, plans, jobs, rules, compliance) need any role.
- **Store writes are plans first.** Every `*_plan` tool always runs as a dry run: it reads the store live and returns a
  change plan (the diff, warnings and the store calls it would make) with its id; nothing is written. `plans_approve`
  (or `subscriptions_apply`, `iap_apply`) applies it, checked against the plan's own permission, and storeasy reads the
  store again: if the touched values changed meanwhile, the approval is refused (409) and a new plan is needed. Plans
  expire after 48 hours.
- **Typed confirmation.** Review submission and production releases need the matching permission (`SubmitForReview`,
  `ReleaseProduction`; Admin and Owner by default), `confirm: true` and `confirmText` typed exactly as the plan's
  `confirmText` (app name and version). `submit_for_review` takes the same pair directly, `ms_submit` needs the
  product's Store id, and `testflight_beta_review` needs `confirm: true` and the build's "version (build)" text.
- **storeasy data writes** (saved answers, saved replies, rejection checklists, mockup templates, review reply drafts)
  are immediate, like in the web app; they never touch a store.
- **Read only tokens** can call read tools only (a write answers `OrganizationForbidden`).
- **Assisted steps** (work the stores offer no API for) return a checklist, links and a prepared prompt for a browser
  agent in your own session. storeasy and its tools never store or type a store password; the store session stays
  yours.

Keep your MCP client's permission prompts on for storeasy's write tools: approving a plan is an action on your store.

## Troubleshooting

| Symptom | Check |
|---|---|
| 401 or 403 | Token expired or revoked, the `Authorization` header is missing, or it is not a personal access token. |
| Tool error `OrganizationForbidden` | The token is read only or your role lacks the permission. |
| Tool error `AssetNotFound` | You passed a path to the remote server: use the bridge, or pass asset ids. |
| Tool error about the confirmation | `confirm` is missing or `confirmText` does not match the plan's `confirmText` exactly. |
| Bridge does not start | Node 22+ on `PATH`; run `npx -y -p storeasy storeasy-mcp` in a terminal to see the error. |
