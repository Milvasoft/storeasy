import { QueryClientProvider } from '@tanstack/react-query'
import { render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StoreTag } from '@/components/ui/Badge'
import { StoreKind } from '@/lib/api/types'
import { resolveStoreIcon } from '@/lib/connections/storeIcons'
import { I18nProvider } from '@/lib/i18n/I18nContext'
import { connection } from '../../support/factories'
import { createTestQueryClient, renderWithProviders } from '../../support/render'
import { requestsTo, testState } from '../../support/state'

const apple = (id: number, icon: string | null) => connection({ Id: id, Store: StoreKind.Apple, Name: `Key ${id}`, Icon: icon })
const google = (id: number, icon: string | null) => connection({ Id: id, Store: StoreKind.Google, Name: `Play ${id}`, Icon: icon })

describe('resolveStoreIcon', () => {
  it("uses the record's own connection: its icon, or the text code when it has none", () => {
    const list = [apple(3, 'rocket'), apple(5, null)]
    expect(resolveStoreIcon(list, StoreKind.Apple, 3)).toBe('rocket')
    expect(resolveStoreIcon(list, StoreKind.Apple, 5)).toBeNull()
  })

  it("falls back to the store's oldest connection with an icon when only the store is known", () => {
    const list = [apple(9, 'phone'), apple(4, null), apple(7, 'appstore'), google(2, 'googleplay')]
    expect(resolveStoreIcon(list, StoreKind.Apple)).toBe('appstore')
    expect(resolveStoreIcon(list, StoreKind.Apple, 999)).toBe('appstore')
    expect(resolveStoreIcon(list, StoreKind.Google)).toBe('googleplay')
    expect(resolveStoreIcon(list, StoreKind.Microsoft)).toBeNull()
  })

  it('ignores keys this version does not know', () => {
    expect(resolveStoreIcon([apple(1, 'unicorn')], StoreKind.Apple)).toBeNull()
    expect(resolveStoreIcon([apple(1, 'unicorn'), apple(2, 'box')], StoreKind.Apple)).toBe('box')
  })
})

describe('StoreTag', () => {
  it('shows the text code when no connection has an icon', async () => {
    testState.connections = [apple(11, null)]
    const { container } = renderWithProviders(<StoreTag store={StoreKind.Apple} />)

    expect(await screen.findByText('App Store')).toBeInTheDocument()
    expect(container.querySelector('.st b')).toHaveTextContent('AS')
    expect(container.querySelector('.sti')).toBeNull()
  })

  it("draws the connection's icon in place of the code and keeps the store name as text", async () => {
    testState.connections = [google(12, 'googleplay'), google(13, 'game')]
    const { container } = renderWithProviders(
      <>
        <StoreTag store={StoreKind.Google} connectionId={13} />
        <StoreTag store={StoreKind.Google} small codeOnly />
      </>,
    )

    await waitFor(() => expect(container.querySelectorAll('.sti')).toHaveLength(2))
    const [own, generic] = Array.from(container.querySelectorAll('.st'))
    expect(own.querySelector('.sti')).toHaveAttribute('data-icon', 'game')
    expect(own.querySelector('.sti')).toHaveAttribute('aria-hidden', 'true')
    expect(own).toHaveTextContent('Google Play')
    // A store only filter or column takes the oldest connection's icon; the name stays for screen readers and as a title.
    expect(generic.querySelector('.sti')).toHaveAttribute('data-icon', 'googleplay')
    expect(generic.querySelector('.sti')).toHaveAttribute('title', 'Google Play')
    expect(generic.querySelector('.sr-only')).toHaveTextContent('Google Play')
    expect(generic.querySelector('b')).toBeNull()
  })

  it('renders the text code outside an organization without asking for the connections', () => {
    const { container } = render(
      <QueryClientProvider client={createTestQueryClient()}>
        <I18nProvider>
          <StoreTag store={StoreKind.Microsoft} small codeOnly />
        </I18nProvider>
      </QueryClientProvider>,
    )

    expect(container.querySelector('.st b')).toHaveTextContent('MS')
    expect(container.querySelector('.st b')).toHaveAttribute('title', 'Microsoft Store')
    expect(requestsTo('/StoreConnections')).toHaveLength(0)
  })
})
