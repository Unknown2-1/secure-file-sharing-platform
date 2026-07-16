#!/bin/sh
set -eu

size_mb="${1:-1}"
case "$size_mb" in *[!0-9]*|'') echo "Usage: $0 SIZE_MB [OUTPUT]" >&2; exit 2 ;; esac
[ "$size_mb" -gt 0 ] || { echo "SIZE_MB must be positive" >&2; exit 2; }
destination="${2:-/tmp/vaultshare-benchmark-${size_mb}mb.bin}"
dd if=/dev/urandom of="$destination" bs=1M count="$size_mb" status=progress
chmod 600 "$destination"
echo "$destination"
