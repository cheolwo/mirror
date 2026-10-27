#!/usr/bin/env bash
set -Eeuo pipefail
script_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
exec bash "$script_directory/deploy-preview-profile.sh" "${1:?release directory is required}" \
  integrated-mobile-field-test /opt/mirror-integrated-field-test \
  "${2:?DEPLOY_ISOLATED_INTEGRATED_FIELD_TEST confirmation is required}"
