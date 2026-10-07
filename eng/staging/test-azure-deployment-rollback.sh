#!/usr/bin/env bash
# Offline command fixtures: no Docker daemon, DB, VM or signing key is used.
set -Eeuo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
temporary_root="$(mktemp -d)"
trap '[[ "$temporary_root" == /tmp/* || "$temporary_root" == /c/* || "$temporary_root" == /d/* ]] && rm -rf -- "$temporary_root"' EXIT
mkdir -p "$temporary_root/bin"
cat > "$temporary_root/bin/docker" <<'DOCKER'
#!/usr/bin/env bash
set -eu
printf '%q ' "$@" >> "$FIX_ROOT/commands.log"; printf '\n' >> "$FIX_ROOT/commands.log"
if [[ "$1" == ps ]]; then
  [[ "${FIX_OPERATIONAL_PRESENT:-0}" != 1 ]] || printf 'operational-container\n'
elif [[ "$1" == tag ]]; then
  [[ "$2" != sha256:old-image ]] || printf OLD > "$FIX_ROOT/image-state"
elif [[ "$1" == load ]]; then
  printf NEW > "$FIX_ROOT/image-state"
  [[ "$FIX_FAIL" != load ]] || exit 9
elif [[ "$1" == inspect ]]; then
  case "${*: -1}" in
    '{{.Image}}') printf 'sha256:old-image\n' ;;
    *project.config_files*) printf '%s\n' "$FIX_ACTIVE_FILES" ;;
    *) if [[ "$FIX_FAIL" == health ]]; then printf 'unhealthy\n'; else printf 'healthy\n'; fi ;;
  esac
elif [[ "$1" == compose ]]; then
  case " $* " in
    *" config "*) [[ "$FIX_FAIL" != config ]] || exit 9 ;;
    *" ps -q app"*) [[ "$FIX_OLD" != 1 && ! -f "$FIX_ROOT/app-started" ]] || printf 'app-container\n' ;;
    *" run "*) [[ "$FIX_FAIL" != initialize ]] || exit 9 ;;
    *" up "*)
      if [[ "${*: -1}" == app ]]; then
        touch "$FIX_ROOT/app-started"
        [[ "$FIX_FAIL" != app || "$(cat "$FIX_ROOT/image-state")" == OLD ]] || exit 9
      elif [[ "${*: -1}" == caddy && "$FIX_FAIL" == caddy && "$(cat "$FIX_ROOT/image-state")" != OLD ]]; then exit 9
      fi ;;
    *" down "*) touch "$FIX_ROOT/project-stopped" ;;
  esac
fi
DOCKER
chmod +x "$temporary_root/bin/docker"
export PATH="$temporary_root/bin:$PATH"
passed=0
for failure in config load initialize app health caddy success; do
  case_root="$temporary_root/$failure"
  mkdir -p "$case_root/application/web" "$case_root/release" "$case_root/new-web"
  printf OLD > "$case_root/image-state"
  printf 'old-web' > "$case_root/application/web/index.html"
  printf 'old-override' > "$case_root/application/compose.food-mobile-field-test.override.yaml"
  printf 'base' > "$case_root/application/compose.yaml"
  printf 'synthetic-fixture=true' > "$case_root/application/.env"
  printf 'new-web' > "$case_root/new-web/index.html"
  printf 'new-override' > "$case_root/release/compose.food-mobile-field-test.override.yaml"
  printf 'fixture-image' > "$case_root/release/ssalddel-server.tar"
  tar -czf "$case_root/release/web.tar.gz" -C "$case_root/new-web" .
  export FIX_ROOT="$case_root" FIX_FAIL="$failure" FIX_OLD=1
  export FIX_ACTIVE_FILES="$case_root/application/compose.yaml,$case_root/application/compose.food-mobile-field-test.override.yaml"
  if bash -c 'source "$1"; run_deployment "$2/release" food-mobile-field-test "$2/application" fixture-mobile' \
      _ "$repo_root/deploy/azure-vm/deploy-preview-profile.sh" "$case_root" > "$case_root/result.log" 2>&1; then
    [[ "$failure" == success ]] || { cat "$case_root/result.log"; echo "Expected failure: $failure" >&2; exit 1; }
    [[ "$(cat "$case_root/application/web/index.html")" == new-web ]]
    [[ "$(cat "$case_root/application/compose.food-mobile-field-test.override.yaml")" == new-override ]]
    [[ "$(cat "$case_root/image-state")" == NEW ]]
    grep -q -- '--initialize-database' "$case_root/commands.log"
  else
    [[ "$failure" != success ]] || { cat "$case_root/result.log"; exit 1; }
    [[ "$(cat "$case_root/application/web/index.html")" == old-web ]]
    [[ "$(cat "$case_root/application/compose.food-mobile-field-test.override.yaml")" == old-override ]]
    [[ "$(cat "$case_root/image-state")" == OLD ]]
    if [[ "$failure" != config ]]; then grep -q 'app caddy' "$case_root/commands.log"; fi
  fi
  ((passed+=1))
done
# The initial failed project is stopped without volume deletion, never rolled
# into the Operational project. Its new override/web are withdrawn.
case_root="$temporary_root/first-failure"
mkdir -p "$case_root/application" "$case_root/release" "$case_root/new-web"
printf base > "$case_root/application/compose.yaml"
printf fixture > "$case_root/application/.env"
printf new-web > "$case_root/new-web/index.html"
printf new-override > "$case_root/release/compose.food-mobile-field-test.override.yaml"
printf image > "$case_root/release/ssalddel-server.tar"
tar -czf "$case_root/release/web.tar.gz" -C "$case_root/new-web" .
export FIX_ROOT="$case_root" FIX_FAIL=caddy FIX_OLD=0 FIX_ACTIVE_FILES=""
if bash -c 'source "$1"; run_deployment "$2/release" food-mobile-field-test "$2/application" fixture-mobile' \
    _ "$repo_root/deploy/azure-vm/deploy-preview-profile.sh" "$case_root" > "$case_root/result.log" 2>&1; then exit 1; fi
test -f "$case_root/project-stopped"
test ! -d "$case_root/application/web"
test ! -f "$case_root/application/compose.food-mobile-field-test.override.yaml"
! grep -q -- 'down -v' "$case_root/commands.log"
((passed+=1))
# Dedicated host/confirmation guards run before touching deployment files.
export FIX_OPERATIONAL_PRESENT=1
if bash "$repo_root/deploy/azure-vm/deploy-preview-profile.sh" "$case_root/release" food-mobile-field-test \
    /opt/ssalddel-mobile-field-test DEPLOY_ISOLATED_MOBILE_FIELD_TEST > "$case_root/guard.log" 2>&1; then exit 1; fi
grep -q 'separate mobile-test host' "$case_root/guard.log"
unset FIX_OPERATIONAL_PRESENT
((passed+=1))
for mode in both aes-only salt-only neither; do
  secret_root="$temporary_root/secrets-$mode"
  mkdir -p "$secret_root"
  printf 'OTHER=fixture\n' > "$secret_root/.env"
  [[ "$mode" != both && "$mode" != aes-only ]] || printf 'SSALDDEL_ISMS_P_AES_KEY_BASE64=fixture-existing-key\n' >> "$secret_root/.env"
  [[ "$mode" != both && "$mode" != salt-only ]] || printf 'SSALDDEL_ISMS_P_HASH_SALT=fixture-existing-salt\n' >> "$secret_root/.env"
  before_hash="$(sha256sum "$secret_root/.env" | cut -d' ' -f1)"
  if bash "$repo_root/deploy/azure-vm/provision-preview-secrets.sh" "$secret_root/.env" > "$secret_root/result.log" 2>&1; then
    [[ "$mode" == both || "$mode" == neither ]] || exit 1
    if [[ "$mode" == both ]]; then
      [[ "$before_hash" == "$(sha256sum "$secret_root/.env" | cut -d' ' -f1)" ]]
    else
      [[ "$(grep -c '^SSALDDEL_ISMS_P_AES_KEY_BASE64=.' "$secret_root/.env")" == 1 ]]
      [[ "$(grep -c '^SSALDDEL_ISMS_P_HASH_SALT=.' "$secret_root/.env")" == 1 ]]
      compgen -G "$secret_root/.env.before-ismp-*" > /dev/null
    fi
  else
    [[ "$mode" == aes-only || "$mode" == salt-only ]] || exit 1
    [[ "$before_hash" == "$(sha256sum "$secret_root/.env" | cut -d' ' -f1)" ]]
    ! compgen -G "$secret_root/.env.before-ismp-*" > /dev/null
  fi
  ! grep -q 'fixture-existing-' "$secret_root/result.log"
  ((passed+=1))
done
printf 'Offline Bash fixtures passed: %s\n' "$passed"
