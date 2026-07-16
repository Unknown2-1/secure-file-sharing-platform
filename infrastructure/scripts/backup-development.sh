#!/bin/sh
set -eu

mkdir -p backups
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"
destination="backups/vaultshare-${timestamp}.dump"
docker compose exec -T postgres pg_dump -U "${POSTGRES_USER:-vaultshare}" -d "${POSTGRES_DB:-vaultshare}" -Fc > "$destination"
chmod 600 "$destination"
echo "Development database backup created at $destination"
