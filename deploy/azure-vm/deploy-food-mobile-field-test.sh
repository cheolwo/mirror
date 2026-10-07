#!/usr/bin/env bash
set -Eeuo pipefail
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$script_directory/deploy-preview-profile.sh" "${1:?release directory is required}" \
  food-mobile-field-test /opt/ssalddel-mobile-field-test "${2:?DEPLOY_ISOLATED_MOBILE_FIELD_TEST confirmation is required}"
