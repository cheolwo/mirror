using Hongdal.Contracts.Common.VehicleLoading;

namespace Hongdal.Services.LogisticsProcessing.VehicleLoading;

public sealed class 차량혼적순서Engine
{
    public 혼적상하차순서계획 계획(IReadOnlyList<혼적화물순서요청항목> 화물목록)
        => 혼적상하차순서계획기.계획(화물목록);
}
