import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import dts from 'vite-plugin-dts'
import * as path from 'path'

export default defineConfig({
  plugins: [react(), dts()],
  server: {
    port: 5176,
  },
  optimizeDeps: {
    include: ['prop-types'],
  },
  resolve: {
    alias: {
      'prop-types': 'prop-types/prop-types.js',
    },
  },
  build: {
    lib: {
      entry: path.resolve(__dirname, 'src/index.ts'),
      name: 'GaReactComponents',
      fileName: (format) => `ga-react-components.${format}.js`
    },
    rollupOptions: {
      external: ['react', 'react-dom'],
      output: {
        globals: {
          react: 'React',
          'react-dom': 'ReactDOM'
        }
      }
    }
  }
})