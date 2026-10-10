import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// The backend base URL the Vite dev server proxies "/api" to.
// Local development: `dotnet run` in backend/ listens on http://localhost:8000.
const apiTarget = process.env.VITE_API_PROXY_TARGET || 'http://localhost:8000'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      // In local development, forward /api requests to the ASP.NET Core backend.
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, '')
      }
    }
  }
})
