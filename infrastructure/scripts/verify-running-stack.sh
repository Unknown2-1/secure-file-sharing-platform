#!/bin/sh
set -eu

base_url="${VAULTSHARE_BASE_URL:-http://localhost:8080}"

curl -fsS "${base_url}/health/live" >/dev/null
curl -fsS "${base_url}/health/ready" >/dev/null
curl -fsS "${base_url}/" >/dev/null

docker compose exec -T api sh -ec 'test "$(id -u)" = 100; test "$(id -g)" = 101; test -w /var/lib/vaultshare/uploads'
docker compose exec -T worker sh -ec 'test "$(id -u)" = 100; test "$(id -g)" = 101; test -w /var/lib/vaultshare/uploads'
