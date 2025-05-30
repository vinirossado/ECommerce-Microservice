#!/bin/bash
# Script to automatically generate Kong configuration from OpenAPI specifications

echo "Generating Kong configuration from your ASP.NET Core API endpoints..."

# Directory to store OpenAPI specs
mkdir -p ./kong-config

# Function to extract paths from OpenAPI spec and create Kong routes
generate_routes() {
  SERVICE_NAME=$1
  HOST=$2
  PORT=$3
  ENV_NAME=$4 # docker or local
  
  echo "Processing $SERVICE_NAME for environment $ENV_NAME..."
  
  # Create Kong service
  curl -s -X POST http://localhost:8001/services \
    --data name=$SERVICE_NAME \
    --data url=http://$HOST:$PORT
  
  # Hard-coded routes based on known API structure
  # This is more reliable than trying to parse the Swagger docs automatically
  
  # Auth routes
  curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
    --data name=$SERVICE_NAME-auth-route \
    --data "paths[]=/api/auth" \
    --data "paths[]=/api/auth/(.+)" \
    --data strip_path=false
  
  # Users routes
  curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
    --data name=$SERVICE_NAME-users-route \
    --data "paths[]=/api/users" \
    --data "paths[]=/api/users/(.+)" \
    --data strip_path=false
  
  # Health routes
  curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
    --data name=$SERVICE_NAME-health-route \
    --data "paths[]=/api/health" \
    --data "paths[]=/health" \
    --data strip_path=false
  
  # Products routes (for product service)
  if [[ $SERVICE_NAME == *"product"* ]]; then
    curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
      --data name=$SERVICE_NAME-products-route \
      --data "paths[]=/api/products" \
      --data "paths[]=/api/products/(.+)" \
      --data strip_path=false
  fi
  
  # Orders routes (for order service)
  if [[ $SERVICE_NAME == *"order"* ]]; then
    curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
      --data name=$SERVICE_NAME-orders-route \
      --data "paths[]=/api/orders" \
      --data "paths[]=/api/orders/(.+)" \
      --data strip_path=false
  fi
  
  # Notifications routes (for notification service)
  if [[ $SERVICE_NAME == *"notification"* ]]; then
    curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
      --data name=$SERVICE_NAME-notifications-route \
      --data "paths[]=/api/notifications" \
      --data "paths[]=/api/notifications/(.+)" \
      --data strip_path=false
  fi
  
  # Swagger routes
  curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/routes \
    --data name=$SERVICE_NAME-swagger-route \
    --data "paths[]=/swagger" \
    --data "paths[]=/swagger/(.+)" \
    --data strip_path=false
    
  # Add CORS plugin
  curl -s -X POST http://localhost:8001/services/$SERVICE_NAME/plugins \
    --data name=cors \
    --data config.origins="*" \
    --data config.methods="GET,POST,PUT,DELETE,OPTIONS" \
    --data config.headers="Content-Type,Authorization" \
    --data config.exposed_headers="*"
}

# Clean up existing Kong configuration first
echo "Cleaning up existing Kong configuration..."
for service in $(curl -s http://localhost:8001/services | grep -o '"name":"[^"]*' | grep -o '[^"]*$'); do
    echo "Removing service: $service"
    curl -s -X DELETE http://localhost:8001/services/$service
done

# Detect option
if [ "$1" == "docker" ]; then
  echo "Configuring Kong for Docker environment..."
  # Docker services
  generate_routes "user-service" "user-service" "8080" "docker"
  generate_routes "product-service" "product-catalog-service" "8080" "docker"
  generate_routes "order-service" "order-service" "8080" "docker"
  generate_routes "notification-service" "notification-service" "8080" "docker"
else
  echo "Configuring Kong for local development environment..."
  # Local services
  generate_routes "user-service" "host.docker.internal" "5003" "local"
  generate_routes "product-service" "host.docker.internal" "5001" "local"
  generate_routes "order-service" "host.docker.internal" "5002" "local"
  generate_routes "notification-service" "host.docker.internal" "5004" "local"
fi

echo "Kong configuration complete! Try accessing:"
if [ "$1" == "docker" ]; then
  echo "- http://localhost:8000/api/auth/register for Docker services"
else
  echo "- http://localhost:8000/api/auth/register for local services"
fi

