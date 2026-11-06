import { resolve } from 'node:path';
import { injectIWER } from '@iwsdk/vite-plugin-iwer';
import basicSsl from '@vitejs/plugin-basic-ssl';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

// `npm run dev` serves http://localhost:5173 (desktop + IWER headset emulation).
// `npm run dev -- --mode https` adds a self-signed certificate so a Quest on the LAN can open the room.
export default defineConfig(({ mode }) => ({
  plugins: [
    react(),
    ...(mode === 'https' ? [basicSsl()] : []),
    injectIWER({ device: 'metaQuest3', activation: 'localhost', injectOnBuild: false }),
  ],
  server: {
    host: true,
    port: 5173,
    proxy: { '/v1': { target: process.env.SAMESKY_API ?? 'http://localhost:8000', ws: true } },
  },
  build: {
    target: 'es2022',
    chunkSizeWarningLimit: 2500,
    rollupOptions: { input: { main: resolve(__dirname, 'index.html'), room: resolve(__dirname, 'room.html') } },
  },
  test: { environment: 'node', include: ['src/**/*.test.ts'] },
}));
