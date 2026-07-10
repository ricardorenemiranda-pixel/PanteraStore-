# SteamMarket Frontend

Proyecto Astro **totalmente independiente** del backend (`SteamMarket-backend`).
Se comunican solo por HTTP, vía `/api/*`.

## Requisitos

- Node.js 20+
- El backend (`SteamMarket.Api`) corriendo en `http://localhost:5000`

## Cookie de sesión + proxy (importante)

El login es por cookie de sesión (Steam OpenID). Si este frontend (`localhost:4321`)
le pegara directo a `localhost:5000`, la cookie sería cross-site y necesitaría
`SameSite=None` + HTTPS.

Para evitarlo en dev, `astro.config.mjs` proxea todo lo que empieza con `/api`
hacia el backend (`BACKEND_URL`, por defecto `http://localhost:5000`). Así el
navegador ve un solo origen (`localhost:4321`) y la cookie funciona sin
configuración especial.

Si el backend corre en otro puerto, arrancá con:

```bash
BACKEND_URL=http://localhost:5001 npm run dev
```

## Desarrollo

```bash
npm install
npm run dev
```

Abre `http://localhost:4321`. Con el backend corriendo, "Iniciar sesión con
Steam" te lleva al login de Steam y te trae de vuelta a `/inventario`.

## Estructura

- `src/lib/api.ts` — cliente tipado del backend (`/api/auth/*`, `/api/inventory/dota`).
- `src/layouts/Layout.astro` — layout compartido, header con estado de sesión.
- `src/pages/index.astro` — landing con botón de login.
- `src/pages/inventario.astro` — inventario de Dota cotizado (protegida: redirige a `/` si no hay sesión).

## Build

```bash
npm run build   # corre astro check + astro build
npm run preview
```
