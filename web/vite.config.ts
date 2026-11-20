import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import path from 'path';
import { readFileSync } from 'node:fs';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: './',
  define: {
    __MAYLO_VERSION__: JSON.stringify(readFileSync(new URL('../VERSION', import.meta.url), 'utf8').trim()),
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
    },
  },
  build: {
    outDir: '../ui-dist',
    emptyOutDir: true,
    assetsInlineLimit: 8192,
  },
});
