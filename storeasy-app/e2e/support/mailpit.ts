/**
 * The mail catcher of the e2e stack (Mailpit, deploy/e2e/docker-compose.e2e.yml): every e-mail the API sends lands
 * here. https://mailpit.axllent.org/docs/api-v1/
 */
export const MAILPIT_URL = process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025'

interface MessageSummary {
  ID: string
  Subject: string
  To: { Address: string }[]
  Created: string
}

interface Message {
  ID: string
  Subject: string
  HTML: string
  Text: string
}

/** Waits for the newest e-mail to `to` (optionally with a subject containing `subject`) and returns it. */
export async function waitForMail(to: string, subject?: string, timeoutMs = 60_000): Promise<Message> {
  const deadline = Date.now() + timeoutMs
  const query = encodeURIComponent(`to:"${to}"`)
  while (Date.now() < deadline) {
    const response = await fetch(`${MAILPIT_URL}/api/v1/search?query=${query}`)
    if (response.ok) {
      const list = (await response.json()) as { messages: MessageSummary[] }
      const match = list.messages.find((m) => !subject || m.Subject.includes(subject))
      if (match) {
        return (await (await fetch(`${MAILPIT_URL}/api/v1/message/${match.ID}`)).json()) as Message
      }
    }
    await new Promise((r) => setTimeout(r, 1000))
  }
  throw new Error(`No e-mail to ${to}${subject ? ` about "${subject}"` : ''} within ${timeoutMs} ms`)
}

/** Every e-mail to an address, newest first. */
export async function mailsTo(to: string): Promise<MessageSummary[]> {
  const response = await fetch(`${MAILPIT_URL}/api/v1/search?query=${encodeURIComponent(`to:"${to}"`)}`)
  return ((await response.json()) as { messages: MessageSummary[] }).messages
}

/** The first link of the e-mail whose path starts with `pathPrefix` (e.g. "/verify-email"). */
export function linkIn(message: Message, pathPrefix: string): string {
  const links = [...message.HTML.matchAll(/href="([^"]+)"/g)].map((m) => m[1].replace(/&amp;/g, '&'))
  const link = links.find((href) => {
    try {
      return new URL(href).pathname.startsWith(pathPrefix)
    } catch {
      return false
    }
  })
  if (!link) {
    throw new Error(`No ${pathPrefix} link in "${message.Subject}": ${links.join(', ')}`)
  }
  return link
}
