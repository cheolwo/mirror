# 30개 행정동 정밀 지형 consumer 준비도 감사

[구현·감사 기록 · 월드·공간·배치 · PLAN-SYSTEM-ADMIN-DONG-DIORAMA · r33]

- 상태: `PrecisionTerrainBlockedBySourceMetadataAndConsumerContract`
- 기준: [지형 공통 격자 비공개 검토 세대 r29](thirty-admin-dong-terrain-private-generation.implementation.r29.md), [등고선·표고 원본 감사 r28](thirty-admin-dong-terrain-contour-audit.implementation.r28.md)
- 공식 자료: [서울 열린데이터광장 OA-22241](https://data.seoul.go.kr/dataList/OA-22241/F/1/datasetView.do), [국가법령정보센터 수치지형도 지형지물 속성목록 별표 2](https://www.law.go.kr/LSW/flDownload.do?bylClsCd=200201&flNm=%5B%EB%B3%84%ED%91%9C+2%5D+%EC%88%98%EC%B9%98%EC%A7%80%ED%98%95%EB%8F%84+%EC%A7%80%ED%98%95%EC%A7%80%EB%AC%BC+%EC%86%8D%EC%84%B1%EB%AA%A9%EB%A1%9D%28%EA%B5%AC%EC%A1%B0%ED%99%94%29&flSeq=145877779)
- 대상: `scope:administrative-dong-diorama:northeast-seoul-rider:r2`의 30개 역사 행정동과 r29 비공개 지형 세대

## 결론

r29의 500m 초과 최근접 원천 표본 47개를 다시 공간 분해한 결과, 모두 물리 tile `x5/z6`의 하나의 8-neighbor 연결 성분이며 `region:kr:hjd:1126068000` 신내1동의 중앙 core 밖 60m halo에만 있다. 중앙 500m core 안은 0개이고, 30개 역사 행정동 polygon 안도 0개이며, 두 물리 tile이 공유하는 key도 0개다. 문제 범위가 국소적이라는 사실은 확인했지만 현재 60m halo를 임의로 줄이거나 47개를 삭제해 `precisionTerrainReady=true`로 바꾸지 않는다.

정밀 지형 적용은 두 관문에서 계속 차단한다. 첫째, `OA-22241`과 일반 수치지형도 속성목록은 수평 CRS와 일반 `F001/F002` 수치 필드 형태를 설명하지만, 이번 ZIP의 정확한 `N3L_F001/N3P_F002` 높이 필드에 대한 물리 단위·수직 datum·기준면 선언을 제공하지 않는다. 둘째, r29 bundle을 읽어 공통 수직 기준과 halo 정책을 지키는 행정동 Unity consumer가 없다. 기존 범용 L2 Mesh Builder는 halo를 중앙 core로 잘라 표현하지만 r29 세대를 읽지 않으며 tile별 최저값을 따로 빼므로 r29의 공통 수직 기준 계약과도 맞지 않는다.

따라서 r29는 구조·재현성 검토를 위한 비공개 입력으로만 보존한다. Unity 높이 Mesh, Collider, 건물·도로 Y 배치, 공개 화면, Runtime, Traversal, Gameplay에는 적용하지 않는다.

## 500m 초과 표본의 공간 국소화

r29의 공통 격자에서 최근접 공식 원천까지의 거리가 500m를 넘는 47개 key를 8-neighbor로 묶고, 물리 tile core·행정동 mask·공유 key에 각각 대조했다.

| 감사 항목 | 결과 |
| --- | --- |
| 500m 초과 key | 47 |
| 8-neighbor 연결 성분 | 1 |
| 물리 tile | `x5/z6` 하나 |
| 행정동 귀속 | 신내1동 외곽 60m halo |
| 중앙 500m core 안 | 0 |
| 30개 역사 행정동 polygon 안 | 0 |
| 물리 tile 사이 shared key | 0 |
| 최대 최근접 원천 거리 | `519.509899m` |

동일 tile의 중앙 core에서 바깥으로 검토 범위를 10m씩 넓혀 최대 거리와 500m 초과 수를 다시 계산했다.

| 검토 범위 | 최대 최근접 원천 거리 | 500m 초과 key |
| --- | ---: | ---: |
| core + 10m | `473.907807m` | 0 |
| core + 20m | `483.450266m` | 0 |
| core + 30m | `493.010862m` | 0 |
| core + 40m | `502.290490m` | 1 |
| core + 50m | `510.874873m` | 13 |
| core + 60m | `519.509899m` | 47 |

등고선 constraint 파생 간격을 40m에서 5m로 조밀하게 만든 대조에서도 이 47개 집합은 줄지 않았다. 따라서 현상은 단순한 선상 샘플 간격의 산물이 아니라 신내1동 북동 외곽 halo와 공식 원천 coverage 사이의 거리 문제로 본다. `core+30m`가 현재 입력에서 500m 이내라는 결과는 **후보 정책 비교값**일 뿐, 승인된 halo 축소나 정밀도 증거가 아니다. halo는 seam·법선·경계 표현을 함께 정하는 consumer 계약이므로 수치 하나만 바꿔 해결하지 않는다.

## 공식 메타데이터가 닫지 못한 높이 의미

`OA-22241`은 경사도를 추출할 수 있는 표고점·등고선 SHP를 제공하며 2025-03-20 `서울시 등고선.zip`을 게시한다. ZIP의 PRJ는 `EPSG:5174`와 수평 단위 metre를 확인해 주므로 평면 좌표 변환에는 사용할 수 있다.

국가법령정보센터의 일반 속성목록은 `F001` 등고선의 등고수치와 `F002` 표고점의 수치를 `DOUBLE`, 표시 형태 `000.0`으로 제시한다. 그러나 이는 일반 지형지물 사양이다. 이번 ZIP의 실제 `N3L_F001` DBF `CONT/HEIGHT`와 `N3P_F002` DBF `NUME/HEIGHT`가 어느 물리 단위와 수직 datum·기준면을 쓰는지 직접 선언하지 않는다. 수평 CRS의 metre나 숫자 표시 형식을 높이 단위의 공식 선언으로 전용하지 않는다.

확인되지 않은 상태는 다음과 같이 유지한다.

| 항목 | 현재 판정 |
| --- | --- |
| 수평 CRS·단위 | `EPSG:5174`, metre 확인 |
| 정확한 높이 필드 단위 | `heightUnitSourceDeclared=false` |
| 수직 datum·기준면 | `verticalDatumVerified=false` |
| 절대 표고 권위 | `absoluteElevationAuthorized=false` |
| r29 출력 의미 | `SourceNumericHeightOnly` |

## Unity consumer 감사

Hongdal과 Unity 저장소에서 r29 generation ID·경로·`precisionTerrainReady` 또는 행정동 지형 bundle을 읽는 consumer를 찾지 못했다. 현재 행정동 디오라마의 Unity 검토 경로는 경계·건물·도로·보행망·방향표시·비오톱 표현까지이며 r29 높이 binary를 역직렬화하지 않는다.

별도 Unity 저장소의 `Assets/Ssalddel/Presentation/World/공간PhysicalElevationMeshBuilder.cs`는 범용 L2 `height-f32-v1` payload에서 60m halo를 잘라 중앙 500m core Mesh를 만드는 기존 코드다. 이 코드는 다음 이유로 r29 consumer가 아니다.

1. r29의 generation·manifest·audit·행정동 mask와 hash를 읽거나 검증하지 않는다.
2. 각 tile의 중앙 core 안 최저 높이를 따로 빼서 Y=0으로 만든다. r29의 세대 공통 수직 기준을 보존하지 못하므로 인접 tile과 건물·도로 Y 결속에 쓸 수 없다.
3. halo 표본을 seam·법선 계산에 쓰지 않고 core를 바로 자른다. 60m 전체가 필요한지, 30m까지로 제한할지, 외곽 결손을 어떻게 차단할지 정한 계약이 없다.
4. r29의 no-data, 500m 초과 거리, shared-key bit equality, 역사 행정동 mask를 적용 관문으로 검사하지 않는다.

현재 코드가 결과적으로 문제의 47개를 화면 core에서 제외한다는 사실은 r29가 Unity에 안전하게 적용됐다는 증거가 아니다. consumer가 없으므로 실제 Mesh 정점·법선·tile seam·건물 바닥 결속에 대한 Unity 증거도 없다.

## 다음 적용 revision의 필수 계약

정밀 지형을 다음 revision에서 Unity까지 가져가려면 아래 항목을 하나의 versioned consumer 계약과 검증기에 고정한다.

1. **원천 CRS·단위·datum 결속**

   `OA-22241` ZIP hash, `EPSG:5174`, 정확한 SHP/DBF layer·field 이름을 고정하고, 생산기관 또는 공식 메타데이터가 해당 `HEIGHT`의 물리 단위·수직 datum·기준면을 직접 선언한 근거를 receipt에 넣는다. 일반 `F001/F002` 사양이나 수평 단위로 대신하지 않는다.

2. **공통 높이 기준**

   절대 표고 사용 가능 여부와 `commonVerticalReferenceSourceValue`를 명시한다. 모든 tile·행정동·건물·도로가 같은 기준을 쓰고, tile별 최소값 차감은 금지한다. 시각적 수직 과장은 원본 높이와 분리된 표현 매개변수로만 둔다.

3. **core·halo·clipping 정책**

   Mesh core, seam/법선 계산 halo, 원천 검색 반경, 행정동 mask의 역할을 각각 정의한다. `core+30m`·`core+40m`·`core+60m` 대조를 근거로 수용 반경을 먼저 정하고 새 세대를 재생성한다. 47개를 사후 삭제하거나 값을 늘려 통과시키지 않는다.

4. **no-data와 외삽 실패**

   최대 원천 거리 초과, NaN·무한값, 제약 부재, scope 밖 key는 명시적인 no-data 또는 generation 실패로 남긴다. 0 높이·평지·최근접 장거리 값으로 채우지 않는다. consumer도 no-data가 있는 Mesh나 Collider 생성을 거부한다.

5. **sample-to-mesh 보간**

   `height-f32-v1`의 endian·표본 간격·행열 방향을 검증하고, halo를 포함해 경계 정점 높이와 법선을 결정한 뒤 중앙 core를 출력한다. 인접 core 정점의 Y bit 또는 허용 오차, 경계 법선 각도, triangle winding, vertex/index 상한을 고정한다.

6. **감사 counter**

   core·halo별 최대 최근접 원천 거리와 기준 초과 수, 연결 성분 수, tile·행정동·shared-key 귀속, no-data 수, shared height bit 불일치, Mesh 경계 높이·법선 불일치, 공통 수직 기준 불일치를 manifest와 Unity capture 증거에 기록한다.

7. **읽기 전용·비공개 권위**

   첫 consumer는 고정 hash의 private bundle만 읽고 DB·Mongo·current pointer를 쓰지 않는다. `publicDisplayAllowed=false`, `distributionApproved=false`, `runtimeAuthorized=false`, `colliderAllowed=false`, `traversalReady=false`, `gameplayReady=false`를 fail-closed로 검증한다. 실제 Play Mode·Game View 검토가 끝나도 이 권위들은 별도 승인·새 revision 없이는 바꾸지 않는다.

## 적용 해제 조건과 현재 경계

`precisionTerrainReady`를 올리려면 공식 높이 단위·수직 datum 근거, 새 halo·검색반경 정책, 공통 높이 기준을 지키는 r29 후속 consumer, core·seam·법선 감사가 모두 같은 immutable generation과 Unity 증거에 결속돼야 한다. 신내1동 외곽 47개가 중앙 core 밖이라는 이유 하나만으로 상태를 올리지 않는다.

이번 r33은 기존 r29 세대와 Unity 코드를 변경하지 않는 준비도 감사다. 새 지형 generation, Unity height Mesh·Collider, Scene 저장, DB·Mongo 쓰기, current pointer 변경, 공개·배포, Runtime·Traversal·Gameplay 적용은 모두 시도하지 않았다. 상태는 `PrecisionTerrainBlockedBySourceMetadataAndConsumerContract`로 유지한다. **새 디오라마 규칙 후보 없음**.
