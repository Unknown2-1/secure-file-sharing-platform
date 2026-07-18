#!/bin/sh
set -eu

require_line() {
  file="$1"
  pattern="$2"
  grep -Eq "$pattern" "$file" || {
    echo "Missing required pattern in ${file}: ${pattern}" >&2
    exit 1
  }
}

forbidden_line() {
  file="$1"
  pattern="$2"
  if grep -Eq "$pattern" "$file"; then
    echo "Forbidden pattern in ${file}: ${pattern}" >&2
    exit 1
  fi
}

docker compose config --quiet
require_line railway.toml '^builder = "DOCKERFILE"$'
require_line railway.toml '^dockerfilePath = "infrastructure/docker/api.Dockerfile"$'
require_line railway.worker.toml '^dockerfilePath = "infrastructure/docker/worker.Dockerfile"$'
forbidden_line railway.toml 'CLAMAV_FAIL_CLOSED[[:space:]]*=[[:space:]]*"false"'
forbidden_line railway.worker.toml 'CLAMAV_FAIL_CLOSED[[:space:]]*=[[:space:]]*"false"'
test ! -e backend/railway.toml
test ! -e backend/Dockerfile
