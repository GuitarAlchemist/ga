# Build stage
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build -- --configuration=production

# Production stage
FROM nginx:1.27-alpine AS final

# Create non-root user for nginx
RUN addgroup -g 1001 -S nginx_user && adduser -S -D -H -u 1001 -h /var/cache/nginx -s /sbin/nologin -c nginx -G nginx_user nginx_user

# Copy built assets
COPY --from=build /app/dist/ga-dashboard/browser /usr/share/nginx/html

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
