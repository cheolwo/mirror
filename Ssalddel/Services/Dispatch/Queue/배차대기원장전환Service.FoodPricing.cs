namespace 살뜰.Services.Dispatch.Queue
{
    public sealed partial class 배차대기원장전환Service
    {
        // 생성 시점이 원복 기준이다. 추천 시도마다 별도 객체를 만들고 저장은 전환 흐름이 소유한다.
        private 배차추천요금준비 추천요금준비생성(운송원장 queue)
            => new(_db, queue, _smallGoodsPricing, _음식배달기사제안요금Service);
    }
}
