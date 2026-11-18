// SPDX-License-Identifier: AGPL-3.0-only
import type { ApiClient } from './http.js'

/** GET /api/v1.0/tokens/current (CONTRACT 2.1). */
export interface CurrentToken {
  kind: number | string
  name?: string | null
  prefix?: string | null
  organizationId?: number | null
  organizationName?: string | null
  userName?: string | null
  readOnly?: boolean
  scopes?: string[]
  appIds?: number[]
  allApps?: boolean
  expiryDate?: string | null
}

export function getCurrentToken(api: ApiClient): Promise<CurrentToken> {
  return api.request<CurrentToken>('GET', '/api/v1.0/tokens/current')
}

export interface AppStoreRecord {
  storeAppId?: number
  store: number | string
  externalId?: string | null
  bundleId?: string | null
  name?: string | null
}

export interface AppListItem {
  id: number
  name: string
  stores?: AppStoreRecord[]
}

/** PATCH /api/v1.0/apps, every page. */
export async function listApps(api: ApiClient, rowCount = 100): Promise<AppListItem[]> {
  const out: AppListItem[] = []
  for (let page = 1; page <= 1000; page++) {
    const env = await api.send<AppListItem[]>('PATCH', '/api/v1.0/apps', { body: { pageNumber: page, rowCount }, retry: true })
    out.push(...(env.data ?? []))
    const total = env.totalPageCount ?? 1
    if (!env.data?.length || page >= total) {
      break
    }
  }
  return out
}

export interface JobStep {
  order: number
  attempt?: number
  name: string
  status: number | string
  progress?: number | null
  total?: number | null
  message?: string | null
}

export interface JobDetail {
  id: number
  kind: number | string
  status: number | string
  store?: number | string | null
  appId?: number | null
  summary?: string | null
  error?: string | null
  attempt?: number
  maxAttempts?: number
  creationDate?: string | null
  startDate?: string | null
  endDate?: string | null
  steps?: JobStep[]
}

/** GET /api/v1.0/storeJobs/job?JobId= (design 20 calls it jobs/job). */
export function getJob(api: ApiClient, jobId: number): Promise<JobDetail> {
  return api.request<JobDetail>('GET', '/api/v1.0/storeJobs/job', { query: { JobId: jobId } })
}
