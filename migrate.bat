@echo off
REM Database migration script for OrderService on Render free plan
REM This script runs EF migrations before starting the application

echo Starting database migration for OrderService...

REM Set environment variables
if "%ASPNETCORE_ENVIRONMENT%"=="" set ASPNETCORE_ENVIRONMENT=Production

REM Check if DATABASE_URL is set
if "%DATABASE_URL%"=="" (
    echo ERROR: DATABASE_URL environment variable is not set
    exit /b 1
)

echo Database URL is configured

REM Wait for database to be available (max 30 seconds)
echo Checking database connection...
for /l %%i in (1,1,30) do (
    dotnet ef database list --connection "%DATABASE_URL%" --no-build --project . >nul 2>&1
    if !errorlevel! equ 0 (
        echo Database connection established
        goto :connected
    )
    
    if %%i equ 30 (
        echo ERROR: Could not connect to database after 30 seconds
        exit /b 1
    )
    
    echo Waiting for database... (%%i/30)
    timeout /t 1 >nul
)

:connected

REM Check pending migrations
echo Checking for pending migrations...
dotnet ef migrations list --connection "%DATABASE_URL%" --no-build --project . 2>nul | find /c /v "" > temp_count.txt
set /p PENDING_MIGRATIONS=<temp_count.txt
del temp_count.txt

if %PENDING_MIGRATIONS% equ 0 (
    echo No pending migrations found
) else (
    echo Found %PENDING_MIGRATIONS% pending migration(s)
    
    REM Apply migrations
    echo Applying database migrations...
    dotnet ef database update --connection "%DATABASE_URL%" --no-build --project .
    if !errorlevel! neq 0 (
        echo ERROR: Failed to apply database migrations
        exit /b 1
    )
    echo Database migrations applied successfully
)

REM Verify migrations were applied
echo Verifying migration status...
dotnet ef database list --connection "%DATABASE_URL%" --no-build --project . 2>nul | find /c /v "" > temp_count.txt
set /p APPLIED_MIGRATIONS=<temp_count.txt
del temp_count.txt
echo Total applied migrations: %APPLIED_MIGRATIONS%

echo Database migration completed successfully
echo Starting OrderService...

REM Start the application
dotnet OrderService.dll
