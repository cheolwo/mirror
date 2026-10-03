namespace Ssalddel.Tests.Architecture;

public sealed class FourRoleFoodLifecycleHeadlessCompositionTests
{
    [Fact]
    public void Headless실행기는_네역할앱Client와_서버권위생명주기를_같은주문에결속한다()
    {
        var project = Read("eng", "Ssalddel.RoleAppHeadlessE2E", "Ssalddel.RoleAppHeadlessE2E.csproj");
        var program = Read("eng", "Ssalddel.RoleAppHeadlessE2E", "Program.cs");

        Assert.Contains("OrdererAuthApiService.cs", project);
        Assert.Contains("Ssalddel음식주문Client.cs", project);
        Assert.Contains("FoodDeliveryDriverApiService.cs", project);
        Assert.Contains("AdminAuthenticatedApiClient.cs", project);
        Assert.Contains("EnsureSyntheticMenuAsync", program);
        Assert.Contains("projectionBaseline", program);
        Assert.Contains("expectedWorkStableId: projectionWorkId", program);
        Assert.DoesNotContain("FOOD_OBSERVER_RESTAURANT_ID", program);
        Assert.DoesNotContain("FOOD_OBSERVER_MENU_ID", program);
        Assert.Contains("음식배달운영생명주기단계Codes.음식점응답대기", program);
        Assert.Contains("음식배달운영생명주기단계Codes.기사확보대기", program);
        Assert.Contains("음식점주문진행작업코드.조리시작", program);
        Assert.Contains("음식배달운영생명주기단계Codes.조리배차병행", program);
        Assert.Contains("음식배달운영생명주기단계Codes.픽업인계", program);
        Assert.Contains("음식배달운영생명주기단계Codes.배송", program);
        Assert.Contains("음식배달운영생명주기단계Codes.수령확인대기", program);
        Assert.Contains("음식배달운영생명주기단계Codes.종료", program);
        Assert.Contains("databaseRoundTripProof = true", program);
        Assert.Contains("deviceUiProof = false", program);
    }

    [Fact]
    public void 격리서버검증기는_운영자계정과_운영추적사본을_정본재조회에포함한다()
    {
        var runner = Read(
            "Ssalddel",
            "Services",
            "Development",
            "FoodObserver",
            "음식배달관찰검증Runner.cs");

        Assert.Contains("\"admin\" => 역할명.서버관리자", runner);
        Assert.Contains("api/v1/admin/food-orders/{_orderNo}/operations-trace", runner);
        Assert.Contains("operations.주문상태 == saved.상태", runner);
        Assert.Contains("operations.생명주기조화.정상경로조화여부", runner);
        Assert.Contains("new(\"admin\", \"플랫폼 운영자\"", runner);
    }

    private static string Read(params string[] path)
        => File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. path]));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Ssalddel.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Ssalddel 저장소 루트를 찾을 수 없습니다.");
    }
}
