#!/usr/bin/env bash
# 로컬 가짜 자료만 사용. DB 접속/외부 업로드 없음. 출력 폴더는 매번 새로 지정.
set -euo pipefail
[[ $# == 1 && ! -e "$1" ]] || exit 2
root=$1
mkdir -p "$root/source"
age-keygen -o "$root/identity.txt" 2> "$root/key-generation.log"
recipient=$(age-keygen -y "$root/identity.txt")
printf 'CREATE TABLE local_fixture(id INT);\n' > "$root/source/mysql.sql"
printf 'local mongo archive fixture\n' | gzip > "$root/source/mongo.archive.gz"
printf 'local keyring fixture\n' > "$root/source/key.xml"
tar -C "$root/source" -cf "$root/source/keyring.tar" key.xml
(cd "$root/source"; sha256sum mysql.sql mongo.archive.gz keyring.tar > SHA256SUMS)
tar -C "$root/source" -cf "$root/archive.tar" mysql.sql mongo.archive.gz keyring.tar SHA256SUMS
age -r "$recipient" -o "$root/valid.tar.age" "$root/archive.tar"
bash deploy/azure-vm/operations-alerts/restore-check.sh "$root/valid.tar.age" "$root/identity.txt" "$root/restore"
cmp "$root/source/mysql.sql" "$root/restore/mysql.sql"
cmp "$root/source/mongo.archive.gz" "$root/restore/mongo.archive.gz"
cmp "$root/source/keyring.tar" "$root/restore/keyring.tar"
printf 'tampered fixture\n' > "$root/source/mysql.sql"
tar -C "$root/source" -cf "$root/tampered.tar" mysql.sql mongo.archive.gz keyring.tar SHA256SUMS
age -r "$recipient" -o "$root/tampered.tar.age" "$root/tampered.tar"
if bash deploy/azure-vm/operations-alerts/restore-check.sh "$root/tampered.tar.age" "$root/identity.txt" "$root/tampered-restore"; then exit 1; fi
printf '{"roundTrip":true,"tamperedChecksumRejected":true,"actualDatabaseImport":false,"externalUpload":false}\n' > "$root/verification.json"
