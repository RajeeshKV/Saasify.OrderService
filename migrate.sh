#!/bin/bash

# Database migration script for OrderService on Render free plan
# This script runs EF migrations before starting the application

echo "Starting database migration for OrderService..."

# Set environment variables
export ASPNETCORE_ENVIRONMENT=${ASPNETCORE_ENVIRONMENT:-Production}
export DATABASE_URL=${DATABASE_URL}

# Check if DATABASE_URL is set
if [ -z "$DATABASE_URL" ]; then
    echo "ERROR: DATABASE_URL environment variable is not set"
    exit 1
fi

echo "Database URL is configured"

# Wait for database to be available (max 30 seconds)
echo "Checking database connection..."
for i in {1..30}; do
    if dotnet ef database list --connection "$DATABASE_URL" > /dev/null 2>&1; then
        echo "Database connection established"
        break
    fi
    
    if [ $i -eq 30 ]; then
        echo "ERROR: Could not connect to database after 30 seconds"
        exit 1
    fi
    
    echo "Waiting for database... ($i/30)"
    sleep 1
done

# Check pending migrations
echo "Checking for pending migrations..."
PENDING_MIGRATIONS=$(dotnet ef migrations list --connection "$DATABASE_URL" --no-build --project . 2>/dev/null | wc -l)

if [ "$PENDING_MIGRATIONS" -eq 0 ]; then
    echo "No pending migrations found"
else
    echo "Found $PENDING_MIGRATIONS pending migration(s)"
    
    # Apply migrations
    echo "Applying database migrations..."
    if dotnet ef database update --connection "$DATABASE_URL" --no-build --project .; then
        echo "Database migrations applied successfully"
    else
        echo "ERROR: Failed to apply database migrations"
        exit 1
    fi
fi

# Verify migrations were applied
echo "Verifying migration status..."
APPLIED_MIGRATIONS=$(dotnet ef database list --connection "$DATABASE_URL" --no-build --project . 2>/dev/null | wc -l)
echo "Total applied migrations: $APPLIED_MIGRATIONS"

echo "Database migration completed successfully"
echo "Starting OrderService..."

# Start the application
exec dotnet OrderService.dll
