# 화물 정보 공고 격리 검증

제품의 `화물정보공고Controller → 화물정보공고UseCase → Mongo화물정보공고Store`를 실제 루프백 HTTP와 별도 MongoDB로 검증한다. 운영 Startup, 실제 회원 인증, 운송·배차·금융 서비스는 등록하지 않는다. 인증 주체와 입력은 시험 전용 합성 자료다.

## 실행 경계

- 새 임시 Mongo 컨테이너의 `127.0.0.1` 전용 포트를 사용한다. 기존 27017/27021 포트와 원격 URI는 거절한다.
- 매 실행마다 `codex_cargo_info_r2_`로 시작하는 새 GUID 데이터베이스와, `artifacts/local/cargo-info-publication-r2/` 아래 아직 없는 결과 폴더를 지정한다.
- 실제 제품 공고 옵션은 바꾸지 않고 이 호스트에서만 옵션을 주입한다. 일반 공개 공고의 기본값은 OFF다.
- 입력·동의·철회·개정 충돌·원 요청 재조회·서버 재시작·정확한 만료 경계를 검사한다. 최종 컬렉션 목록을 확인하며 실제 운송이나 결제를 실행하지 않는다.

PowerShell에서 저장소 루트 기준으로 실행한다. `<port>`는 새 컨테이너의 동적 포트를 확인해서 바꾼다.

```powershell
$runKey = [guid]::NewGuid().ToString('N')
dotnet run --project eng/CargoInformationPublicationProbe -- --output "$PWD/artifacts/local/cargo-info-publication-r2/http-$runKey" --mongo 'mongodb://127.0.0.1:<port>' --database "codex_cargo_info_r2_$runKey"
```

`result.json`은 해당 실행의 성공 근거이며 실패 시 `failed.json`을 남긴다. 최초 실패를 지우거나 기존 결과 폴더에 덮어쓰지 않는다. 테스트 서버를 재시작하는 것은 실제 운영 서버의 재시작·백업 복구 검증과 다르다. 사용 후에는 이 작업에서 생성한 컨테이너만 중지한다.

이 도구는 APK 설치·실제 휴대폰·외부 회원 인증·기관 허가·운영 활성화·실제 회원 데이터 이전을 검증하지 않는다. 400/409 뒤 결과가 확인되지 않은 요청을 해제하는 관리자 절차도 이번 범위에 포함하지 않는다.
