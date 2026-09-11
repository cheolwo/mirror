# Unity 턴 카드 실제 Simulation 서버 연결

## 변경 결과

실제 Unity `SimulationWorldShell`의 턴 마감 권위를 로컬 Fixture에서 당시 `Ssalddel.Simulation.Server` HTTP 경계로 교체했다. 현재 이 경로는 단일 `Ssalddel` 호스트의 `Ssalddel.Simulation.Hosting` 모듈로 이동했으며 route 계약은 유지한다. Scene 조립부에서 서버 모드와 Fixture 모드를 명시적으로 구분하며, 서버 모드는 다음 순서로 동작한다.

1. Development Simulation session 확보
2. 현재 턴 context 조회
3. 카드 선택 턴 마감 Preview
4. 사용자의 명시적 Confirm
5. canonical session 재조회와 revision·Tick·다음 턴 검증
6. 검증된 WorldShell snapshot 적용

서버 호출이나 검증이 실패해도 Fixture로 조용히 대체하지 않는다.

## 직접 확인한 화면

실제 `localhost:5104` Development Simulation 서버와 Unity Play Mode를 연결했다. 서울 생활문화 질문 카드를 선택해 턴을 마감한 뒤 화면이 2026-04-12·Tick 0·Revision 0에서 2026-04-13·Tick 1·Revision 1로 바뀌고, 다음 턴 효과 `LocalContextAwareness`와 문화 출처 계보를 표시하는 것을 확인했다.

대표 Game View PNG는 Unity 프로젝트의 `Assets/Documentation/Changes/2026-08-11-turn-card-http-1/server-culture-next-day.png`에 보존했다. 골든 패스 구간의 새 Console 오류는 0건이다.

## 검증과 제한

- 집중 Unity EditMode: 5/5 통과
- 전체 Unity EditMode: 219/220 통과. 실패 1건은 기존 연구 Scene 기대 개수 27과 현재 28의 불일치
- 실제 HTTP 서버 health: 정상
- 실제 Unity Play Mode canonical 재조회: Tick 1, Revision 1 확인
- 운영 인증·영속 DB session·서버 재시작 복구: 미구현
- 실제 행사 publication·운영 상태 변경: 수행하지 않음
- commit·push·배포: 수행하지 않음
