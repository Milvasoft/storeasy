import { http, HttpResponse, type HttpHandler } from 'msw'

// The dev mock's EAS endpoints (EasController): no Expo token in the demo, so the EAS tab shows how to add one; nothing is
// stored and no build starts.

const BASE = '*/api/v1.0'

function envelope<T>(data: T) {
  return HttpResponse.json({ isSuccess: true, statusCode: 200, messages: [], data })
}

function refuse(key: string, message: string, status: number) {
  return HttpResponse.json({ isSuccess: false, statusCode: status, messages: [{ key, message, type: 3 }], data: null }, { status })
}

const notInDemo = () => refuse('FeatureDisabled', 'Not available in the demo.', 503)

export const easMockHandlers: HttpHandler[] = [
  http.get(`${BASE}/eas/connection`, () => envelope(null)),
  http.post(`${BASE}/eas/connection`, notInDemo),
  http.delete(`${BASE}/eas/connection`, () => envelope(0)),
  http.get(`${BASE}/eas/projects`, () => refuse('ExpoConnectionMissing', 'Add an Expo access token on the connections page first.', 400)),
  http.put(`${BASE}/eas/project`, notInDemo),
  http.get(`${BASE}/eas/app`, ({ request }) =>
    envelope({
      AppId: Number(new URL(request.url).searchParams.get('AppId')),
      AppName: '',
      Connected: false,
      Link: null,
      Builds: [],
      Submissions: [],
    }),
  ),
  http.get(`${BASE}/eas/profiles`, () => refuse('EasProjectNotLinked', 'Link this app to an EAS project first.', 400)),
  http.post(`${BASE}/eas/build/start`, notInDemo),
  http.get(`${BASE}/eas/build`, () => refuse('EasBuildNotFound', 'EAS build not found.', 404)),
  http.post(`${BASE}/eas/build/cancel`, notInDemo),
  http.post(`${BASE}/eas/build/intake`, notInDemo),
  http.post(`${BASE}/eas/sync`, notInDemo),
  http.put(`${BASE}/eas/chain`, notInDemo),
  http.post(`${BASE}/eas/submission/start`, notInDemo),
  http.get(`${BASE}/eas/submissions`, () => envelope([])),
  http.get(`${BASE}/eas/submission`, () => refuse('EasSubmissionNotFound', 'EAS submission not found.', 404)),
]
