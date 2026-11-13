# Stage 1: build the React app
FROM node:22-alpine AS web
WORKDIR /web
COPY Tabby-ui/package.json Tabby-ui/package-lock.json ./
RUN npm ci
COPY Tabby-ui/ .
# Empty API address: the app calls /api/... on the same address it was loaded from,
# which is Nginx, so it reaches the replicas through the load balancer
ENV VITE_API_URL=""
RUN npm run build

# Stage 2: Nginx with the built app and a self-signed certificate
FROM nginx:1.27-alpine
RUN apk add --no-cache openssl \
 && mkdir -p /etc/nginx/certs \
 && openssl req -x509 -nodes -newkey rsa:2048 -days 365 \
      -keyout /etc/nginx/certs/tabby.key -out /etc/nginx/certs/tabby.crt \
      -subj "/CN=localhost" \
      -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"
COPY --from=web /web/dist /usr/share/nginx/html
COPY Tabby.Api/nginx/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80 443