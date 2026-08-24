/** @type {import('tailwindcss').Config} */
export default {
  darkMode: 'class',
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        bg: 'rgb(var(--color-bg) / <alpha-value>)',
        surface: 'rgb(var(--color-surface) / <alpha-value>)',
        surface2: 'rgb(var(--color-surface2) / <alpha-value>)',
        border: 'rgb(var(--color-border) / <alpha-value>)',
        text: 'rgb(var(--color-text) / <alpha-value>)',
        muted: 'rgb(var(--color-muted) / <alpha-value>)',
        primary: 'rgb(var(--color-primary) / <alpha-value>)',
        primaryDim: 'rgb(var(--color-primary-dim) / <alpha-value>)',
        accent: 'rgb(var(--color-accent) / <alpha-value>)',
        danger: 'rgb(var(--color-danger) / <alpha-value>)',
      },
      fontFamily: {
        sans: ['var(--font-body)'],
        cairo: ['var(--font-heading)'],
        tajawal: ['var(--font-body)'],
      },
      fontSize: {
        'ui-xs': ['var(--text-xs)', { lineHeight: '1.45' }],
        'ui-sm': ['var(--text-sm)', { lineHeight: '1.55' }],
        'ui-base': ['var(--text-base)', { lineHeight: 'var(--leading-body)' }],
        'ui-h3': ['var(--text-h3)', { lineHeight: 'var(--leading-heading)', fontWeight: '700' }],
        'ui-h2': ['var(--text-h2)', { lineHeight: 'var(--leading-heading)', fontWeight: '700' }],
        'ui-h1': ['var(--text-h1)', { lineHeight: 'var(--leading-heading)', fontWeight: '800' }],
      },
      spacing: {
        'ui-1': 'var(--space-1)',
        'ui-2': 'var(--space-2)',
        'ui-3': 'var(--space-3)',
        'ui-4': 'var(--space-4)',
        'ui-5': 'var(--space-5)',
        'ui-6': 'var(--space-6)',
        'ui-8': 'var(--space-8)',
        'ui-10': 'var(--space-10)',
      },
      borderRadius: {
        'ui-control': 'var(--radius-control)',
        'ui-card': 'var(--radius-card)',
        'ui-modal': 'var(--radius-modal)',
      },
      boxShadow: {
        'ui-card': 'var(--shadow-card)',
        'ui-card-hover': 'var(--shadow-card-hover)',
      },
    },
  },
  plugins: [],
}
