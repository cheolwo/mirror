#!/usr/bin/env bash
set -Eeuo pipefail

# Public entry point validates the fixed deployment root. Tests source this file
# and exercise the transaction with command fixtures, never a real Docker host.
run_deployment() {
  local release_directory="$1" profile_name="$2" application_root="$3" project_name="$4"
  local timestamp override_name compose_file override_file environment_file
  timestamp="$(date -u +%Y%m%dT%H%M%SZ)-$$"
  override_name="compose.$profile_name.override.yaml"
  compose_file="$application_root/compose.yaml"
  override_file="$application_root/$override_name"
  environment_file="$application_root/.env"
  local web_next="$application_root/web-next-$timestamp"
  local web_backup="$application_root/web-before-$timestamp"
  local override_backup="$application_root/$override_name.before-$timestamp"
  local override_next="$application_root/$override_name.next-$timestamp"
  local override_existed=false web_existed=false image_loaded=false web_changed=false override_changed=false
  local old_image="" container_id old_files_list=""
  local -a compose_args=(-p "$project_name" --env-file "$environment_file" -f "$compose_file" -f "$override_file")
  local -a old_compose_args=(-p "$project_name" --env-file "$environment_file")

  for required_file in "$release_directory/ssalddel-server.tar" "$release_directory/web.tar.gz" \
    "$release_directory/$override_name" "$compose_file" "$environment_file"; do
    [[ -f "$required_file" ]] || { echo "Required deployment file is missing: $required_file" >&2; return 1; }
  done
  if [[ -f "$release_directory/deployment.sha256" ]]; then
    (cd "$release_directory" && sha256sum --check --strict deployment.sha256)
  fi
  if [[ -f "$override_file" ]]; then
    override_existed=true
    cp -p "$override_file" "$override_backup"
  fi
  [[ ! -d "$application_root/web" ]] || web_existed=true
  container_id="$(docker compose -p "$project_name" --env-file "$environment_file" -f "$compose_file" ps -q app)"
  if [[ -n "$container_id" ]]; then
    old_image="$(docker inspect "$container_id" --format '{{.Image}}')"
    old_files_list="$(docker inspect "$container_id" --format '{{index .Config.Labels "com.docker.compose.project.config_files"}}')"
    local -a old_files=()
    IFS=',' read -r -a old_files <<< "$old_files_list"
    for old_file in "${old_files[@]}"; do
      [[ -n "$old_file" ]] || continue
      case "$old_file" in "$application_root/"*) ;; *) echo "Previous Compose file is outside the deployment root." >&2; return 1 ;; esac
      [[ -f "$old_file" ]] || { echo "Previous Compose file is missing." >&2; return 1; }
      old_compose_args+=(-f "$old_file")
    done
    if [[ "${#old_compose_args[@]}" -eq 4 ]]; then
      old_compose_args+=(-f "$compose_file")
      [[ "$override_existed" != true ]] || old_compose_args+=(-f "$override_file")
    fi
    docker tag "$old_image" "ssalddel-server:azure-preview-rollback-$timestamp"
  fi

  rollback() {
    trap - ERR INT TERM
    set +e
    if [[ "$web_changed" == true ]]; then
      [[ ! -d "$application_root/web" ]] || mv "$application_root/web" "$application_root/web-failed-$timestamp"
      [[ "$web_existed" != true ]] || mv "$web_backup" "$application_root/web"
    fi
    if [[ "$override_changed" == true ]]; then
      if [[ "$override_existed" == true ]]; then
        cp -p "$override_backup" "$override_next"
        mv -f "$override_next" "$override_file"
      else
        [[ ! -f "$override_file" ]] || mv "$override_file" "$override_file.failed-$timestamp"
      fi
    fi
    if [[ -n "$old_image" && "$image_loaded" == true ]]; then
      docker tag "$old_image" ssalddel-server:azure-preview
      docker compose "${old_compose_args[@]}" up -d --no-deps --force-recreate app caddy
    elif [[ "$image_loaded" == true ]]; then
      # First isolated deployment: stop the failed project, retain all volumes.
      docker compose -p "$project_name" --env-file "$environment_file" -f "$compose_file" down
    fi
    echo "Deployment failed; previous image/web/Compose settings were restored. Schema rollback is not automatic." >&2
  }
  trap 'rollback; exit 1' ERR INT TERM

  # Prepare and validate everything before replacing visible files.
  cp "$release_directory/$override_name" "$override_next"
  docker compose -p "$project_name" --env-file "$environment_file" -f "$compose_file" -f "$override_next" config --quiet
  mkdir -p "$web_next"
  tar -xzf "$release_directory/web.tar.gz" -C "$web_next"
  test -f "$web_next/index.html"
  image_loaded=true
  docker load --input "$release_directory/ssalddel-server.tar"
  override_changed=true
  mv -f "$override_next" "$override_file"

  # Explicit migration approval is required for existing Operational databases.
  # The isolated mobile wrapper prepares its own databases and initialization.
  if [[ "$profile_name" != food-mobile-field-test ]]; then
    [[ "${SSALDDEL_DEPLOY_MIGRATIONS_VERIFIED:-}" == true ]] || {
      echo "Set SSALDDEL_DEPLOY_MIGRATIONS_VERIFIED=true only after same-image migration and backup verification." >&2
      false
    }
  else
    docker compose "${compose_args[@]}" up -d --wait --wait-timeout 120 mysql mongo
    docker compose "${compose_args[@]}" run --rm --no-deps app --initialize-database
  fi

  [[ "$web_existed" != true ]] || mv "$application_root/web" "$web_backup"
  web_changed=true
  mv "$web_next" "$application_root/web"
  docker compose "${compose_args[@]}" up -d --no-deps --force-recreate app
  local healthy=false
  for _ in $(seq 1 30); do
    container_id="$(docker compose "${compose_args[@]}" ps -q app)"
    local status
    status="$(docker inspect "$container_id" --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}')"
    if [[ "$status" == healthy ]]; then healthy=true; break; fi
    if [[ "$status" == unhealthy || "$status" == exited ]]; then break; fi
    sleep 2
  done
  [[ "$healthy" == true ]] || { echo "New application readiness failed." >&2; false; }
  # Caddy replacement belongs to the same failure/rollback boundary.
  docker compose "${compose_args[@]}" up -d --no-deps --force-recreate caddy
  trap - ERR INT TERM
  printf 'profile=%s\nproject=%s\nweb_backup=%s\noverride_backup=%s\n' \
    "$profile_name" "$project_name" "$web_backup" "$override_backup"
}

deploy_profile() {
  local release_directory="${1:?release directory is required}"
  local profile_name="${2:?deployment profile name is required}"
  local application_root="${3:-/opt/ssalddel}"
  local project_name=ssalddel-preview
  case "$profile_name" in
    orderer-v10|orderer-v15|transport-v20|fulfillment-v25|food-delivery-v30|mart-v35)
      [[ "$application_root" == /opt/ssalddel ]] || { echo "Application root must be /opt/ssalddel." >&2; return 1; }
      ;;
    food-mobile-field-test)
      [[ "$application_root" == /opt/ssalddel-mobile-field-test ]] || { echo "Mobile test root must be isolated." >&2; return 1; }
      [[ "${4:-}" == DEPLOY_ISOLATED_MOBILE_FIELD_TEST ]] || { echo "Explicit isolated field-test confirmation is required." >&2; return 1; }
      project_name=ssalddel-mobile-field-test
      # Its own 80/443 listener must not replace an existing preview gateway.
      [[ -z "$(docker ps -q --filter label=com.docker.compose.project=ssalddel-preview)" ]] || {
        echo "Use a separate mobile-test host; an Operational preview project is present." >&2; return 1;
      }
      ;;
    *) echo "Unsupported deployment profile: $profile_name" >&2; return 1 ;;
  esac
  run_deployment "$release_directory" "$profile_name" "$application_root" "$project_name"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  deploy_profile "$@"
fi
