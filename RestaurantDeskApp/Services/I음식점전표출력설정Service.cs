namespace RestaurantDeskApp.Services;

/// <summary>이 기기의 빌지 구성 장수입니다. 프린터 실제 부수나 출력 완료 상태가 아닙니다.</summary>
public sealed record 음식점전표출력설정Snapshot(int 출력장수);

public interface I음식점전표출력설정Service
{
    음식점전표출력설정Snapshot 현재조회();
    Task 저장Async(int 출력장수, CancellationToken cancellationToken = default);
}

/// <summary>기기 저장소의 단일 설정 값입니다. 읽기·저장 실패는 호출자에게 전달합니다.</summary>
public interface I음식점전표출력설정Store
{
    int? Read();
    void Write(int 출력장수);
}
