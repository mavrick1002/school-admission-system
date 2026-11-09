#!/bin/sh
set -eu

api_base_url="${API_BASE_URL:-http://localhost:8080}"
escaped=$(printf '%s' "$api_base_url" | tr -d '\r\n' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')

printf 'window.__APP_CONFIG__ = { API_BASE_URL: "%s" };\n' "$escaped" > /usr/share/nginx/html/config.js
echo "generate-config: API_BASE_URL=$api_base_url"
