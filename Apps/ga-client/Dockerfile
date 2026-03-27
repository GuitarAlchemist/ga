# Build stage — context must be the repo root (see docker-compose.yml)
FROM node:20-alpine AS build

# Replicate the monorepo directory structure so relative imports resolve
WORKDIR /repo/Apps/ga-client

# Copy ga-react-components first (needed as file: dependency and for source imports)
COPY ReactComponents/ga-react-components /repo/ReactComponents/ga-react-components

# Copy ga-client package files and install dependencies
COPY Apps/ga-client/package*.json ./
RUN npm ci

# Copy ga-client source code
COPY Apps/ga-client/ .

# Build the application
RUN npm run build

# Production stage
FROM nginx:1.27-alpine AS final
WORKDIR /usr/share/nginx/html

# Create non-root user for nginx
RUN addgroup -g 1001 -S nginx_user && adduser -S -D -H -u 1001 -h /var/cache/nginx -s /sbin/nologin -c nginx -G nginx_user nginx_user

# Remove default nginx static assets
RUN rm -rf ./*

# Copy built assets from build stage
COPY --from=build /repo/Apps/ga-client/dist .

# Copy nginx configuration
COPY Apps/ga-client/nginx.conf /etc/nginx/conf.d/default.conf

# Set proper permissions
RUN chown -R nginx_user:nginx_user /usr/share/nginx/html && \
    chown -R nginx_user:nginx_user /var/cache/nginx && \
    chown -R nginx_user:nginx_user /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown nginx_user:nginx_user /var/run/nginx.pid

EXPOSE 80

# Switch to non-root user
USER nginx_user

CMD ["nginx", "-g", "daemon off;"]
