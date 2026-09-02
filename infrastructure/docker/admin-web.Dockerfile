# Gatus admin web — production image
#
# The frontend is pre-built on the host (`npm run build` in apps/admin-web)
# because npm registry access inside Docker builds is unreliable on some
# corporate networks (npm "Exit handler never called" stall).
#
# Build steps:
#   cd apps/admin-web && npm ci && npm run build
#   docker build -f infrastructure/docker/admin-web.Dockerfile .

FROM nginx:alpine
COPY apps/admin-web/dist /usr/share/nginx/html
COPY infrastructure/docker/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80 443
