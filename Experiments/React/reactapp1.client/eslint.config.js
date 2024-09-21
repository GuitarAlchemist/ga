import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';

export default [
    {
        ignores: ['dist'],
    },
    {
        extends: [
            'eslint:recommended',
            'plugin:@typescript-eslint/recommended',
            'plugin:react/recommended',
            'plugin:react-hooks/recommended',
            'prettier',
        ],
        files: ['**/*.{ts,tsx}'],
        parser: '@typescript-eslint/parser',
        parserOptions: {
            ecmaVersion: 2020,
            sourceType: 'module',
            ecmaFeatures: {
                jsx: true, // Ensure JSX is correctly handled
            },
        },
        env: {
            browser: true,
            es2020: true,
        },
        globals: globals.browser,
        plugins: ['react-hooks', 'react-refresh', '@typescript-eslint'],
        rules: {
            'quotes': ['error', 'single'], // Enforce single quotes
            "semi": ["error", "always"],
            'react-refresh/only-export-components': [
                'warn',
                { allowConstantExport: true },
            ],
        },
        settings: {
            react: {
                version: 'detect', // Automatically detect the React version
            },
        },
    },
];