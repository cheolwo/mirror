[CmdletBinding()]
param(
    [switch] $EnableLocalAccount,
    [string] $Sql = 'SELECT DATABASE(), CURRENT_USER();'
)
$ErrorActionPreference = 'Stop'
# 현재 프로젝트의 loopback 개발 DB만 허용한다. 비밀값은 컨테이너 밖으로 꺼내지 않는다.
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$info = @(& docker inspect hongdal-mysql-1 | ConvertFrom-Json)[0]
if ($LASTEXITCODE -or $info.Config.Labels.'com.docker.compose.project' -ne 'hongdal' -or
    [IO.Path]::GetFullPath($info.Config.Labels.'com.docker.compose.project.working_dir') -ne $repo -or
    $info.Config.Labels.'com.docker.compose.service' -ne 'mysql') { throw 'Local DB ownership mismatch.' }
$ports = @($info.HostConfig.PortBindings.'3306/tcp')
if ($ports.Count -ne 1 -or $ports[0].HostIp -ne '127.0.0.1' -or $ports[0].HostPort -ne '13306') {
    throw 'Only loopback port 13306 is permitted.'
}
if ($EnableLocalAccount) {
    # 기존 root/앱 계정과 인증을 유지하고 새 localhost 전용 계정만 추가한다.
    # 계정이 이미 있으면 실패하므로 임의 비밀번호 초기화/권한 덮어쓰기가 없다.
    $setup = @'
CREATE USER 'local_developer'@'localhost' IDENTIFIED BY '';
GRANT ALL PRIVILEGES ON hongdal_dev.* TO 'local_developer'@'localhost';
'@
    $setup | & docker exec -i hongdal-mysql-1 sh -c 'export MYSQL_PWD="$MYSQL_ROOT_PASSWORD"; exec mysql -uroot --protocol=SOCKET --batch'
    if ($LASTEXITCODE) { throw 'Local account setup failed; existing accounts were not reset.' }
}
$Sql | & docker exec -i hongdal-mysql-1 env -u MYSQL_PWD mysql --protocol=SOCKET -ulocal_developer hongdal_dev --default-character-set=utf8mb4 --batch --raw
if ($LASTEXITCODE) { throw 'Local SQL failed. No credential or database fallback.' }
