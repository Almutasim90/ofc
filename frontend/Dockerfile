# ---- build ----
FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Left empty on purpose: the reverse proxy serves the frontend and the API on
# the same origin (see deploy/nginx), so the app calls relative /api/... paths
# and needs no absolute API URL baked in. Only override this if the API is
# ever deployed on a different origin.
ARG VITE_API_URL=""
ENV VITE_API_URL=$VITE_API_URL
ARG VITE_PUBLIC_ORDER_URL=""
ENV VITE_PUBLIC_ORDER_URL=$VITE_PUBLIC_ORDER_URL
RUN npm run build

# ---- runtime ----
FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
