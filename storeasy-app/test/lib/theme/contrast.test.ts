import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'

type Rgb = [number, number, number]

const CSS = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf-8')

// The custom properties of one theme block (":root," for light, ".dark {" for dark), as RGB triplets.
function tokens(selector: string): Record<string, Rgb> {
  const start = CSS.indexOf(selector)
  const block = CSS.slice(start, CSS.indexOf('}', start))
  const result: Record<string, Rgb> = {}
  for (const match of block.matchAll(/--([a-z0-9-]+):\s*(\d+)\s+(\d+)\s+(\d+);/g)) {
    result[match[1]] = [Number(match[2]), Number(match[3]), Number(match[4])]
  }
  return result
}

function channel(value: number): number {
  const c = value / 255
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
}

function luminance([r, g, b]: Rgb): number {
  return 0.2126 * channel(r) + 0.7152 * channel(g) + 0.0722 * channel(b)
}

function contrast(a: Rgb, b: Rgb): number {
  const [high, low] = [luminance(a), luminance(b)].sort((x, y) => y - x)
  return (high + 0.05) / (low + 0.05)
}

// A tint as the browser paints it: `color` at `alpha` over `base`.
function over(color: Rgb, alpha: number, base: Rgb): Rgb {
  return color.map((value, index) => Math.round(value * alpha + base[index] * (1 - alpha))) as Rgb
}

const THEMES = { light: tokens(':root,'), dark: tokens('.dark {') }

describe.each(Object.entries(THEMES))('%s theme contrast', (_, theme) => {
  const surfaces = [theme.surface, theme.bg, theme['bg-2']]

  it('defines the accessible tokens', () => {
    expect(theme['border-control']).toBeDefined()
    expect(theme['warning-text']).toBeDefined()
  })

  it('gives control borders 3:1 on every surface (WCAG 1.4.11)', () => {
    for (const surface of surfaces) {
      expect(contrast(theme['border-control'], surface)).toBeGreaterThanOrEqual(3)
    }
  })

  it('gives warning text 4.5:1 on every surface and on the warning tints (WCAG 1.4.3)', () => {
    const tints = [0.07, 0.08, 0.1].map((alpha) => over(theme.warning, alpha, theme.surface))
    for (const background of [...surfaces, ...tints]) {
      expect(contrast(theme['warning-text'], background)).toBeGreaterThanOrEqual(4.5)
    }
  })
})

describe('token use', () => {
  it('never colors text with the warning fill', () => {
    expect(CSS).not.toMatch(/(^|[^-])color:\s*rgb\(var\(--warning\)\)/m)
  })

  it('draws form controls with the control border', () => {
    for (const selector of ['.inp {', '.chk {', '.rad {', '.sw {']) {
      const start = CSS.indexOf(`  ${selector}`)
      expect(CSS.slice(start, CSS.indexOf('}', start)), selector).toContain('--border-control')
    }
  })

  it('measures the old values the way the design review did', () => {
    // Guards the formula: the review measured the light warning fill at 4.39:1 on the page background.
    expect(contrast([180, 95, 6], THEMES.light.bg)).toBeCloseTo(4.39, 1)
  })
})
