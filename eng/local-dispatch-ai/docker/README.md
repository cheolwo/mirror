# Docker Ollama 실행·검증

2026-10-11 사용자의 Docker 설치·이용 및 한 번 실행 요청에 따라 로컬 런타임을 시작했다. 이번 Docker 시작은 실행 도구에서 허용됐으며 이전 Windows portable의 `blocked by policy` 거절 원인까지 해결한 것으로 확대하지 않는다.

## 현재 확인 결과

- 공식 `ollama/ollama:0.40.2` Linux 이미지의 digest는 `sha256:b86366bb528bbf7f1424435d165028497a5b69bf6ddb4fa5a87102e2b79f44fb`다. Git 제외 `.env`에 고정하고 자동 pull·자동 재시작은 끈다.
- 컨테이너는 `mirror-local-dispatch-ai-ollama-1`, 로컬 API는 `http://127.0.0.1:12681`이다. `/api/version`의 `0.40.2`와 `/api/tags`의 `qwen3:1.7b`를 실제 확인했다.
- 기존 모델을 별도 Docker volume `mirror-local-dispatch-ai_ollama-models`에 복사했다. 컨테이너 안 manifest와 config/layer blob 5개의 SHA-256이 검증된 원본과 같다. Windows 원본 캐시는 변경하지 않는다.
- [기존 합성 집계 클라이언트](../dispatch_advisor.py)의 balanced 한 건을 실제 호출했다. 약 12.7초에 `validated_advisory_only`를 반환했으며, 시간당 주문 가정 35건·처리량 3.5건/h에 참고 기사 수 10명으로 계산했다. 이 응답은 수요 예측이나 최적 배차의 증거가 아니다.
- 실제 로그의 계산 장치는 CPU다. GPU 장치 예약은 별도 `compose.gpu.yaml`이며 이번에 활성화하거나 검증하지 않았다. MCP 도구 등록 없이 Codex가 기존 클라이언트를 호출했다.
- 실행 근거·초기 실패·실제 응답은 Git 제외 `artifacts/local/local-dispatch-ai/docker-runtime-20261011/`에 보관한다. 이전 다운로드·정적 검증 기록 `docker-preparation-20261011/`은 당시 상태로 보존한다.

## 실행 중 발견한 문제와 수정

첫 시작은 읽기 전용 `/models`에서 디렉터리를 초기화하려다 종료됐다. 원본을 쓰기 가능으로 열지 않고 별도 Docker 모델 volume에 검증된 캐시를 복사해 수정했다. 새 모델 다운로드는 없다.

내부 전용 네트워크에서는 Ollama 자체가 실행됐지만 이 PC에서 게시 API에 연결되지 않았다. 별도 bridge 네트워크로 바꾼 뒤 실제 호스트 연결을 확인했다. 공개 binding은 `127.0.0.1:12681`이며 클라우드 기능은 끈다. 현재 bridge 자체가 외부 통신 차단 장치는 아니므로 인터넷 차단을 검증했다고 주장하지 않는다.

## 연결·권한·운영 경계

Codex → 합성 집계 클라이언트 → 로컬 Docker Ollama API → JSON·계산·모델명·`advisoryOnly` 검사의 순서다. 클라이언트 오류 시 규칙 결과를 유지한다. 실제 고객·기사·주문 원장을 읽지 않으며 이번 실제 호출에서도 `operationalApiCalls=0`, `stateChanges=0`이다. 서버의 최종 배차 권한과 기본 OFF 운영 설정은 유지한다.

CPU 4개·RAM 6GiB·동시 요청 1개·로드 모델 1개 제한, `keep_alive=0`, 제한된 로그 용량을 사용한다. `local-ai` profile은 기본 Compose 실행에 포함되지 않지만 명시적 서비스 지정으로 활성화될 수 있다. 현재 컨테이너는 사용자가 이용할 수 있도록 실행 상태로 남긴다. 자동 재시작이 꺼져 있어 Docker/PC 재시작 뒤 자동 복구를 보장하지 않는다.

[Windows 래퍼](../run-local.ps1)의 Execute는 portable 서버를 직접 시작하므로 실행 중인 Docker 서버 재사용 진입점으로 사용하지 않는다. 별도 런타임이나 중복 서버를 띄우지 않고 기존 Python 클라이언트에서 명시적 루프백 API를 호출한다.

## Azure 후속 범위

Linux 이미지와 API 경계는 이후 배포 기반으로 재사용할 수 있지만 이 파일은 로컬 시험 설정이다. Azure의 사설 접근·인증·지속 모델 저장소·용량·비용과 GPU는 별도 검증이 필요하다. 현재 클라이언트의 외부 URL 거절은 유지하며 Azure 배포·운영 자동 배차·실제 알림을 활성화하지 않았다.

## 공식 근거

- [Ollama Docker](https://docs.ollama.com/docker)
- [Ollama 모델 경로·클라우드 비활성화·네트워크 설정](https://docs.ollama.com/faq)
- [Docker profiles](https://docs.docker.com/compose/how-tos/profiles/)
- [Docker Compose GPU 장치 예약](https://docs.docker.com/compose/how-tos/gpu-support/)
- [Codex MCP 연결](https://learn.chatgpt.com/docs/extend/mcp)
