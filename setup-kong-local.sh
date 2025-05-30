#!/bin/bash
# Script to configure Kong routes for locally running services (outside Docker)

echo "Setting up Kong routes for services running on localhost..."

# Remove any existing Kong services and routes
echo "Cleaning up existing Kong configuration..."
for service in $(curl -s http://localhost:8001/services | grep -o '"name":"[^"]*' | grep -o '[^"]*$'); do
    echo "Removing service: $service"
    curl -s -X DELETE http://localhost:8001/services/$service
done

# Create services pointing to host.docker.internal (the host machine from Docker's perspective)
echo "Creating Kong services for locally running microservices..."

# User Service
curl -s -X POST http://localhost:8001/services \
  --data name=user-service-local \
  --data url=http://host.docker.internal:5003
  
# Create route for User Service
curl -s -X POST http://localhost:8001/services/user-service-local/routes \
  --data name=users-route \
  --data "paths[]=/api/users" \
  --data "paths[]=/api/users/(.+)" \
  --data strip_path=false

# Add route for swagger
curl -s -X POST http://localhost:8001/services/user-service-local/routes \
  --data name=user-swagger-route \
  --data "paths[]=/swagger" \
  --data "paths[]=/swagger/(.+)" \
  --data strip_path=false

# Product Catalog Service
curl -s -X POST http://localhost:8001/services \
  --data name=product-service-local \
  --data url=http://host.docker.internal:5001
  
# Create route for Product Service
curl -s -X POST http://localhost:8001/services/product-service-local/routes \
  --data name=products-route \
  --data "paths[]=/api/products" \
  --data "paths[]=/api/products/(.+)" \
  --data strip_path=false

# Order Service
curl -s -X POST http://localhost:8001/services \
  --data name=order-service-local \
  --data url=http://host.docker.internal:5002
  
# Create route for Order Service
curl -s -X POST http://localhost:8001/services/order-service-local/routes \
  --data name=orders-route \
  --data "paths[]=/api/orders" \
  --data "paths[]=/api/orders/(.+)" \
  --data strip_path=false

# Notification Service
curl -s -X POST http://localhost:8001/services \
  --data name=notification-service-local \
  --data url=http://host.docker.internal:5004
  
# Create route for Notification Service
curl -s -X POST http://localhost:8001/services/notification-service-local/routes \
  --data name=notifications-route \
  --data "paths[]=/api/notifications" \
  --data "paths[]=/api/notifications/(.+)" \
  --data strip_path=false

curl -s -X POST http://localhost:8001/services/user-service-local/routes \
  --data name=auth-route \
  --data "paths[]=/api/auth" \
  --data "paths[]=/api/auth/(.+)" \
  --data strip_path=false
  
# Add CORS plugin to all services
for service in user-service-local product-service-local order-service-local notification-service-local; do
  curl -s -X POST http://localhost:8001/services/$service/plugins \
    --data name=cors \
    --data config.origins="*" \
    --data config.methods="GET,POST,PUT,DELETE,OPTIONS" \
    --data config.headers="Content-Type,Authorization" \
    --data config.exposed_headers="*"
done

echo "Kong configuration for local development complete!"
echo "Remember to run your services locally on ports 5001, 5002, 5003, and 5004"
