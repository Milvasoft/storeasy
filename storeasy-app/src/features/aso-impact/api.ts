import { useQuery } from '@tanstack/react-query'
import { apiGet } from '@/lib/api/http'
import type { AsoImpactEventDto, AsoImpactRecentDto, AsoImpactReportDto, ImpactEventFilter, ImpactReportOptions } from './types'

// storeasy-api AsoImpactController. Read only (View); everything is computed from storeasy's rows on each call.
export const impactApi = {
  // 404 AppNotFound; 400 StoreMetricsRangeInvalid past 400 days.
  events: (organizationId: number, appId: number, filter: ImpactEventFilter = {}) =>
    apiGet<AsoImpactEventDto[]>('/aso-impact/events', { OrganizationId: organizationId, AppId: appId, ...clean(filter) }),

  // 404 AsoImpactEventNotFound; 400 AsoImpactWindowInvalid (window 3 to 56 days, gap 0 to 14).
  report: (organizationId: number, appId: number, eventId: string, options: ImpactReportOptions) =>
    apiGet<AsoImpactReportDto>('/aso-impact', { OrganizationId: organizationId, AppId: appId, EventId: eventId, ...clean(options) }),

  // Every app of the organization.
  recent: (organizationId: number, options: { Days?: number; WindowDays?: number } = {}) =>
    apiGet<AsoImpactRecentDto>('/aso-impact/recent', { OrganizationId: organizationId, ...clean(options) }),
}

function clean<T extends object>(value: T): Record<string, string | number> {
  return Object.fromEntries(
    Object.entries(value)
      .filter(([, item]) => item != null && item !== '')
      .map(([key, item]) => [key, typeof item === 'boolean' ? String(item) : item]),
  ) as Record<string, string | number>
}

export const impactKeys = {
  all: ['aso-impact'] as const,
  events: (organizationId: number, appId: number, filter: ImpactEventFilter) =>
    ['aso-impact', 'events', organizationId, appId, filter] as const,
  report: (organizationId: number, appId: number, eventId: string, options: ImpactReportOptions) =>
    ['aso-impact', 'report', organizationId, appId, eventId, options] as const,
  recent: (organizationId: number) => ['aso-impact', 'recent', organizationId] as const,
}

export function useImpactEvents(organizationId: number, appId: number, filter: ImpactEventFilter = {}, enabled = true) {
  return useQuery({
    queryKey: impactKeys.events(organizationId, appId, filter),
    queryFn: () => impactApi.events(organizationId, appId, filter),
    enabled,
  })
}

export function useImpactReport(organizationId: number, appId: number, eventId: string | null, options: ImpactReportOptions) {
  return useQuery({
    queryKey: impactKeys.report(organizationId, appId, eventId ?? '', options),
    queryFn: () => impactApi.report(organizationId, appId, eventId ?? '', options),
    enabled: eventId != null,
    placeholderData: (previous) => previous,
  })
}

export function useRecentImpact(organizationId: number) {
  return useQuery({
    queryKey: impactKeys.recent(organizationId),
    queryFn: () => impactApi.recent(organizationId),
  })
}
