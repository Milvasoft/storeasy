import type {
  ContentRatingAnswers,
  DataSafetyAnswers,
  DataSafetyRow,
  PrivacyDataTypeAnswer,
  PrivacyLabelsAnswers,
  ReviewLoginAnswers,
  ReviewNotesAnswers,
  TargetAudienceAnswers,
} from '@/features/common/types'

// The saved answer documents (Storeasy.Domain SavedAnswerDocuments) read defensively from the API's free JSON, and the
// Data safety CSV exactly as the API generates it (DataSafetyCsv), so the preview is what Play receives.

type Raw = Record<string, unknown>

function obj(value: unknown): Raw {
  return value && typeof value === 'object' && !Array.isArray(value) ? (value as Raw) : {}
}

function str(value: unknown): string | null {
  return typeof value === 'string' ? value : null
}

function bool(value: unknown): boolean {
  return value === true
}

function strings(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : []
}

export function readPrivacy(value: unknown): PrivacyLabelsAnswers {
  const raw = obj(value)
  return {
    CollectsData: bool(raw.CollectsData),
    PrivacyPolicyUrl: str(raw.PrivacyPolicyUrl),
    DataTypes: (Array.isArray(raw.DataTypes) ? raw.DataTypes : []).map((item): PrivacyDataTypeAnswer => {
      const row = obj(item)
      return {
        Category: str(row.Category) ?? '',
        Type: str(row.Type) ?? '',
        Purposes: strings(row.Purposes),
        LinkedToUser: bool(row.LinkedToUser),
        UsedForTracking: bool(row.UsedForTracking),
      }
    }),
  }
}

export function readDataSafety(value: unknown): DataSafetyAnswers {
  const raw = obj(value)
  return {
    Rows: (Array.isArray(raw.Rows) ? raw.Rows : []).map((item): DataSafetyRow => {
      const row = obj(item)
      return {
        QuestionId: str(row.QuestionId) ?? '',
        ResponseId: str(row.ResponseId),
        ResponseValue: str(row.ResponseValue),
        Label: str(row.Label),
      }
    }),
  }
}

// pascalizeKeys capitalizes the question keys of this dictionary too (02 section 14, pitfall 30); they are sent back as
// they come.
export function readContentRating(value: unknown): ContentRatingAnswers {
  const raw = obj(value)
  const answers = obj(raw.Answers)
  return {
    Category: str(raw.Category),
    Email: str(raw.Email),
    Answers: Object.fromEntries(
      Object.entries(answers).map(([key, answer]) => [key, typeof answer === 'string' ? answer : String(answer ?? '')]),
    ),
  }
}

export function readTargetAudience(value: unknown): TargetAudienceAnswers {
  const raw = obj(value)
  return { AgeGroups: strings(raw.AgeGroups), AppealsToChildren: bool(raw.AppealsToChildren), ContainsAds: bool(raw.ContainsAds) }
}

export function readReviewLogin(value: unknown): ReviewLoginAnswers {
  const raw = obj(value)
  return {
    ContactFirstName: str(raw.ContactFirstName),
    ContactLastName: str(raw.ContactLastName),
    ContactEmail: str(raw.ContactEmail),
    ContactPhone: str(raw.ContactPhone),
    DemoAccountRequired: bool(raw.DemoAccountRequired),
    DemoAccountName: str(raw.DemoAccountName),
  }
}

export function readReviewNotes(value: unknown): ReviewNotesAnswers {
  return { Notes: str(obj(value).Notes) }
}

// ---- Data safety CSV -----------------------------------------------------------------------------------------------------
export const DATA_SAFETY_HEADER =
  'Question ID (machine readable),Response ID (machine readable),Response value,Answer requirement,Human-friendly question label'

function csvField(value: string | null | undefined): string {
  if (!value) {
    return ''
  }
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value
}

/** The CSV the API sends to Play for these rows (CRLF line ends, the answer requirement column left empty). */
export function dataSafetyCsv(rows: DataSafetyRow[]): string {
  const lines = rows.map((row) =>
    [csvField(row.QuestionId), csvField(row.ResponseId), csvField(row.ResponseValue), '', csvField(row.Label)].join(','),
  )
  return [DATA_SAFETY_HEADER, ...lines].join('\r\n') + '\r\n'
}

/** Splits CSV text into records, quotes and escaped quotes included. */
export function parseCsv(text: string): string[][] {
  const records: string[][] = []
  let field = ''
  let record: string[] = []
  let quoted = false
  for (let index = 0; index < text.length; index += 1) {
    const char = text[index]
    if (quoted) {
      if (char === '"' && text[index + 1] === '"') {
        field += '"'
        index += 1
      } else if (char === '"') {
        quoted = false
      } else {
        field += char
      }
    } else if (char === '"') {
      quoted = true
    } else if (char === ',') {
      record.push(field)
      field = ''
    } else if (char === '\n' || char === '\r') {
      if (char === '\r' && text[index + 1] === '\n') {
        index += 1
      }
      record.push(field)
      records.push(record)
      record = []
      field = ''
    } else {
      field += char
    }
  }
  if (field || record.length > 0) {
    record.push(field)
    records.push(record)
  }
  return records.filter((row) => row.some((cell) => cell.trim() !== ''))
}

/** The rows of a CSV exported from Play Console; null when the header is not Play's. */
export function importDataSafetyCsv(text: string): DataSafetyRow[] | null {
  const records = parseCsv(text.trim())
  if (records.length === 0 || !records[0][0]?.toLowerCase().startsWith('question id')) {
    return null
  }
  return records.slice(1).map((cells) => ({
    QuestionId: cells[0] ?? '',
    ResponseId: cells[1] || null,
    ResponseValue: cells[2] || null,
    Label: cells[4] || null,
  }))
}
