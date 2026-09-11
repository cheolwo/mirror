# Simulation 유지보수 도구

장기 실행 서버가 아니라 단일 `Ssalddel` 서버의 Simulation 저장소·공간 산출물을 관리하는 일회성 CLI다.

```powershell
dotnet run --project eng/Ssalddel.Simulation.Tools -- --migrate-simulation-session-database
dotnet run --project eng/Ssalddel.Simulation.Tools -- --migrate-simulation-world-database
```

기존 평창 공간 파생·경관 Graph·WI 공간 연결·업무 규칙·World UI 계획 명령도 같은 프로젝트에서 실행한다. 연결 문자열은 `Ssalddel/appsettings.Local.json` 또는 환경 변수로 제공하며 운영 API 서버를 하나 더 실행하지 않는다.
