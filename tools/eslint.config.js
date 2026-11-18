// SPDX-License-Identifier: AGPL-3.0-only
import js from '@eslint/js'
import tseslint from 'typescript-eslint'
import globals from 'globals'

export default tseslint.config(
  { ignores: ['**/dist/**', '**/node_modules/**', 'azure-devops-task/StoreasyUploadV1/dist/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    files: ['**/*.{ts,js,mjs}'],
    languageOptions: { globals: { ...globals.node } },
    rules: {
      curly: ['error', 'all'],
      eqeqeq: ['error', 'smart'],
      'no-console': 'error',
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_', destructuredArrayIgnorePattern: '^_' }],
    },
  },
  {
    // Build scripts may print.
    files: ['**/build.mjs', 'scripts/**/*.mjs'],
    rules: { 'no-console': 'off' },
  },
)
