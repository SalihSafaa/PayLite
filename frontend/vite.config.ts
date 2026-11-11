import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The browser only ever talks to ONE origin. In dev, Vite proxies to the two services
// running locally (`dotnet run` ports); in Docker, nginx does the same (see nginx.conf).
// Keep the two routing tables identical.
const wallet = process.env.WALLET_API_URL ?? 'http://localhost:8081'
const activity = process.env.ACTIVITY_SERVICE_URL ?? 'http://localhost:8082'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // More specific Activity routes first: /api/merchants/me/payments belongs to the Wallet API.
      '/api/notifications': activity,
      '/api/merchants/me/daily-summary': activity,
      '/hubs': { target: activity, ws: true },
      '/api': wallet,
    },
  },
})
