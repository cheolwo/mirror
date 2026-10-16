namespace 살뜰.Services.Dispatch.Common;

/// <summary>기존 멱등 요청의 내용 변경만 구분한다. 저장소 장애를 요청 충돌로 숨기지 않는다.</summary>
public sealed class 운영배차수신요청충돌Exception()
    : InvalidOperationException("같은 클라이언트 요청 ID에 다른 배차 수신 의사 요청이 있습니다.");
