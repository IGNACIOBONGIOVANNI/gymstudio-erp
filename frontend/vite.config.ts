import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  optimizeDeps: {
    exclude: ['jspdf']
  },
  server: {
    host: true,
    watch: {
      usePolling: true
    },
    proxy: {
      '/api': {
        // En Docker el backend no está en localhost sino en el servicio "backend".
        target: process.env.VITE_PROXY_TARGET || 'http://localhost:5122',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})