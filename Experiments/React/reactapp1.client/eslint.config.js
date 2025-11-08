// Note: 'defineConfig' may not be directly available as ESM, so we omit it if it causes issues.
export default {
    ignores: ['dist'],
    overrides: [
        {
            files: ['**/*.{ts,tsx}'],
            extends: [
                'eslint:recommended',
                'plugin:@typescript-eslint/recommended',
                'plugin:react/recommended',
                'plugin:react-hooks/recommended',
                'prettier'
            ],
            parser: '@typescript-eslint/parser',
            parserOptions: {
                ecmaVersion: 2020,
                sourceType: 'module',
                ecmaFeatures: {
                    jsx: true,
                },
            },
            env: {
                browser: true,
                es2020: true,
            },
            plugins: [
                'react-hooks',
                'react-refresh',
                '@typescript-eslint'
            ],
            rules: {
                'quotes': ['error', 'single'],
                'semi': ['error', 'always'],
                'react-refresh/only-export-components': [
                    'warn',
                    {allowConstantExport: true},
                ],
            },
            settings: {
                react: {
                    version: 'detect',
                },
            },
        },
    ],
};
