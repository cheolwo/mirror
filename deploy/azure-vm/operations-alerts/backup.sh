#!/usr/bin/env bash
# 운영 Linux VM 전용. 외부 실행/업로드는 이 스크립트를 명시적으로 실행했을 때만 수행.
set -euo pipefail
umask 077
: "${MIRROR_BACKUP_RECIPIENT:?age public recipient required}"
: "${MIRROR_BACKUP_STORAGE_ACCOUNT:?separate Azure storage account required}"
: "${MIRROR_BACKUP_CONTAINER:?private backup container required}"
: "${MIRROR_COMPOSE_DIR:?absolute compose directory required}"
: "${MIRROR_PROTECTION_KEYS_BACKUP_VERIFIED:?separate certificate/private encryption keys backup must be verified}"
[[ "$MIRROR_PROTECTION_KEYS_BACKUP_VERIFIED" == true ]] || exit 2
[[ "$MIRROR_COMPOSE_DIR" = /* ]] || exit 2
for command in docker age az sha256sum tar flock; do command -v "$command" >/dev/null; done
exec 9>/run/lock/mirror-backup.lock
flock -n 9 || exit 0
scratch=$(mktemp -d)
trap 'rm -rf -- "$scratch"' EXIT
cd "$MIRROR_COMPOSE_DIR"
# 각 DB에 일관된 dump. 서로 다른 DB의 전역 원자 snapshot이라고 주장하지 않는다.
docker compose exec -T mysql sh -c 'MYSQL_PWD="$MYSQL_PASSWORD" exec mysqldump -u "$MYSQL_USER" --single-transaction --routines --events "$MYSQL_DATABASE"' > "$scratch/mysql.sql"
docker compose exec -T mongo sh -c 'exec mongodump --username "$MONGO_INITDB_ROOT_USERNAME" --password "$MONGO_INITDB_ROOT_PASSWORD" --authenticationDatabase admin --archive --gzip' > "$scratch/mongo.archive.gz"
docker compose exec -T app tar -C /app/App_Data -cf - DataProtection-Keys > "$scratch/keyring.tar"
[[ -s "$scratch/mysql.sql" && -s "$scratch/mongo.archive.gz" && -s "$scratch/keyring.tar" ]]
(cd "$scratch"; sha256sum mysql.sql mongo.archive.gz keyring.tar > SHA256SUMS)
tar -C "$scratch" -cf - mysql.sql mongo.archive.gz keyring.tar SHA256SUMS | age -r "$MIRROR_BACKUP_RECIPIENT" -o "$scratch/backup.tar.age"
stamp=$(date -u +%Y%m%dT%H%M%SZ)
az storage blob upload --auth-mode login --account-name "$MIRROR_BACKUP_STORAGE_ACCOUNT" --container-name "$MIRROR_BACKUP_CONTAINER" --name "backup/daily/$stamp.tar.age" --file "$scratch/backup.tar.age" --overwrite false --only-show-errors -o none
if [[ $(date -u +%u) == 7 ]]; then
  az storage blob upload --auth-mode login --account-name "$MIRROR_BACKUP_STORAGE_ACCOUNT" --container-name "$MIRROR_BACKUP_CONTAINER" --name "backup/weekly/$stamp.tar.age" --file "$scratch/backup.tar.age" --overwrite false --only-show-errors -o none
fi
logger -t mirror-backup 'BACKUP_OK'
