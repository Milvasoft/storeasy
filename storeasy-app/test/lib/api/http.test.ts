import { HttpResponse, http } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { ApiError, apiErrorMessage, apiGet, apiPost, AUTH_LOST_EVENT, setGateHandler } from '@/lib/api/http'
import { tokenStore } from '@/lib/api/tokenStore'
import { StorageKey, storageKey } from '@/lib/utils/storage'
import { endpoint, envelope, errorResponse } from '../../support/handlers'
import { signIn } from '../../support/render'
import { server } from '../../support/server'
import { requestsTo } from '../../support/state'

const json = (data: unknown, status = 200) => HttpResponse.json(data as Record<string, unknown>, { status })

describe('http', () => {
  it('sends the UI language as Accept-Language, the stored choice first', async () => {
    localStorage.setItem(storageKey(StorageKey.Lang), 'en')
    server.use(endpoint('get', '/Probe', () => json(envelope(1))))

    await apiGet('/Probe')

    expect(requestsTo('/Probe')[0].headers['accept-language']).toBe('en-US')
  })

  it('falls back to the browser language when nothing is stored', async () => {
    vi.spyOn(navigator, 'language', 'get').mockReturnValue('tr-TR')
    server.use(endpoint('get', '/Probe', () => json(envelope(1))))

    await apiGet('/Probe')

    expect(requestsTo('/Probe')[0].headers['accept-language']).toBe('tr-TR')
  })

  it('sends the bearer token, and none on an anonymous call', async () => {
    signIn()
    server.use(endpoint('get', '/Probe', () => json(envelope(1))))
    server.use(endpoint('post', '/Anonymous', () => json(envelope(1))))

    await apiGet('/Probe')
    await apiPost('/Anonymous', {}, { auth: false })

    expect(requestsTo('/Probe')[0].headers.authorization).toBe('Bearer access-token')
    expect(requestsTo('/Anonymous')[0].headers.authorization).toBeUndefined()
  })

  it('reads a camelCase response as PascalCase', async () => {
    server.use(
      endpoint('get', '/Probe', () => json({ isSuccess: true, statusCode: 200, data: { userName: 'deniz', nested: [{ keyId: 'X' }] } })),
    )

    const data = await apiGet<{ UserName: string; Nested: Array<{ KeyId: string }> }>('/Probe')

    expect(data).toEqual({ UserName: 'deniz', Nested: [{ KeyId: 'X' }] })
  })

  it('turns a failed envelope into an ApiError with status, messages and keys', async () => {
    server.use(endpoint('post', '/Probe', () => errorResponse('OrganizationLastOwner', 'Son sahip düşemez.', 400)))

    const error = await apiPost('/Probe').catch((failure: unknown) => failure)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ statusCode: 400, messages: ['Son sahip düşemez.'], keys: ['OrganizationLastOwner'] })
  })

  it('reads Retry-After from a 429', async () => {
    server.use(endpoint('post', '/Probe', () => errorResponse('TooManyRequests', 'Çok fazla deneme.', 429, { 'Retry-After': '42' })))

    const error = (await apiPost('/Probe').catch((failure: unknown) => failure)) as ApiError

    expect(error.retryAfterSeconds).toBe(42)
  })

  it('shows the dictionary fallback, not "Request failed (502)", for an HTML error page', async () => {
    server.use(
      endpoint(
        'get',
        '/Probe',
        () => new HttpResponse('<html>Bad gateway</html>', { status: 502, headers: { 'Content-Type': 'text/html' } }),
      ),
    )

    const error = await apiGet('/Probe').catch((failure: unknown) => failure)

    expect(error).toBeInstanceOf(ApiError)
    expect(apiErrorMessage(error, 'Bir şeyler ters gitti')).toBe('Bir şeyler ters gitti')
  })

  it('shares one refresh between two concurrent 401s and replays both requests', async () => {
    signIn()
    let refreshed = false
    server.use(
      endpoint('post', '/Account/login/refresh', () => {
        refreshed = true
        return json(
          envelope({ Id: 7, UserType: 2, Token: { AccessToken: 'fresh', RefreshToken: 'r2', ExpiresIn: 1, RefreshTokenExpiresIn: 2 } }),
        )
      }),
    )
    server.use(
      endpoint('get', '/A', ({ request }) =>
        request.headers.get('authorization') === 'Bearer fresh' ? json(envelope('a')) : errorResponse('Unauthorized', 'x', 401),
      ),
    )
    server.use(
      endpoint('get', '/B', ({ request }) =>
        request.headers.get('authorization') === 'Bearer fresh' ? json(envelope('b')) : errorResponse('Unauthorized', 'x', 401),
      ),
    )

    const results = await Promise.all([apiGet('/A'), apiGet('/B')])

    expect(results).toEqual(['a', 'b'])
    expect(refreshed).toBe(true)
    expect(requestsTo('/Account/login/refresh')).toHaveLength(1)
    expect(requestsTo('/Account/login/refresh')[0].headers.authorization).toBe('Bearer access-token')
    expect(tokenStore.get()?.accessToken).toBe('fresh')
  })

  it('ends the session when the refresh fails', async () => {
    signIn()
    const lost = vi.fn()
    window.addEventListener(AUTH_LOST_EVENT, lost)
    server.use(endpoint('post', '/Account/login/refresh', () => errorResponse('InvalidToken', 'x', 401)))
    server.use(endpoint('get', '/Probe', () => errorResponse('Unauthorized', 'x', 401)))

    await apiGet('/Probe').catch(() => undefined)

    expect(lost).toHaveBeenCalledTimes(1)
    expect(tokenStore.get()).toBeNull()
    window.removeEventListener(AUTH_LOST_EVENT, lost)
  })

  it('does not try a refresh when the sign-in itself answers 401', async () => {
    signIn()

    await apiPost('/Account/login', { UserName: 'x', Password: 'wrong-password' }).catch(() => undefined)

    expect(requestsTo('/Account/login/refresh')).toHaveLength(0)
  })

  it('replays an idempotent POST after a refresh with the same Idempotency-Key', async () => {
    signIn()
    server.use(
      endpoint('post', '/Create', ({ request }) =>
        request.headers.get('authorization') === 'Bearer access-token' && requestsTo('/Create').length === 1
          ? errorResponse('SessionTimeout', 'x', 419)
          : json(envelope(5)),
      ),
    )

    await apiPost('/Create', { Name: 'x' }, { idempotent: true })

    const keys = requestsTo('/Create').map((request) => request.headers['idempotency-key'])
    expect(keys).toHaveLength(2)
    expect(keys[0]).toBeTruthy()
    expect(keys[1]).toBe(keys[0])
  })

  it('sends two identical idempotent POSTs at once only once', async () => {
    server.use(endpoint('post', '/Create', () => json(envelope(5))))

    const [first, second] = await Promise.all([
      apiPost('/Create', { Name: 'x' }, { idempotent: true }),
      apiPost('/Create', { Name: 'x' }, { idempotent: true }),
    ])

    expect([first, second]).toEqual([5, 5])
    expect(requestsTo('/Create')).toHaveLength(1)
  })

  it('sends a gated request once more when the gate is passed, and gives a second refusal to the caller', async () => {
    const gate = vi.fn().mockResolvedValue(true)
    setGateHandler('NeedsSomething', gate)
    server.use(endpoint('post', '/Gated', () => errorResponse('NeedsSomething', 'Önce şunu yap.', 403)))

    const error = await apiPost('/Gated').catch((failure: unknown) => failure)

    expect(gate).toHaveBeenCalledTimes(1)
    expect(requestsTo('/Gated')).toHaveLength(2)
    expect(error).toBeInstanceOf(ApiError)
    setGateHandler('NeedsSomething', null)
  })

  it('sends a list query parameter as a repeated key', async () => {
    server.use(http.get('*/api/v1.0/List', ({ request }) => json(envelope(new URL(request.url).searchParams.getAll('Ids')))))

    const ids = await apiGet<string[]>('/List', { Ids: [1, 2], Skip: null })

    expect(ids).toEqual(['1', '2'])
  })
})
