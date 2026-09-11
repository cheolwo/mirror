using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "음식 배달 완료 상태 사본의 개인정보·온라인 휘발 계약을 자동 검증한다.",
    Boundary = "계약 정적 검증과 실제 운영 서버·Unity Runtime·Game View 증거를 구분한다.")]
public sealed class 음식배달완료WorldSnapshotContractTests
{
    [Fact]
    public void Unity완료사본계약은_원천개인식별자와정확위치를요구하지않는다()
    {
        var propertyNames = typeof(음식배달완료WorldSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("OrderStableId", propertyNames);
        Assert.DoesNotContain("OrdererUserId", propertyNames);
        Assert.DoesNotContain("DriverUserId", propertyNames);
        Assert.DoesNotContain("Address", propertyNames);
        Assert.DoesNotContain("Latitude", propertyNames);
        Assert.DoesNotContain("Longitude", propertyNames);
    }

    [Fact]
    public void Unity완료사본은_온라인휘발성과30초갱신을명시한다()
    {
        var snapshot = new 음식배달완료WorldSnapshot();
        var response = new 음식배달완료WorldSnapshot목록응답();

        Assert.Equal(음식배달완료WorldSnapshot정책.OnlineEphemeral, snapshot.DataPolicyCode);
        Assert.False(snapshot.LocalStorageAllowed);
        Assert.False(snapshot.ReplayAllowed);
        Assert.Equal(30, response.RefreshAfterSeconds);
    }
}
