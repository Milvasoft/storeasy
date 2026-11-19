import { apiDelete, apiGet, apiPatchList, apiPost, apiPut } from '@/lib/api/http'
import type { ListRequest } from '@/lib/api/list'
import type {
  OrgWebhookDto,
  WebhookDeliveryDto,
  WebhookEndpointDto,
  WebhookEventDetailDto,
  WebhookEventDto,
  WebhookSecretDto,
  WebhookSource,
} from './types'

// storeasy-api WebhooksController (owners and admins: ManageWebhooks). Inbound endpoints receive store events at
// /api/webhooks/apple|signed|google/{publicId}; outgoing subscriptions post storeasy's events to the organization's own
// URLs.
export const webhooksApi = {
  endpoints: (organizationId: number) => apiGet<WebhookEndpointDto[]>('/Webhooks', { OrganizationId: organizationId }),

  createEndpoint: (
    organizationId: number,
    input: {
      Source: WebhookSource
      StoreConnectionId: number | null
      Name: string | null
      RegisterAtStore: boolean
      PushServiceAccount: string | null
    },
  ) => apiPost<WebhookSecretDto>('/Webhooks/endpoint', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  // App Store Connect sends a signed test event to every webhook of the endpoint (a job); the pings land in the event log.
  pingEndpoint: (organizationId: number, endpointId: number) =>
    apiPost<number>('/Webhooks/endpoint/ping', { OrganizationId: organizationId, EndpointId: endpointId }),

  // The new secret is answered once; the old one is still accepted for the grace hours.
  rotateEndpointSecret: (organizationId: number, endpointId: number) =>
    apiPost<WebhookSecretDto>('/Webhooks/endpoint/rotate-secret', { OrganizationId: organizationId, EndpointId: endpointId }),

  deleteEndpoint: (organizationId: number, endpointId: number) =>
    apiDelete<number>('/Webhooks/endpoint', undefined, { OrganizationId: organizationId, EndpointId: endpointId }),

  events: (organizationId: number, request: ListRequest) =>
    apiPatchList<WebhookEventDto>('/Webhooks/events', { OrganizationId: organizationId, ...request }),

  event: (organizationId: number, eventId: number) =>
    apiGet<WebhookEventDetailDto>('/Webhooks/event', { OrganizationId: organizationId, EventId: eventId }),

  reprocessEvent: (organizationId: number, eventId: number) =>
    apiPost<number>('/Webhooks/event/reprocess', { OrganizationId: organizationId, EventId: eventId }),

  subscriptions: (organizationId: number) => apiGet<OrgWebhookDto[]>('/Webhooks/subscriptions', { OrganizationId: organizationId }),

  createSubscription: (organizationId: number, input: { Name: string; Url: string; Events: string[] }) =>
    apiPost<WebhookSecretDto>('/Webhooks/subscription', { OrganizationId: organizationId, ...input }, { idempotent: true }),

  updateSubscription: (
    organizationId: number,
    webhookId: number,
    input: { Name?: string | null; Url?: string | null; Events?: string[] | null; IsActive?: boolean | null },
  ) => apiPut<number>('/Webhooks/subscription', { OrganizationId: organizationId, WebhookId: webhookId, ...input }),

  deleteSubscription: (organizationId: number, webhookId: number) =>
    apiDelete<number>('/Webhooks/subscription', undefined, { OrganizationId: organizationId, WebhookId: webhookId }),

  rotateSubscriptionSecret: (organizationId: number, webhookId: number) =>
    apiPost<WebhookSecretDto>('/Webhooks/subscription/rotate-secret', { OrganizationId: organizationId, WebhookId: webhookId }),

  testSubscription: (organizationId: number, webhookId: number) =>
    apiPost<WebhookDeliveryDto>('/Webhooks/subscription/test', { OrganizationId: organizationId, WebhookId: webhookId }),

  deliveries: (organizationId: number, webhookId: number | null, request: ListRequest) =>
    apiPatchList<WebhookDeliveryDto>('/Webhooks/deliveries', { OrganizationId: organizationId, WebhookId: webhookId, ...request }),
}

export const webhookKeys = {
  all: ['webhooks'] as const,
  endpoints: (organizationId: number) => ['webhooks', 'endpoints', organizationId] as const,
  events: (organizationId: number, request: ListRequest) => ['webhooks', 'events', organizationId, request] as const,
  event: (organizationId: number, eventId: number) => ['webhooks', 'event', organizationId, eventId] as const,
  subscriptions: (organizationId: number) => ['webhooks', 'subscriptions', organizationId] as const,
  deliveries: (organizationId: number, webhookId: number | null, request: ListRequest) =>
    ['webhooks', 'deliveries', organizationId, webhookId, request] as const,
}
