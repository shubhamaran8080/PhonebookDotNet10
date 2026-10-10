# Build the Vue app with Vite...
FROM node:20-alpine AS build

WORKDIR /app

# The SPA talks to the backend through a reverse proxy ("/api" by
# default), so no build-time configuration is required. Override
# VITE_API_BASE_URL if the API is served from another origin.
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

# ...then serve the static files with nginx.
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
