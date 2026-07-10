import { defineConfig } from "astro/config";
import tailwindcss from "@tailwindcss/vite";

// El backend (SteamMarket.Api) corre en http://localhost:5000.
// Este proyecto es 100% independiente del backend, se comunican solo por HTTP.
//
// El backend usa cookie de sesion (login por Steam). Si el frontend le pegara
// directo a localhost:5000 desde localhost:4321, la cookie seria cross-site
// (necesitaria SameSite=None + HTTPS). Para evitarlo en dev, Astro proxea
// /api/* hacia el backend: asi el navegador ve todo como mismo origen
// (localhost:4321) y la cookie funciona sin configuracion especial.
const backendUrl = process.env.BACKEND_URL ?? "http://localhost:5000";

export default defineConfig({
  vite: {
    plugins: [tailwindcss()],
    server: {
      proxy: {
        "/api": {
          target: backendUrl,
          changeOrigin: true,
        },
      },
    },
  },
});
