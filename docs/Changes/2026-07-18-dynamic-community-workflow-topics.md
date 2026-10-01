# 업무 계층형 동적 게시판 주제

## 커밋 기록

| 커밋 | 변경 축 | 화면 변경 여부 | 시각 증거 |
| --- | --- | --- | --- |
| `537b3c9` | 관리자가 비활성화한 YouTube 채널을 기본 동기화가 다시 변경하지 않도록 경계 보완 | 화면 없음 | 서비스 회귀 테스트로 비활성 상태·외부 호출·저장 없음 확인 |
| `cb1e856` | 동적 게시판을 창고·주문·판매·운송과 8개 세부 주제로 계층화하고 목록·피드 ViewModel 분리 | 간접 확인 | 새 Razor 화면은 없으며 서버·공통 UI·통합 앱 빌드와 ViewModel 테스트로 조립 경계 확인 |

## 동적 주제 구조

```text
동적 게시판 주제
├─ 창고: 입고, 출고
├─ 주문: 개별주문, 공동주문
├─ 판매: 음식, 화물
└─ 운송: 상차, 하차
```

- 게시글은 원래 게시판을 유지하면서 여러 동적 주제에 동시에 투영될 수 있다.
- 기존 `food`, `cargo` 조회 주소는 각각 `sales-food`, `sales-cargo`로 정규화해 호환한다.
- `CommunityDynamicTopicDirectoryViewModel`은 주제 목록을, `CommunityDynamicTopicFeedViewModel`은 선택한 세부 피드만 담당한다.
- 화물·상하차 정보는 읽기 전용 후보이며 자동 주선·배차·계약을 수행하지 않는다.

## 검증

- 관련 커뮤니티·YouTube 회귀 테스트 45개 통과
- `dotnet build Hongdal/Hongdal.csproj --no-restore`
- `dotnet build HongdalApp/HongdalApp.csproj --no-restore -f net10.0-windows10.0.19041.0`
- `dotnet build HongdalAdminApp/HongdalAdminApp.csproj --no-restore -f net10.0-windows10.0.19041.0`
- `dotnet build Hongdal.FoodApi/Hongdal.FoodApi.csproj --no-restore`
- 전체 테스트 1,332개 중 1,331개 통과. 기존 `ApifyActorGatewayTests` 배열 응답 테스트 1건은 이번 변경과 무관한 알려진 잔여 항목이다.
