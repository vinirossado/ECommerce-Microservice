#!/bin/bash
# Script to configure Kong routes for containerized services

echo "Setting up Kong routes for containerized services..."

# Remove any existing Kong services and routes
echo "Cleaning up existing Kong configuration..."
for service in $(curl -s http://localhost:8001/services | grep -o '"name":"[^"]*' | grep -o '[^"]*$'); do
    echo "Removing service: $service"
    curl -s -X DELETE http://localhost:8001/services/$service
done

# Create services pointing to container names
echo "Creating Kong services for containerized microservices..."

# User Service
curl -s -X POST http://localhost:8001/services \
  --data name=user-service \
  --data url=http://user-service:8080
  
# Create route for User Service
curl -s -X POST http://localhost:8001/services/user-service/routes \
  --data name=users-route \
  --data "paths[]=/api/users" \
  --data "paths[]=/api/users/(.+)" \
  --data strip_path=false

# Add route for swagger
curl -s -X POST http://localhost:8001/services/user-service/routes \
  --data name=user-swagger-route \
  --data "paths[]=/swagger" \
  --data "paths[]=/swagger/(.+)" \
  --data strip_path=false
  
    
curl -s -X POST http://localhost:8001/services/user-service/routes \
  --data name=auth-route \
  --data "paths[]=/api/auth" \
  --data "paths[]=/api/auth/(.+)" \
  --data strip_path=false

# Product Catalog Service
curl -s -X POST http://localhost:8001/services \
  --data name=product-service \
  --data url=http://product-catalog-service:8080
  
# Create route for Product Service
curl -s -X POST http://localhost:8001/services/product-service/routes \
  --data name=products-route \
  --data "paths[]=/api/products" \
  --data "paths[]=/api/products/(.+)" \
  --data strip_path=false

# Order Service
curl -s -X POST http://localhost:8001/services \
  --data name=order-service \
  --data url=http://order-service:8080
  
# Create route for Order Service
curl -s -X POST http://localhost:8001/services/order-service/routes \
  --data name=orders-route \
  --data "paths[]=/api/orders" \
  --data "paths[]=/api/orders/(.+)" \
  --data strip_path=false

# Notification Service
curl -s -X POST http://localhost:8001/services \
  --data name=notification-service \
  --data url=http://notification-service:8080
  
# Create route for Notification Service
curl -s -X POST http://localhost:8001/services/notification-service/routes \
  --data name=notifications-route \
  --data "paths[]=/api/notifications" \
  --data "paths[]=/api/notifications/(.+)" \
  --data strip_path=false

# Add CORS plugin to all services
for service in user-service product-service order-service notification-service; do
  curl -s -X POST http://localhost:8001/services/$service/plugins \
    --data name=cors \
    --data config.origins="*" \
    --data config.methods="GET,POST,PUT,DELETE,OPTIONS" \
    --data config.headers="Content-Type,Authorization" \
    --data config.exposed_headers="*"
done

echo "Kong configuration complete!"
