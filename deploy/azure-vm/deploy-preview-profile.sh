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
  if [[ "$profile_name" != food-mobile-field-test && "$profile_name" != integrated-mobile-field-test ]]; then
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
    integrated-mobile-field-test)
      [[ "$application_root" == /opt/mirror-integrated-field-test ]] || { echo "Integrated test root must be isolated." >&2; return 1; }
      [[ "${4:-}" == DEPLOY_ISOLATED_INTEGRATED_FIELD_TEST ]] || { echo "Explicit isolated integrated field-test confirmation is required." >&2; return 1; }
      [[ "$(readlink -f -- "$application_root")" == "$application_root" ]] || { echo "Integrated test root must be a real dedicated directory, not a linked root." >&2; return 1; }
      project_name=mirror-integrated-field-test
      [[ -f "$release_directory/deployment.sha256" ]] || { echo "Integrated deployment checksums are required." >&2; return 1; }
      # Initial setup copies these reviewed files. Do not silently replace an existing gateway/base.
      cmp -s "$release_directory/Caddyfile" "$application_root/Caddyfile" \
        && cmp -s "$release_directory/compose.yaml" "$application_root/compose.yaml" || {
          echo "Integrated base Compose/Caddy must match the reviewed bundle; prepare the dedicated root first." >&2; return 1;
        }
      command -v python3 >/dev/null || { echo "Python3 is required for the offline profile guard." >&2; return 1; }
      validate_integrated_field_profile "$release_directory" || return 1
      for occupied_project in ssalddel-preview ssalddel-mobile-field-test mirror-integrated-preparation; do
        [[ -z "$(docker ps -q --filter "label=com.docker.compose.project=$occupied_project")" ]] || {
          echo "Use a separate integrated-test host; another known preview/preparation project is present." >&2; return 1;
        }
      done
      validate_integrated_public_ports || return 1
      ;;
    *) echo "Unsupported deployment profile: $profile_name" >&2; return 1 ;;
  esac
  run_deployment "$release_directory" "$profile_name" "$application_root" "$project_name"
}

validate_integrated_public_ports() {
  local port ids id owner
  for port in 80 443; do
    ids="$(docker ps -q --filter "publish=$port")" || return 1
    for id in $ids; do
      owner="$(docker inspect "$id" --format '{{index .Config.Labels "com.docker.compose.project"}}')" || return 1
      [[ "$owner" == mirror-integrated-field-test ]] || {
        echo "Another container owns a public gateway port; use a dedicated integrated-test host." >&2; return 1;
      }
    done
  done
}

# Fixed field-test guards are checked again on the host, before image/DB changes.
# Generic projections/inbox HostedServices may still run in this isolated synthetic DB.
validate_integrated_field_profile() {
  python3 - "$1" <<'PY'
import json, pathlib, sys
try:
    root = pathlib.Path(sys.argv[1])
    p = json.loads((root/'compose.integrated-mobile-field-test.override.yaml').read_text(encoding='utf-8-sig'))
    manifest = json.loads((root/'deployment-manifest.json').read_text(encoding='utf-8-sig'))
    m = p['x-mirror-field-test']; e = p['services']['app']['environment']
    assert p['name'] == manifest['composeProject'] == 'mirror-integrated-field-test'
    assert manifest['profile'] == 'integrated-mobile-field-test' and manifest['runtimeMode'] == e['SsalddelExecution__Mode'] == 'Simulation'
    assert e['ASPNETCORE_ENVIRONMENT'] == 'Staging' and manifest['deploymentRoot'] == '/opt/mirror-integrated-field-test'
    assert manifest['automaticCloudDeployment'] is False and manifest['physicalDeviceVerified'] is False
    assert m['autoShutdown'] is False and m['apiInstances'] == p['services']['app']['deploy']['replicas'] == 1
    assert m['allHostedServicesDisabled'] is False and m['generalProjectionAndInboxWorkersMayRun'] is True
    assert m['automaticDispatchEnabled'] is False and m['financialExecutionEnabled'] is False and m['weeklyPreparationEnabled'] is False and m['publicReleaseReady'] is False
    assert e['NeighborhoodMemberDelivery__Enabled'] == 'true' and e['NeighborhoodMemberDelivery__OperationalEnabled'] == 'false'
    for key in ['TransportPrepayment__Enabled','DriverPayoutBatch__Enabled','DriverPayoutBatch__AutomaticPreparationEnabled','MobileFieldTest__FoodDispatch__Enabled','FoodObserverVerification__Enabled','ObservableOperationsVerification__Enabled','FulfillmentRouting__Enabled','FreightContinuity__Enabled','DistributedFulfillment__Enabled','NeighborhoodSettlement__Enabled','SharedDriverWork__Enabled','DatabaseInitialization__RunAtStartup','IdentitySeed__BootstrapAdmin__Enabled']:
        assert e[key] == 'false'
    assert not e['TossPayments__ClientKey'] and not e['TossPayments__SecretKey'] and not e['PushNotifications__ServerKey']
    assert p['services']['mysql']['environment']['MYSQL_DATABASE'] == p['services']['mongo']['environment']['MONGO_INITDB_DATABASE'] == e['MongoDb__Database'] == 'mirror_integrated_field_test'
    parts = [part.split('=',1) for part in e['ConnectionStrings__DefaultConnection'].split(';') if part]
    assert len([part for part in parts if part[0].lower() == 'database']) == 1
    connection = {key.lower(): value for key, value in parts}
    assert connection['server'] == 'mysql' and connection['database'] == 'mirror_integrated_field_test'
    assert e['ObjectStorage__Provider'] == 'GoogleCloud' and not e['GoogleCloudStorage__PublicBucketName'] and not e['GoogleCloudStorage__PublicBaseUrl']
    assert e['GoogleCloudStorage__PrivateBucketName'].startswith('${MIRROR_FIELD_PRIVATE_BUCKET:?')
    assert e['GoogleCloudStorage__BucketName'] == e['GoogleCloudStorage__PrivateBucketName']
    assert all(v['read_only'] is True and v['bind']['create_host_path'] is False and v['source'].startswith('${MIRROR_') for v in p['services']['app']['volumes'])
    assert all(p['volumes'][v]['name'] == 'mirror-integrated-field-test-'+v.replace('_','-') for v in ['mysql_data','mongo_data','app_data','app_uploads','caddy_data','caddy_config'])
except Exception:
    sys.exit('Integrated field-test profile/manifest is invalid; no deployment changes were performed.')
PY
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  deploy_profile "$@"
fi
