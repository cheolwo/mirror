#!/usr/bin/env bash
# 내려받은 암호화 백업을 새 임시 디렉터리에 해독·무결성 확인. 운영 DB에 자동 restore하지 않는다.
set -euo pipefail
umask 077
[[ $# == 3 ]] || { echo 'usage: restore-check.sh backup.tar.age age-identity-file NEW-output-directory' >&2; exit 2; }
[[ -f "$1" && -f "$2" && ! -e "$3" ]] || exit 2
mkdir -p -- "$3"
age -d -i "$2" "$1" > "$3/archive.tar"
# 정해진 원본만 추출하고 임의 경로/심볼릭 링크를 복구하지 않는다.
tar -tf "$3/archive.tar" | while IFS= read -r member; do
  case "$member" in mysql.sql|mongo.archive.gz|keyring.tar|SHA256SUMS) ;; *) exit 2 ;; esac
done
[[ $(tar -tvf "$3/archive.tar" | wc -l) == 4 ]] || exit 2
# 네 항목 모두 일반 파일이어야 한다. 허용된 이름을 가진 symlink/hardlink도 거부한다.
tar -tvf "$3/archive.tar" | while IFS= read -r entry; do
  [[ ${entry:0:1} == '-' ]] || exit 2
done
tar -xf "$3/archive.tar" -C "$3" --no-same-owner mysql.sql mongo.archive.gz keyring.tar SHA256SUMS
(cd "$3"; sha256sum -c SHA256SUMS)
[[ -s "$3/mysql.sql" && -s "$3/mongo.archive.gz" ]]
echo 'Backup decrypted and verified; DB import is a separate isolated restore test.'
