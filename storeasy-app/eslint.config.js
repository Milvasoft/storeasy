import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import reactHooks from 'eslint-plugin-react-hooks'
import globals from 'globals'

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'e2e/.artifacts', 'playwright-report', 'test-results', 'public/mockServiceWorker.js'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: { globals: { ...globals.browser } },
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,
      // Bodies always in braces, on their own line (no one-line if).
      curly: ['error', 'all'],
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
    },
  },
  {
    files: ['*.config.{js,ts}', 'e2e/**/*.ts', 'test/**/*.{ts,tsx}'],
    languageOptions: { globals: { ...globals.node } },
  },
  {
    files: ['src/**/*.{ts,tsx}'],
    rules: {
      // Cross-folder imports go through @/.
      'no-restricted-imports': [
        'error',
        { patterns: [{ regex: '^\\.\\./', message: 'Use the @/ alias for imports outside the current folder.' }] },
      ],
      'no-restricted-syntax': [
        'error',
        { selector: 'ExportDefaultDeclaration', message: 'Use a named export.' },
        { selector: "CallExpression[callee.name='fetch']", message: 'Call the API through lib/api/endpoints.ts.' },
      ],
    },
  },
  {
    // The files allowed to call fetch directly: the API client only.
    files: ['src/lib/api/http.ts'],
    rules: { 'no-restricted-syntax': 'off' },
  },
)
