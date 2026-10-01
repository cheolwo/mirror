# 기사 API 기능 ViewModel 조립 방식

각 PageViewModel은 화면에 필요한 기능 ViewModel만 생성자 주입으로 받아 조립한다.
모든 기사 기능이 필요한 화면은 `기사Api기능모음ViewModel`을 사용할 수 있다.

```csharp
public sealed class 예약PageViewModel : 조립ViewModelBase
{
    public 예약PageViewModel(기사예약기능ViewModel 예약)
    {
        this.예약 = 하위ViewModel등록(예약);
    }

    public 기사예약기능ViewModel 예약 { get; }

    public Task InitializeAsync()
        => 예약.목록조회.실행Async();
}
```

`Api작업ViewModel<TResult>`는 매개변수 없는 API를, `Api작업ViewModel<TParameter, TResult>`는
상세 조회나 명령처럼 요청 값이 있는 API를 표현한다. 두 형식 모두 다음 상태를 공통 제공한다.

- `상태`: 대기, 처리중, 성공, 실패, 취소됨
- `결과`, `결과있음`
- `오류메시지`, `오류발생`
- `실행Command`, `초기화Command`

기능 ViewModel과 대응하는 API 영역은 다음과 같다.

| 기능 ViewModel | API 영역 |
| --- | --- |
| `기사프로필기능ViewModel` | home, drivers/register, drivers/me |
| `기사근무기능ViewModel` | work, shifts, driver별 shifts, community inquiries |
| `기사추천기능ViewModel` | recommendations, public-dispatches, requests |
| `기사탐색캠페인기능ViewModel` | exploration-campaigns |
| `기사배차액션기능ViewModel` | dispatch-actions |
| `기사예약기능ViewModel` | reservations |
| `기사운송기능ViewModel` | transports |
| `기사설정기능ViewModel` | preferences |
| `기사정산기능ViewModel` | settlements |
| `기사알림기능ViewModel` | notifications |
| `기사Command기능설정ViewModel` | command-feature-settings |
| `기사개발도구기능ViewModel` | dev-snapshot |

`Food` 기사 컨트롤러는 별도 `FDriverApp` 제품 영역이므로 이 모음에 포함하지 않는다.
