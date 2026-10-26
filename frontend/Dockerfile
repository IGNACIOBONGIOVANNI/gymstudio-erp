FROM node:20-alpine

WORKDIR /app

# npm ci instala exactamente las versiones de package-lock.json.
COPY package*.json ./
RUN npm ci

# El código se monta en /app desde docker-compose (hot reload) y node_modules vive
# en un volumen propio. Si ese volumen quedó vacío o viejo (caché rota, otra versión
# de dependencias), se reinstala antes de arrancar en vez de fallar con "vite: not found".
CMD ["sh", "-c", "[ -x node_modules/.bin/vite ] || npm ci; npm run dev -- --host"]
