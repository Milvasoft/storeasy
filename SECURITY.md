# Security policy

storeasy holds credentials that can change apps in public stores, so we take reports seriously and thank everyone who
reports responsibly.

## Reporting a vulnerability

Please do **not** open a public issue, discussion or pull request for a security problem.

Report it privately, by either:

- GitHub private vulnerability reporting: **Security > Report a vulnerability** on this repository, or
- e-mail to `SECURITY_CONTACT_EMAIL` <!-- The maintainers replace this placeholder with the security contact address before the repository is made public. -->

Include what you can: the affected component (`storeasy-api`, `storeasy-app`, `tools`, `deploy`), version or commit,
steps to reproduce, impact, and any proof of concept. Never include real store credentials or other people's data;
use test accounts.

## What to expect

| Step | Target |
|---|---|
| Acknowledgement | within 3 working days |
| First assessment (severity, affected versions) | within 10 working days |
| Fix or mitigation for critical and high issues | as fast as possible, usually within 30 days |
| Public advisory | after a fix is available, crediting you unless you prefer otherwise |

Please give us a reasonable time to fix the issue before disclosing it.

## Scope

In scope: the code in this repository and the default configuration in `deploy/`. Particularly interesting:

- access to another organization's data, or actions beyond a role's permissions;
- exposure of store credentials, tokens, presigned URLs or the master key (API responses, logs, MCP results, CI output);
- bypassing change plans, typed confirmations or the audit log;
- webhook signature or OIDC trusted publisher bypasses, SSRF through artifact downloads.

Out of scope: vulnerabilities in a self-hosted instance caused by its operator's configuration (for example exposing the
API without a reverse proxy, or weak secrets), denial of service by volume, and reports from automated scanners without
a demonstrated impact.

## Supported versions

storeasy is not released yet. Until the first release, fixes land on the default branch only.

## Safe harbor

We will not take legal action against research done in good faith within this policy: no data destruction, no access
to data that is not yours beyond what is needed to show the issue, and no testing against stores or accounts you do
not own.
