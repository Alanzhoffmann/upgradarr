#!/bin/sh
set -e

dotnet /app/migrations/Huntarr.Net.Migrations.dll
exec /app/Huntarr.Net.Api
