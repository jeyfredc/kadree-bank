# ---- Compilación ----
FROM node:24-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY . .
RUN npx ng build --configuration production

# ---- Servidor estático ----
FROM nginx:1.29-alpine
# La imagen oficial procesa /etc/nginx/templates/*.template con envsubst al arrancar,
# así la URL de la API (API_UPSTREAM) se configura por variable de entorno.
COPY nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
ENV API_UPSTREAM=http://api:8080
EXPOSE 80
