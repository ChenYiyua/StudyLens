import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'

const extensionRoot = fileURLToPath(new URL('.', import.meta.url))

export default defineConfig({
  base: './',
  plugins: [react()],
  build: {
    rollupOptions: {
      input: {
        popup: `${extensionRoot}index.html`,
        background: `${extensionRoot}src/background.ts`,
      },
      output: {
        entryFileNames: (chunk) => chunk.name === 'background'
          ? 'background.js'
          : 'assets/[name]-[hash].js',
      },
    },
  },
})
