/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Every color is a CSS variable holding an RGB triplet (src/index.css, ported from the design kit), so one class
        // works in light and dark and the opacity modifier keeps working (bg-fg/10) through the <alpha-value> slot.
        bg: 'rgb(var(--bg) / <alpha-value>)',
        bg2: 'rgb(var(--bg-2) / <alpha-value>)',
        surface: 'rgb(var(--surface) / <alpha-value>)',
        border: 'rgb(var(--border) / <alpha-value>)',
        borderStrong: 'rgb(var(--border-strong) / <alpha-value>)',
        // Control boundaries, 3:1 on every surface (WCAG 1.4.11).
        borderControl: 'rgb(var(--border-control) / <alpha-value>)',
        fg: 'rgb(var(--fg) / <alpha-value>)',
        fg2: 'rgb(var(--fg-2) / <alpha-value>)',
        fg3: 'rgb(var(--fg-3) / <alpha-value>)',
        accent: {
          DEFAULT: 'rgb(var(--accent) / <alpha-value>)',
          fg: 'rgb(var(--accent-fg) / <alpha-value>)',
          // Accent as text, and as a fill under accent-fg text (storeasy: the same Ultramarine, AA on both themes).
          text: 'rgb(var(--accent-text) / <alpha-value>)',
          fill: 'rgb(var(--accent-fill) / <alpha-value>)',
        },
        success: 'rgb(var(--success) / <alpha-value>)',
        warning: 'rgb(var(--warning) / <alpha-value>)',
        // Warning as text (AA 4.5:1); the fill above is for dots, bars and tints.
        warningText: 'rgb(var(--warning-text) / <alpha-value>)',
        danger: 'rgb(var(--danger) / <alpha-value>)',
        info: 'rgb(var(--info) / <alpha-value>)',
        overlay: 'rgb(var(--overlay) / <alpha-value>)',
      },
      fontFamily: {
        // Headings, body and data (design 19 section 3); all three are self-hosted through @fontsource.
        display: ['"Bricolage Grotesque"', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        sans: ['Onest', 'ui-sans-serif', 'system-ui', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'monospace'],
      },
      boxShadow: {
        // Hairlines carry the app; the one shadow token is reserved for things that float above the page.
        float: 'var(--sh-float)',
      },
      borderRadius: {
        xs: '6px',
        sm: '8px',
        ctl: '10px', // buttons and fields
        md: '12px',
        lg: '16px', // cards
        xl: '24px', // modals
        pill: '999px',
      },
      transitionDuration: {
        1: '120ms',
        2: '220ms',
        3: '420ms',
      },
      transitionTimingFunction: {
        'out-soft': 'cubic-bezier(.2,.7,.2,1)',
      },
      keyframes: {
        'fade-in': { from: { opacity: '0' }, to: { opacity: '1' } },
        'scale-in': { from: { opacity: '0', transform: 'scale(.96)' }, to: { opacity: '1', transform: 'scale(1)' } },
        'slide-up': { from: { opacity: '0', transform: 'translateY(100%)' }, to: { opacity: '1', transform: 'translateY(0)' } },
        'slide-in-right': { from: { opacity: '0', transform: 'translateX(100%)' }, to: { opacity: '1', transform: 'translateX(0)' } },
        'slide-in-left': { from: { opacity: '0', transform: 'translateX(-100%)' }, to: { opacity: '1', transform: 'translateX(0)' } },
        'toast-in': { from: { opacity: '0', transform: 'translateY(12px)' }, to: { opacity: '1', transform: 'translateY(0)' } },
      },
      animation: {
        'fade-in': 'fade-in 220ms cubic-bezier(.2,.7,.2,1) both',
        'scale-in': 'scale-in 220ms cubic-bezier(.2,.7,.2,1) both',
        'slide-up': 'slide-up 420ms cubic-bezier(.2,.7,.2,1) both',
        'slide-in-right': 'slide-in-right 420ms cubic-bezier(.2,.7,.2,1) both',
        'slide-in-left': 'slide-in-left 420ms cubic-bezier(.2,.7,.2,1) both',
        'toast-in': 'toast-in 220ms cubic-bezier(.2,.7,.2,1) both',
      },
    },
  },
  plugins: [],
}
