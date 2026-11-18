import { generateKeyPairSync } from 'node:crypto'

/**
 * A Google Play service account key that is well formed but belongs to no Google project: a fresh RSA key generated per
 * run. The e2e stack resolves every store host to a refusing address (deploy/e2e/docker-compose.e2e.yml), so nothing
 * built from it ever reaches Google.
 */
export function fakeServiceAccount(id: string) {
  const { privateKey } = generateKeyPairSync('rsa', {
    modulusLength: 2048,
    privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
    publicKeyEncoding: { type: 'spki', format: 'pem' },
  })
  const json = JSON.stringify(
    {
      type: 'service_account',
      project_id: `e2e-${id}`,
      private_key_id: `e2e${id}`,
      private_key: privateKey,
      client_email: `storeasy-e2e@e2e-${id}.iam.gserviceaccount.com`,
      client_id: '100000000000000000000',
      auth_uri: 'https://accounts.google.com/o/oauth2/auth',
      token_uri: 'https://oauth2.googleapis.com/token',
    },
    null,
    2,
  )
  return { json, privateKey }
}
