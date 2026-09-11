# Ssalddel Simulation Hosting 모듈

`Ssalddel.Simulation.Hosting`은 별도 실행 서버가 아니라 장기 실행 호스트
`Ssalddel` 안에 조립되는 ASP.NET Core 모듈이다. 기존
`/api/simulation/v1/*`와 Simulation SignalR Hub 경로는 유지하지만 주소,
JWT 발급·검증, 상태 확인과 컨테이너는 `Ssalddel` 하나를 사용한다.

## 책임 경계

- `Ssalddel` 호스트: 로그인, JWT, HTTP pipeline, 상태 확인과 배포 단위
- `Ssalddel.Simulation.Hosting`: Simulation Controller·Hub·필터·DI 조립
- `Ssalddel.Simulation.Application/Domain`: 세션 업무 흐름과 가상 세계 권위
- `Ssalddel.Simulation.Persistence`: Simulation 전용 저장·파생 DB와 공유
  공공데이터의 읽기 전용 연결
- `eng/Ssalddel.Simulation.Tools`: migration과 공간 파생용 단발성 CLI

물리 호스트를 합쳐도 상태 권위는 합치지 않는다. 실제 주문·계약·결제·재고는
운영 원장이, scenario·seed·가상 시간·save/replay는 Simulation 원장이 각각
소유한다. Unity는 이 상태의 관찰·입력 표현이며 업무 완료를 확정하지 않는다.

모든 Simulation HTTP 요청은 기본적으로 `Ssalddel`의 로그인 JWT가 필요하다.
개인 세션은 로그인 주체와 세션 고유 식별자의 접근 원장을 확인하며, 다른
사용자의 세션은 존재 여부를 노출하지 않도록 `404`로 처리한다. 온라인 세계는
공유 방 멤버십 규칙이 별도로 접근을 판정한다.

## 설정과 DB

다음 기능은 기본 비활성이며 `Ssalddel` 설정에서 명시적으로 켠다.

- `SimulationSharedPublicData`: 운영 서버가 수집한 공공데이터를 읽기 전용 조회
- `SimulationWorldDerivationDatabase`: 공간·규칙·표현 파생 원장
- `SimulationSessionDatabase`: save/replay와 세션 접근 원장

`SimulationWorldDerived`와 `SimulationSession`은 운영 `SsalddelContext`와 분리된
DB 권위를 유지한다. migration은 호스트 시작 때 자동 실행하지 않는다.

```powershell
dotnet run --project eng/Ssalddel.Simulation.Tools -- `
  --migrate-simulation-world-database

dotnet run --project eng/Ssalddel.Simulation.Tools -- `
  --migrate-simulation-session-database
```

`docker-compose.simulation.yml`은 독립 API 컨테이너를 만들지 않는다. 기본
`docker-compose.yml`의 `app`에 Simulation DB 설정을 보태고 DB 계정 초기화만
수행한다.

```powershell
docker compose `
  -f docker-compose.yml `
  -f docker-compose.simulation.yml `
  --profile simulation `
  up -d --build mysql mongo simulation-db-init app
```

공간 파생·대장 생성 명령도 같은 CLI 프로젝트에 있으며 `--help`로 현재 명령을
확인한다. CLI 결과는 운영 상태를 자동 변경하거나 Unity Scene 배치를 입증하지
않는다.
