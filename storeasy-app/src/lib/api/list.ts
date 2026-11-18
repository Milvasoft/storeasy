// Milvasoft ListRequest bodies. Enum values are numeric (Milvasoft.Components.Rest).
export const SortType = { Asc: 0, Desc: 1 } as const
export type SortType = (typeof SortType)[keyof typeof SortType]

// Milvasoft FilterType (Milvasoft.Components.Rest.Enums, read from the package): enum and bool columns use only EqualTo /
// NotEqualTo (08 section 3), text uses Contains, dates use Between.
export const FilterType = { Between: 0, Contains: 1, EqualTo: 5, NotEqualTo: 6 } as const
export type FilterType = (typeof FilterType)[keyof typeof FilterType]

export interface Sorting {
  SortBy: string
  Type: SortType
}

export interface FilterCriteria {
  FilterBy: string
  Value: unknown
  OtherValue?: unknown
  Type: FilterType
}

export interface ListRequest {
  PageNumber: number
  RowCount: number
  Sorting?: Sorting
  Filtering?: { Criterias: FilterCriteria[] }
}

// Page sizes offered on every paged list (design 20 section 5: 10 / 20 / 50 / 100).
export const PAGE_SIZES = [10, 20, 50, 100] as const
