import { useIsFetching } from '@tanstack/react-query'

/**
 * The 2 px accent line at the top of the content while data that is already on screen refreshes (design 18.4). A first
 * load shows its skeleton instead, so queries without data are not counted.
 */
export function TopProgress() {
  const fetching = useIsFetching({ predicate: (query) => query.state.data !== undefined })
  return fetching > 0 ? <div className="topline" aria-hidden /> : null
}
