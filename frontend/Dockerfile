FROM node:20-alpine

WORKDIR /app

# npm ci instala exactamente las versiones de package-lock.json. Se guarda una huella
# del lock para saber, al arrancar, si node_modules corresponde a esas versiones.
COPY package*.json ./
RUN npm ci && md5sum package-lock.json > node_modules/.huella-lock

# El código se monta en /app desde docker-compose (hot reload) y node_modules vive en
# un volumen que Docker conserva entre versiones. Si ese volumen quedó vacío o con
# dependencias de una versión anterior (la huella no coincide), se reinstala antes de
# arrancar en lugar de fallar por un paquete faltante.
CMD ["sh", "-c", "if [ ! -x node_modules/.bin/vite ] || [ \"$(md5sum package-lock.json)\" != \"$(cat node_modules/.huella-lock 2>/dev/null)\" ]; then echo 'Actualizando dependencias...' && npm install --no-audit --no-fund && md5sum package-lock.json > node_modules/.huella-lock; fi; npm run dev -- --host"]
