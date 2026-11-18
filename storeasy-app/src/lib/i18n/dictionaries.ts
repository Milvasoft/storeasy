import { en, type Dictionary } from './en'
import { readLang, type Lang } from './lang'
import { tr } from './tr'

export const dictionaries: Record<Lang, Dictionary> = { tr, en }

// For code outside React (the query client's error toast, the root error boundary). A plain module, so importing it
// never pulls React context code into the http or query layer.
export function getDictionary(): Dictionary {
  return dictionaries[readLang()]
}
