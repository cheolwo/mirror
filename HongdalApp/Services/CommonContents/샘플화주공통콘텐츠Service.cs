using Hongdal.Contracts.CommonContents;

namespace HongdalApp.Services.CommonContents;

public sealed class 샘플화주공통콘텐츠Service : I화주공통콘텐츠Service
{
    public Task<홍달위젯콘텐츠Dto?> 혜택콘텐츠조회Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<홍달위젯콘텐츠Dto?>(new 홍달위젯콘텐츠Dto
        {
            콘텐츠Id = 101,
            제목 = "결제 전 혜택 콘텐츠",
            설명 = "영상 시청 후 할인 혜택을 받을 수 있습니다.",
            이동Url = "https://example.invalid/shipper-benefit",
            상태문구 = "혜택 연결 가능"
        });
    }

    public Task<홍달위젯콘텐츠Dto?> 공지콘텐츠조회Async(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<홍달위젯콘텐츠Dto?>(new 홍달위젯콘텐츠Dto
        {
            콘텐츠Id = 102,
            제목 = "살뜰 운영 공지",
            설명 = "공지/정책 업데이트를 확인하세요.",
            이동Url = "https://example.invalid/shipper-notice",
            상태문구 = "앱 공지"
        });
    }
}
