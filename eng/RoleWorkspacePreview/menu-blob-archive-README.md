# 메뉴 참고 사진의 비공개 Blob 보관 준비

`prepare-menu-blob-archive.py`는 기존 개인 검토 카탈로그와 사진을 읽고 `artifacts/local/orderer-menu-catalog-r55/blob-archive-manifest.json`만 생성한다. 같은 폴더의 `import-proof.json`과 카탈로그 해시를 먼저 대조하고 전체 사진의 SHA-256·바이트 수·실제 이미지 형식·크기와 지점/메뉴/후보 연결을 검증한다. 이미지 바이트·카탈로그·운영 DB·서버 설정은 변경하지 않으며 네트워크 요청·업로드 코드가 없다.

```powershell
python -X utf8 eng/RoleWorkspacePreview/prepare-menu-blob-archive.py
python -X utf8 -m unittest discover -s eng/RoleWorkspacePreview -p test_menu_blob_archive.py
```

`menu-reference/<SHA-256>.<형식별 확장자>`는 같은 파일에 대한 고정 비공개 객체 이름이다. 메뉴별 첫 사진은 `RepresentativeCandidate`, 이후 사진은 `DetailCandidate`로 보존하지만 자료상의 순서일 뿐 운영 대표 사진의 확정이 아니다. 사진 권리·옵션·실제 지점/촬영 당시 판본 미확인과 `publicUseApproved=false`는 유지한다. 관측 단가·항목금액·주문총액을 판매가로 변환하지 않는다.

`AzureBlobStorage:ServiceUri`, `AzureBlobStorage:PrivateContainerName`이라는 기존 설정명을 재사용한다. 현재 확인된 계정은 없으므로 `serviceUri=null`, `containerName=null`이다. `platform-private`는 기존 기본 이름 관측만 기록하며 `liveVerified=false`, `privateAclVerified=false`다. `--service-uri`에 비밀값 없는 HTTPS Blob 계정 원점을 넣어도 관측 후보로만 남고 실제 설정으로 승격되지 않는다. 다른 컨테이너, SAS/쿼리·자격 증명·경로 포함 주소는 거부한다. 명세는 항상 `PreparedBlocked`이며 준비 완료는 업로드 완료가 아니다. 재실행은 동일한 명세를 유지하고 다른 내용의 기존 명세나 실행 증거를 덮어쓰지 않는다.

## 인증 복구 후 기존 SDK로 실행할 계약

후속 서버 실행은 기존 `IObjectStorageService`/`AzureBlobStorageService`를 사용한다. 정확한 기존 계정·컨테이너 접근과 `PublicAccess=None`, 기존 외부 처리 검토·실행 권한을 먼저 확인한다. 어댑터의 `Private` 인자만으로 실제 비공개 ACL이 검증되는 것은 아니다. 현재의 로컬 명세를 Azure 인증·권한·공개 허락으로 해석하지 않는다.

명세/로컬 파일 해시를 재검사하고 같은 `assetId`와 객체 이름으로 `UploadImmutableAsync(..., ObjectStorageAccess.Private)`를 호출한다. 반환 컨테이너·객체 이름을 대조한 뒤 반드시 `DownloadAsync`로 실제 저장 바이트를 읽어 길이와 SHA-256을 확인해야 원격 검증 완료로 기록한다. 기존 객체 충돌(409/412)이나 응답 유실도 같은 객체를 재조회하며 새 이름으로 업로드하지 않는다. 준비 명세를 수정해 성공을 표시하지 않고 별도 비공개 실행 기록에 결과를 남긴다.

미승인 사진은 공개 Blob URL/SAS나 운영 메뉴 DTO에 넣지 않는다. 메뉴 상세에서의 원격 사진 제공은 검증된 `assetId` 허용 목록과 서버 권한 검사·비공개 프록시가 갖춰진 뒤 별도로 연결한다. 현재 공통 UI는 로컬 검토 사진을 계속 사용한다.
