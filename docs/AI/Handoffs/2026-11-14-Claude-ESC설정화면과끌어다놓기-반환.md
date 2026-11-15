# 2026-11-14 Claude ESC 설정 화면과 끌어다 놓기 반환

- **작업실 역할.** 개발 작업실(Claude)의 반환이다. PLANNING · r 문서는 바꾸지 않았다. [음악 파일에 맞춘 빛](2026-11-14-Claude-음악파일에맞춘빛-반환.md)의 뒤를 잇는다.
- **사용자의 말(2026-11-14 21시경).** 「음악은 ESC 누르면 설정에 들어가고, 그 설정 UI 화면에 음악이 있고, 거기에 wav나 mp3 파일을 넣으면 분석이 되어서 박자 흐름에 맞춰 빛이 움직이는 식으로 하면 좋겠다.」 → Claude가 두 가지를 물음 → 「가운데 큰 설정 화면」, 「파일 창 + 끌어다 놓기」.
- **쓰기 경로.** `ssalddel/Assets\Ssalddel\WindowsGame\Runtime\`. Kernel은 건드리지 않았다.
- **그림.** `artifacts/local/hongdal-unity-claude-20261112/captures/방향촬영-20261114/설정화면-ESC-음악탭과-지도탭.jpg`. 내부 검토용 · 배포 아님.

## 1. 무엇이 달라졌나

**ESC → 설정 화면**

- ESC를 누르면 지도 가운데에 큰 설정 화면이 뜨고, 한 번 더 누르면(또는 ×) 닫힌다. 누르고 있는 동안 되풀이되는 키는 한 번으로 본다. 오른쪽 위 「설정」 단추도 같은 화면을 연다.
- 열려 있는 동안 뒤가 어두워지고, 아래의 지도 · 단추 · 결정 순회의 Enter는 눌리지 않는다. 고른 것과 열린 칸(카드, 동 관리 창 등)은 그대로 두고 그 위에 뜬다. 닫으면 그대로 이어진다. 전에는 설정 칸을 열면 고른 것을 내려놓았다.
- 「지도만 보기」 · 「기존 화면」에서도 ESC로 열린다(전에는 「설정」 단추가 보이는 화면에서만 열 수 있었다).
- 왼쪽 탭 셋:
  - **음악 · 소리**: 곡 넣기, 지금의 곡과 분석 결과, 재생 · 멈춤, 소리 크기, 지금의 박 · 저음, 신호 소리.
  - **화면 · 지도**: 지도 효과, 지도 모양, 지도 방향, 동 보기, 지도 도구, 화면(전의 설정 칸에 있던 것 그대로).
  - **회차**: 처음부터 새로 시작(두 번 눌러야 한다), 개발 정보.
- 오른쪽에 있던 설정 칸은 없어졌다. 「오늘 결산」 칸은 전의 자리 · 크기 그대로다(설정 화면을 열면 전처럼 닫힌다).

**음악 탭**

- **곡 넣기 두 길.** ① 탐색기에서 mp3 · wav 파일을 게임 창 위로 끌어다 놓는다. 설정 화면이 닫혀 있어도 받고, 받으면 설정 화면의 음악 탭이 열려 분석 결과를 보인다. 여러 파일을 놓으면 첫 mp3 · wav를 쓴다. 다른 파일이면 「mp3 · wav 파일만 넣을 수 있습니다」. ② 「곡 넣기」를 누르면 Windows 파일 창(전과 같다).
- **분석 결과.** 곡 이름 아래에 「곡을 읽는 중」 → 「박을 찾는 중」 → 「120 BPM · 박에 맞춤 · 재생 중」 또는 「박이 흐려 세기에 맞춤」.
- **소리 크기.** 막대를 누르거나 끌어서 0~100%(처음 70%). 이 PC에 기억한다.
- **지금의 박 · 저음.** 네 점이 곡의 박마다 차례로 금빛으로 커지고, 저음 막대가 저음의 세기를 보인다. 분석이 곡과 맞는지 눈으로 볼 수 있다.
- 빛이 박에 맞춰 퍼지는 방식은 앞 반환과 같다.

## 2. 바뀐 파일

- `Runtime/SagajeongDispatchSettings.cs`: 설정 화면(`DrawSettingsScreen`, 탭 셋, 소리 크기 막대), ESC(`HandleSettingsKey`). 오른쪽 설정 칸(`DrawSettingsPanel`)을 걷었다.
- 새 파일 `Runtime/SagajeongDispatchMusicDrop.cs`(`음악끌어놓기`): Windows 실행판에서만, 게임 창(UnityWndClass)에 끌어다 놓기를 받겠다고 알리고 메시지 길목(WH_GETMESSAGE)에서 WM_DROPFILES만 집는다. 다른 메시지는 건드리지 않는다. 게임이 끝날 때 걷는다. 편집기에서는 꺼 둔다.
- `Runtime/SagajeongDispatchMusic.cs`: 끌어다 놓은 파일 받기, 소리 크기와 기억(`hongdal.music.volume`), 상태 줄(`MusicState`), 방향 촬영의 ESC · 끌어다 놓기 시험.
- `Runtime/SagajeongDispatchView.cs`: OnGUI의 처음에 ESC, 끝에 설정 화면. 설정 화면이 떠 있는 동안 아래 단추를 끈다.
- `Runtime/SagajeongDispatchDong.cs`: 설정 화면이 떠 있으면 지도가 눌리지 않는다(`MapOverlayCovers`).
- `Runtime/SagajeongDispatchCycle.cs`: 설정 화면이 떠 있으면 Enter로 결정 순회를 누르지 않는다.
- `Runtime/SagajeongDispatchExpansion.cs`: 오른쪽 설정 칸을 그리던 자리와 가리던 범위를 걷었다.
- `Runtime/SagajeongDispatchGuLabels.cs`: 앞 반환에서 더한 줄을 걷었다(음악 전의 파일과 같다).
- `Runtime/SagajeongDispatchViewProof.cs`: 설정 장면 셋(07 · 20 · 27)은 「화면 · 지도」 탭으로 찍는다. ESC · 끌어다 놓기의 받은 값과 통과 조건.

| 파일 | SHA-256 앞 16자리 |
| --- | --- |
| `Runtime/SagajeongDispatchSettings.cs` | `f12b52b97cf92069` |
| `Runtime/SagajeongDispatchMusicDrop.cs` (새 파일) | `4a589689106eb14f` |
| `Runtime/SagajeongDispatchMusic.cs` | `1c05e319389d697f` |
| `Runtime/SagajeongDispatchView.cs` | `9609609a8ac639f1` |
| `Runtime/SagajeongDispatchDong.cs` | `e0fde8df594c52b4` |
| `Runtime/SagajeongDispatchCycle.cs` | `57092219c53a9674` |
| `Runtime/SagajeongDispatchExpansion.cs` | `ddb4250c76b42014` |
| `Runtime/SagajeongDispatchGuLabels.cs` | `30e9be3ede432d6f` |
| `Runtime/SagajeongDispatchViewProof.cs` | `4b0a3c4a8b3a5a87` |

- 바꾸기 전 파일은 `artifacts/local/hongdal-windows-build-20261112/auto-backup/20261114-esc/`에 있다.

## 3. 확인한 것

- **확인용 빌드 통과(2026-11-14 21:02).** 한 번에 통과. 오류 0, 경고 0. 검사 56 · 15 · 19 · 35 · 59 · 29 · 32 · 22 · 19 · 29 · 14.
- **방향 촬영 통과(91장면).** 앞의 음악 값은 그대로(wav 120.01 · mp3 120.04 BPM). 새로 본 것:

| 촬영의 값 | |
| --- | --- |
| 게임 창을 찾아 끌어다 놓기를 받을 준비 | 됨 |
| 게임 창에 ESC 키 메시지를 보내 설정 화면이 열림 · 한 번 더 보내 닫힘 | 열림 · 닫힘 |
| 게임 창에 끌어다 놓기 메시지(WM_DROPFILES)로 시험 wav를 보냄 | 들어가 분석됨(120 BPM), 설정 화면의 음악 탭이 열림 |
| 전후의 커널 시각 · 원장 줄 수 · 기억한 곡 | 같음 |

- 촬영은 탐색기에서 끌어다 놓을 때와 같은 메시지를 게임 창에 보낸다. ESC도 키보드가 창에 보내는 메시지를 그대로 보낸다. 게임 안의 길은 실제 입력과 같다.
- **하지 않은 확인.** 사람이 탐색기에서 실제로 끌어다 놓은 것, 키보드로 ESC를 누른 것, 마우스로 탭 · 소리 크기 막대를 누른 것, 소리를 귀로 들은 것, 실제 노래. 사용자가 띄워 둔 실행판은 전의 판이다. 게임을 관리자 권한으로 실행하면 Windows가 보통의 탐색기에서 끌어다 놓는 것을 막는다(그때는 「곡 넣기」를 쓴다).

## 4. 다음 · 정할 것

- 앞 반환 6절과 같다: 퍼짐의 세기, 「몇 박마다」, 박 보정 · 반 박 옮기기, 곡 목록, 루프백(r211).
- ~~설정 화면이 열려 있는 동안에도 게임 시간은 흐른다.~~ → 5절에서 멈추게 했다.
- 예약은 멈춘 그대로다. git 커밋 · 푸시는 하지 않았다.

## 5. 덧붙임 — 설정 화면을 여는 동안 시간 멈춤 (21:20)

- **사용자의 말.** 「시간이 멈추게」(4절의 물음에 대한 답).
- **무엇이 달라졌나.** 설정 화면이 떠 있는 동안 게임 시간이 멈추고, 닫으면 전의 속도(1배 · 2배 · 4배)로 이어진다. 위의 진행 속도 단추는 그동안 「정지」로 보인다. 설정 화면 제목 옆에 「게임 시간 멈춤 · 닫으면 1배로 이어집니다」가 뜬다.
- 이미 「정지」였거나 전국 지도가 시계를 멈춰 두었으면 손대지 않는다(닫아도 정지 그대로). 운영이 돌고 있을 때만이다. 설정 화면에서 새 회차를 열면 새 회차의 속도를 따른다.
- 길은 전국 지도의 시계 정지(`HoldNationClock`)와 같은 `SetCampaignSpeed`다. 시간 · 돈 · 주문 규칙은 그대로이고, 멈춘 동안 기사 빛도 서 있다. 음악과 빛의 숨(박에 맞춰 퍼짐)은 그대로 흐른다(실제 시간).
- **바뀐 파일.** `Runtime/SagajeongDispatchSettings.cs`(`SyncSettingsClock`, 제목 옆 알림), `Runtime/SagajeongDispatchView.cs`(OnGUI의 처음, ESC 다음에 부른다), `Runtime/SagajeongDispatchMusic.cs`(촬영의 확인), `Runtime/SagajeongDispatchViewProof.cs`(받은 값과 통과 조건). 바꾸기 전 파일은 `auto-backup/20261114-pause/`.

| 파일 | SHA-256 앞 16자리 |
| --- | --- |
| `Runtime/SagajeongDispatchSettings.cs` | `ea69cb05e8efab96` |
| `Runtime/SagajeongDispatchView.cs` | `969af3a4278d19d5` |
| `Runtime/SagajeongDispatchMusic.cs` | `ba504c315567bcd1` |
| `Runtime/SagajeongDispatchViewProof.cs` | `2fbe22fb341e93d0` |

- **확인.** 확인용 빌드 통과(21:18, 한 번에. 오류 0, 경고 0, 검사 56 · 15 · 19 · 35 · 59 · 29 · 32 · 22 · 19 · 29 · 14). 방향 촬영 통과(91장면): 열기 전 1배 → 떠 있는 동안 0(정지) → 닫은 뒤 1배. 촬영 사진에 제목 옆 알림과 위의 「정지」가 보인다. ESC · 끌어다 놓기 · 음악 값은 그대로 통과. 사람이 직접 눌러 본 것은 아니다.
