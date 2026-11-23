import { useEffect, useRef, useState } from 'react'

/**
 * The rendered width of an element in pixels, kept current with a ResizeObserver. Charts draw at this width (1 SVG unit is
 * 1 pixel) so their text keeps its size and their height stays fixed however wide the page is; `fallback` is used until
 * the element is measured and where layout is not computed (tests).
 */
export function useMeasuredWidth<T extends HTMLElement>(fallback: number) {
  const ref = useRef<T>(null)
  const [width, setWidth] = useState(fallback)
  useEffect(() => {
    const element = ref.current
    if (!element || typeof ResizeObserver === 'undefined') {
      return
    }
    const observer = new ResizeObserver(([entry]) => {
      const measured = Math.round(entry.contentRect.width)
      if (measured > 0) {
        setWidth(measured)
      }
    })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  return [ref, width] as const
}
