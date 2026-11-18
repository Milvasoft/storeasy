// Offline checks of store credentials before they are sent (design 14.5, 14.6, 14.8, 14.9). The API runs the same checks
// (AppleSecretInvalid, AppleKeyInvalid, GoogleSecretInvalid, GoogleKeyInvalid); these only save a round trip and let the
// user see which file they picked. The private key itself is never put on screen.

export const ISSUER_ID = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/
export const KEY_ID = /^[A-Z0-9]{10}$/

// "AuthKey_7QX2M4K9PL.p8" -> "7QX2M4K9PL".
export function keyIdFromFileName(name: string): string | null {
  const match = /AuthKey_([A-Za-z0-9]{10})\.p8$/i.exec(name)
  return match ? match[1].toUpperCase() : null
}

export type P8Problem = 'notPem' | 'notPrivateKey'

// An App Store Connect key is a PKCS#8 PEM ("BEGIN PRIVATE KEY") holding a P-256 key. The curve is checked on the API.
export function checkP8(content: string): P8Problem | null {
  const text = content.trim()
  if (!/^-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]+-----END [A-Z ]*PRIVATE KEY-----$/.test(text)) {
    return 'notPem'
  }
  if (!text.startsWith('-----BEGIN PRIVATE KEY-----')) {
    return 'notPrivateKey'
  }
  return null
}

export function appleSecret(issuerId: string, keyId: string, privateKey: string): string {
  return JSON.stringify({ issuerId: issuerId.trim(), keyId: keyId.trim().toUpperCase(), privateKey: privateKey.trim() })
}

export interface ServiceAccountInfo {
  clientEmail: string | null
  projectId: string | null
}

export type ServiceAccountProblem = 'notJson' | 'notServiceAccount' | 'noPrivateKey'

// Reads what can be shown (client_email, project_id) and says what is wrong; the readable fields are returned even
// when the file is refused, so the user understands which file they chose (design 14.9).
export function readServiceAccount(content: string): { info: ServiceAccountInfo; problem: ServiceAccountProblem | null } {
  let parsed: Record<string, unknown>
  try {
    const value: unknown = JSON.parse(content)
    if (!value || typeof value !== 'object' || Array.isArray(value)) {
      return { info: { clientEmail: null, projectId: null }, problem: 'notJson' }
    }
    parsed = value as Record<string, unknown>
  } catch {
    return { info: { clientEmail: null, projectId: null }, problem: 'notJson' }
  }
  const info = {
    clientEmail: typeof parsed.client_email === 'string' ? parsed.client_email : null,
    projectId: typeof parsed.project_id === 'string' ? parsed.project_id : null,
  }
  if (parsed.type !== 'service_account' || !info.clientEmail) {
    return { info, problem: 'notServiceAccount' }
  }
  if (typeof parsed.private_key !== 'string' || !parsed.private_key.includes('PRIVATE KEY')) {
    return { info, problem: 'noPrivateKey' }
  }
  return { info, problem: null }
}
