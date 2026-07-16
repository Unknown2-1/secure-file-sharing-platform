#!/bin/sh
set -eu

mc alias set local http://minio:9000 "$OBJECT_STORAGE_ACCESS_KEY" "$OBJECT_STORAGE_SECRET_KEY"
mc mb --ignore-existing "local/$OBJECT_STORAGE_BUCKET"
mc anonymous set none "local/$OBJECT_STORAGE_BUCKET"

