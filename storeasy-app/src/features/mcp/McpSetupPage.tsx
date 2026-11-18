import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Badge, type BadgeTone } from '@/components/ui/Badge'
import { Callout } from '@/components/ui/Feedback'
import { Select } from '@/components/ui/Select'
import { CardSkeleton } from '@/components/ui/primitives'
import { interpolate } from '@/lib/i18n/interpolate'
import { tokenKeys, tokensApi } from '@/features/tokens/api'
import { CopyButton, Snippet } from '@/features/tokens/ShownOnce'
import { DeveloperHeader } from '@/features/tokens/TokensPage'
import { useMcpText } from './i18n'
import { claudeCodeBridge, claudeCodeRemote, claudeDesktopJson, loginCommand, mcpJson, mcpUrl } from './snippets'

// Design 16.15 and docs/mcp.md: the remote MCP address of this installation, the snippets for Claude Code and Claude
// Desktop, the local bridge, the tool groups and the confirmation rules. The page never sees a token value: the snippets
// carry the chosen token's prefix as a placeholder, or the environment variable.

const ENV_TOKEN = '$STOREASY_TOKEN'

export function McpSetupPage() {
  const t = useMcpText()
  const tokens = useQuery({ queryKey: tokenKeys.list(), queryFn: tokensApi.list })
  const usable = (tokens.data ?? []).filter((token) => !token.IsExpired)
  const [tokenId, setTokenId] = useState<number | null>(null)
  const chosen = usable.find((token) => token.Id === tokenId) ?? null
  const placeholder = chosen ? `${chosen.Prefix ?? 'sea_'}…` : ENV_TOKEN

  const groups: Array<{ name: string; tools: string; tag: string; tone: BadgeTone }> = [
    { name: t.groups.read, tools: t.groups.readTools, tag: t.groups.readTag, tone: 'neutral' },
    { name: t.groups.content, tools: t.groups.contentTools, tag: t.groups.dryRun, tone: 'info' },
    { name: t.groups.release, tools: t.groups.releaseTools, tag: t.groups.dryRun, tone: 'info' },
    { name: t.groups.submit, tools: t.groups.submitTools, tag: t.groups.confirm, tone: 'warning' },
    { name: t.groups.feedback, tools: t.groups.feedbackTools, tag: t.groups.dryRun, tone: 'info' },
    { name: t.groups.assisted, tools: t.groups.assistedTools, tag: t.groups.record, tone: 'neutral' },
    { name: t.groups.local, tools: t.groups.localTools, tag: 'stdio', tone: 'accent' },
  ]

  return (
    <div className="grid gap-4">
      <DeveloperHeader count={tokens.data?.length} />
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,360px)]">
        <div className="grid content-start gap-4">
          <section className="card grid gap-3 p-4" aria-label={t.server.title}>
            <h2 className="text-[15px] font-semibold">{t.server.title}</h2>
            <dl className="grid gap-2 text-[13px]">
              <div className="grid gap-0.5">
                <dt className="text-xs text-fg3">{t.server.address}</dt>
                <dd className="flex items-center gap-2">
                  <code className="mono min-w-0 flex-1 break-all">{mcpUrl()}</code>
                  <CopyButton value={mcpUrl()} label={t.server.copyAddress} />
                </dd>
              </div>
              <div className="grid gap-0.5">
                <dt className="text-xs text-fg3">{t.server.transport}</dt>
                <dd>{t.server.transportValue}</dd>
              </div>
              <div className="grid gap-0.5">
                <dt className="text-xs text-fg3">{t.server.identity}</dt>
                <dd className="mono">{t.server.identityValue}</dd>
              </div>
            </dl>
            {tokens.isLoading ? (
              <CardSkeleton count={1} />
            ) : usable.length === 0 ? (
              <Callout tone="warning">
                {t.server.noToken}{' '}
                <Link className="lnk" to="/developer?create=1">
                  {t.server.createToken}
                </Link>
              </Callout>
            ) : (
              <label className="grid gap-1 text-[13px]">
                <span className="text-xs text-fg3">{t.server.token}</span>
                <Select<number>
                  aria-label={t.server.token}
                  value={tokenId ?? 0}
                  options={[
                    { value: 0, label: t.server.tokenNone },
                    ...usable.map((token) => ({
                      value: token.Id,
                      label: interpolate(t.server.tokenOption, { name: token.Name ?? `#${token.Id}`, prefix: token.Prefix ?? '' }),
                    })),
                  ]}
                  onChange={(value) => setTokenId(value === 0 ? null : value)}
                />
              </label>
            )}
          </section>

          <section className="card grid gap-4 p-4">
            <Snippet
              title={t.claudeCode.title}
              code={claudeCodeRemote(placeholder)}
              copyLabel={t.claudeCode.copy}
              hint={chosen ? t.server.placeholder : undefined}
            />
            <Snippet title={t.json.title} code={mcpJson()} copyLabel={t.json.copy} hint={t.json.hint} />
          </section>

          <section className="card grid gap-3 p-4" aria-label={t.desktop.title}>
            <h2 className="text-[15px] font-semibold">{t.desktop.title}</h2>
            <p className="text-[13px] text-fg2">{t.desktop.body}</p>
            <Snippet title={t.desktop.login} code={loginCommand()} copyLabel={t.desktop.copyLogin} />
            <Snippet title={t.desktop.config} code={claudeDesktopJson()} copyLabel={t.desktop.copyConfig} />
          </section>

          <section className="card grid gap-3 p-4" aria-label={t.bridge.title}>
            <h2 className="text-[15px] font-semibold">{t.bridge.title}</h2>
            <p className="text-[13px] text-fg2">{t.bridge.body}</p>
            <Snippet title="Claude Code" code={claudeCodeBridge(placeholder)} copyLabel={t.bridge.copy} />
          </section>
        </div>

        <div className="grid content-start gap-4">
          <section className="card grid gap-2 p-4" aria-label={t.rules.title}>
            <h2 className="text-[15px] font-semibold">{t.rules.title}</h2>
            <p className="text-[13px] text-fg2">{t.rules.body}</p>
            <ul className="grid list-disc gap-1.5 pl-5 text-[13px]">
              <li>{t.rules.reads}</li>
              <li>{t.rules.plans}</li>
              <li>{t.rules.submit}</li>
              <li>{t.rules.readOnly}</li>
              <li>{t.rules.assisted}</li>
            </ul>
            <Callout tone="info">{t.rules.prompts}</Callout>
          </section>

          <section className="card grid gap-2 p-4" aria-label={t.groups.title}>
            <h2 className="text-[15px] font-semibold">{t.groups.title}</h2>
            <ul className="grid gap-2.5">
              {groups.map((group) => (
                <li key={group.name} className="grid gap-0.5">
                  <span className="flex items-center justify-between gap-2">
                    <b className="text-[13px]">{group.name}</b>
                    <Badge small tone={group.tone}>
                      {group.tag}
                    </Badge>
                  </span>
                  <span className="mono break-words text-xs text-fg2">{group.tools}</span>
                </li>
              ))}
            </ul>
          </section>

          <section className="card grid gap-2 p-4" aria-label={t.troubleshooting.title}>
            <h2 className="text-[15px] font-semibold">{t.troubleshooting.title}</h2>
            <ul className="grid list-disc gap-1.5 pl-5 text-[13px]">
              <li>{t.troubleshooting.unauthorized}</li>
              <li>{t.troubleshooting.forbidden}</li>
              <li>{t.troubleshooting.assetNotFound}</li>
              <li>{t.troubleshooting.bridge}</li>
            </ul>
          </section>
        </div>
      </div>
    </div>
  )
}
