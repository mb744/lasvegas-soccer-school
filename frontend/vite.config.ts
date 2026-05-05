import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      // changeOrigin: false preserves Host=localhost:5173 so ASP.NET builds the
      // OAuth redirect_uri against :5173 (which matches the Google/Facebook
      // console allow-lists) and sets cookies on the same origin the SPA uses.
      '/api': {
        target: 'http://localhost:5282',
        changeOrigin: false,
      },
      '/health': {
        target: 'http://localhost:5282',
        changeOrigin: false,
      },
      // External OAuth callbacks land here (default Identity callback paths).
      '/signin-google': {
        target: 'http://localhost:5282',
        changeOrigin: false,
      },
      '/signin-facebook': {
        target: 'http://localhost:5282',
        changeOrigin: false,
      },
    },
  },
})
