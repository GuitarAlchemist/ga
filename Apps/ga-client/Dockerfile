# Build stage
FROM node:20-alpine AS build
WORKDIR /app

# Copy package files
COPY package*.json ./

# Install dependencies
RUN npm ci

# Copy source code
COPY . .

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
COPY --from=build /app/dist .

# Copy nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

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
