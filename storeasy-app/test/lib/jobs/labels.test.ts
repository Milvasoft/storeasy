import { describe, expect, it } from 'vitest'
import { BuildProcessingState, ConnectionStatus, StoreJobKind, StoreJobStatus, StoreJobStepStatus, StoreKind } from '@/lib/api/types'
import {
  APP_COLORS,
  appColor,
  connectionIdsOf,
  isValidPackageName,
  lastSyncOf,
  linkCandidates,
  primaryIdentifier,
  storeCounts,
} from '@/lib/apps/apps'
import { en } from '@/lib/i18n/en'
import { tr } from '@/lib/i18n/tr'
import {
  buildStateView,
  connectionStatusLabel,
  formatDuration,
  isJobActive,
  jobDuration,
  jobKindLabel,
  jobStatusView,
  stepStatusLabel,
} from '@/lib/jobs/labels'
import { app, storeRecord } from '../../support/factories'

describe('job labels', () => {
  it('names the job kinds and falls back for an unknown one', () => {
    expect(jobKindLabel(StoreJobKind.SyncApps, tr)).toBe('Uygulama ve build senkronu')
    expect(jobKindLabel(99 as StoreJobKind, en)).toBe('99')
    expect(jobKindLabel(null, en)).toBe('Job')
  })

  it('gives every job state a text and a tone, with retry and stop variants', () => {
    expect(jobStatusView(StoreJobStatus.Queued, tr)).toEqual({ label: 'Kuyrukta', tone: 'neutral' })
    expect(jobStatusView(StoreJobStatus.Queued, tr, { retrying: true })).toEqual({ label: 'Tekrar bekliyor', tone: 'danger' })
    expect(jobStatusView(StoreJobStatus.Running, tr)).toMatchObject({ label: 'Sürüyor', tone: 'accent', dot: true })
    expect(jobStatusView(StoreJobStatus.Running, tr, { stopping: true })).toMatchObject({ label: 'Duruyor', tone: 'warning' })
    expect(jobStatusView(StoreJobStatus.Succeeded, tr).tone).toBe('success')
    expect(jobStatusView(StoreJobStatus.Failed, tr).tone).toBe('danger')
    expect(jobStatusView(StoreJobStatus.Cancelled, en).label).toBe('Cancelled')
  })

  it('knows which states are still moving', () => {
    expect([1, 2, 3, 4, 5].map((status) => isJobActive(status as StoreJobStatus))).toEqual([true, true, false, false, false])
  })

  it('names step, build and connection states', () => {
    expect(
      [StoreJobStepStatus.Running, StoreJobStepStatus.Succeeded, StoreJobStepStatus.Failed, StoreJobStepStatus.Skipped].map((status) =>
        stepStatusLabel(status, en),
      ),
    ).toEqual(['Running', 'Done', 'Failed', 'Skipped'])
    expect(Object.values(BuildProcessingState).map((state) => buildStateView(state, en).label)).toEqual([
      'Processing',
      'Valid',
      'Failed',
      'Invalid',
      'Expired',
    ])
    expect(Object.values(ConnectionStatus).map((status) => connectionStatusLabel(status, en))).toEqual([
      'Not verified',
      'Verified',
      'Rejected',
      'Unreachable',
    ])
  })

  it('formats durations the way the job list shows them', () => {
    expect(formatDuration(42_000, tr)).toBe('42 sn')
    expect(formatDuration(66_000, tr)).toBe('1 dk 06 sn')
    expect(formatDuration(2 * 3_600_000 + 5 * 60_000, en)).toBe('2 h 05 min')
  })

  it('measures a job from start to end, or to now while it runs', () => {
    expect(jobDuration('2026-11-18T09:00:00Z', '2026-11-18T09:01:00Z')).toBe(60_000)
    expect(jobDuration('2026-11-18T09:00:00Z', null, Date.parse('2026-11-18T09:00:30Z'))).toBe(30_000)
    expect(jobDuration(null, null)).toBeNull()
    expect(jobDuration('not a date', null)).toBeNull()
  })
})

describe('app helpers', () => {
  const halka = app({
    Id: 102,
    Name: 'Halka',
    Stores: [
      storeRecord({
        StoreAppId: 210,
        Store: StoreKind.Google,
        BundleId: null,
        ExternalId: 'dev.halka',
        StoreConnectionId: 12,
        SyncedDate: '2026-11-18T10:00:00Z',
      }),
    ],
  })

  it('accepts Play package names the way the API does', () => {
    expect(isValidPackageName('com.example.app')).toBe(true)
    expect(isValidPackageName(' dev.kuzeylab.pusula_2 ')).toBe(true)
    expect(isValidPackageName('kasa')).toBe(false)
    expect(isValidPackageName('com.1example')).toBe(false)
  })

  it('picks a stable icon color per app', () => {
    expect(appColor(101)).toBe(appColor(101))
    expect(APP_COLORS).toContain(appColor(-3))
  })

  it('reads the identifier, the last sync, the store counts and the connections', () => {
    expect(primaryIdentifier(app())).toBe('dev.kuzeylab.pusula')
    expect(primaryIdentifier(halka)).toBe('dev.halka')
    expect(primaryIdentifier(app({ Stores: [] }))).toBeNull()
    expect(lastSyncOf([app(), halka])).toBe('2026-11-18T10:00:00Z')
    expect(lastSyncOf([app({ Stores: [storeRecord({ SyncedDate: null })] })])).toBeNull()
    expect(storeCounts([app(), halka])).toEqual({ [StoreKind.Apple]: 1, [StoreKind.Google]: 2, [StoreKind.Microsoft]: 0 })
    expect(connectionIdsOf(app())).toEqual([11, 12])
  })

  it('offers only records of other apps for stores the app is missing', () => {
    const ios = app({ Stores: [storeRecord()] })
    const other = app({ Id: 103, Stores: [storeRecord({ StoreAppId: 230 })] })

    expect(linkCandidates(ios, [ios, halka, other]).map(({ record }) => record.StoreAppId)).toEqual([210])
  })
})
