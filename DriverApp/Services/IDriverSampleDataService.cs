using DriverApp.Models.Driver;
using DriverApp.Models.Driver.Samples;

namespace DriverApp.Services;

public interface IDriverSampleDataService
{
    DriverWorkspaceReadState ReadState => new();

    Task RefreshAsync(
        CancellationToken cancellationToken = default,
        bool force = false);

    기사근무샘플상태 근무상태 { get; }

    기사현재위치샘플 기사현재위치 { get; }

    기사정산샘플요약 정산요약 { get; }

    IReadOnlyList<DriverRequestItem> 추천의뢰목록 { get; }

    IReadOnlyList<기사예약샘플항목> 예약목록 { get; }

    IReadOnlyList<기사운송샘플항목> 운송목록 { get; }

    IReadOnlyList<기사알림샘플항목> 알림목록 { get; }

    DriverRequestItem? 추천의뢰조회(string 의뢰Id);

    IReadOnlyList<추천의뢰표시항목> 거리포함추천의뢰목록조회();

    기사운송샘플항목? 운송조회(long 운송Id);

    기사운송샘플항목? 현재운송조회();
}
