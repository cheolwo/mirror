using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Food;
using Ssalddel.Application.Warehouse;
using Ssalddel.Application.WorldProjection;
using Ssalddel.Contracts.Common.WorldProjection;
using Ssalddel.Services.Food;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;

namespace Ssalddel.Tests.Application.WorldProjection;

public sealed class 운영지역장면조회UseCaseTests
{
    private const string Area = 음식배달완료WorldAreaStableIds.Myeonmok;

    [Fact]
    public async Task 음식_창고_화물완료를_개인식별정보없이_한지역장면으로조합한다()
    {
        await using var db = CreateContext();
        db.창고.Add(new 창고
        {
            Id = 7,
            소유자UserId = "owner",
            창고명 = "면목 공동 창고",
            주소 = "서울특별시 중랑구 면목동 1",
            IsActive = true
        });
        db.생활권물류거점.Add(new 생활권물류거점
        {
            StableId = "neighborhood-logistics-hub:public",
            신청가원장Id = "private-application-ledger",
            신청자UserId = "private-space-owner",
            관리담당자UserId = "private-site-manager",
            공간StableId = "building:public",
            생활권Key = Area,
            대략위치Label = "면목 생활권",
            정확위치보호참조 = "protected-place:private",
            상태Code = 생활권물류거점상태Codes.Pilot,
            기사인계가능 = true,
            주문자수령가능 = true,
            최대동시보관건수 = 3,
            현재예약건수 = 1,
            UpdatedAtUtc = DateTime.UtcNow
        });
        db.운송원장.Add(new 운송원장
        {
            Id = 9,
            의뢰Id = "private-request-id",
            운송번호 = "private-transport-number",
            화주Id = "private-shipper-id",
            기사_운송자 = "private-driver-id",
            확정기사Id = "private-driver-id",
            배차업무유형 = 상태값.배차업무유형.용달운송,
            상태 = "인수완료",
            픽업_도로명주소 = "서울특별시 중랑구 면목동 2",
            하차_도로명주소 = "서울특별시 중랑구 면목동 3",
            UpdatedAt = DateTime.UtcNow
        });
        var stages = new[] { "상차지도착", "상차완료", "하차지도착", "인수완료" };
        for (var index = 0; index < stages.Length; index++)
        {
            db.운송이벤트.Add(new 운송이벤트
            {
                의뢰Id = "private-request-id",
                이벤트타입 = 운송이벤트유형.기사운송상태변경,
                이벤트시각 = DateTime.UtcNow.AddMinutes(-4 + index),
                메타데이터 = JsonSerializer.Serialize(new
                {
                    targetState = stages[index],
                    completionProjectionToken = index == stages.Length - 1 ? "publictoken" : string.Empty
                })
            });
        }
        await db.SaveChangesAsync();

        var useCase = new 운영지역장면조회UseCase(
            db,
            new AdminCurrentUser(),
            new FoodReader(),
            new EmptyActiveFoodReader(),
            new WarehouseReader(),
            new 음식배달완료WorldAreaResolver(),
            new Empty관찰운영검증ProjectionReader(),
            NullLogger<운영지역장면조회UseCase>.Instance);

        var response = await useCase.조회Async(Area, 0, CancellationToken.None);

        Assert.Contains(response.Items, item => item.OperatingSystemId == "FoodDeliveryOS");
        Assert.Contains(response.Items, item => item.ItemKind == OperationalWorldSceneItemKinds.WarehouseTask);
        Assert.Contains(response.Items, item => item.RoleCode == "NeighborhoodMicroHub");
        Assert.Contains(response.Items, item => item.SnapshotStableId == "cargo-completed:publictoken");
        var json = JsonSerializer.Serialize(response);
        Assert.DoesNotContain("private-request-id", json);
        Assert.DoesNotContain("private-transport-number", json);
        Assert.DoesNotContain("private-driver-id", json);
        Assert.DoesNotContain("private-shipper-id", json);
        Assert.DoesNotContain("private-application-ledger", json);
        Assert.DoesNotContain("private-space-owner", json);
        Assert.DoesNotContain("private-site-manager", json);
        Assert.DoesNotContain("protected-place:private", json);
        Assert.DoesNotContain("latitude", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("longitude", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 한자료원실패는_다른자료원응답을막지않는다()
    {
        await using var db = CreateContext();
        var useCase = new 운영지역장면조회UseCase(
            db,
            new AdminCurrentUser(),
            new ThrowingFoodReader(),
            new EmptyActiveFoodReader(),
            new WarehouseReader(),
            new 음식배달완료WorldAreaResolver(),
            new Empty관찰운영검증ProjectionReader(),
            NullLogger<운영지역장면조회UseCase>.Instance);

        var response = await useCase.조회Async(Area, 0, CancellationToken.None);

        Assert.Contains(response.SourceFailures, failure => failure.SourceCode == "FoodDeliveryOS");
    }

    [Fact]
    public async Task v2는_검증표본과_운영투영을_출처와의미위치로구분한다()
    {
        await using var db = CreateContext();
        var useCase = new 운영지역장면조회UseCase(
            db,
            new AdminCurrentUser(),
            new FoodReader(),
            new EmptyActiveFoodReader(),
            new WarehouseReader(),
            new 음식배달완료WorldAreaResolver(),
            new VerificationReader(),
            NullLogger<운영지역장면조회UseCase>.Instance);

        var response = await useCase.조회Async(
            Area,
            0,
            OperationalWorldScenePolicy.SchemaVersionV2,
            CancellationToken.None);

        Assert.Equal(OperationalWorldScenePolicy.SchemaVersionV2, response.SchemaVersion);
        var operational = Assert.Single(response.Items, item => item.SnapshotStableId == "food-completed:public");
        Assert.Equal(OperationalWorldSceneSourceKinds.OperationalProjection, operational.SourceKindCode);
        Assert.NotEmpty(operational.WorkStableId);
        Assert.StartsWith("semantic-place:", operational.SemanticPlaceStableId);
        var sample = Assert.Single(response.Items, item => item.SourceKindCode == OperationalWorldSceneSourceKinds.VerificationSample);
        Assert.Equal("observable-operations-run:test", sample.ScenarioRunStableId);
        Assert.Equal("synthetic-place:food-route-a", sample.SemanticPlaceStableId);
        Assert.False(sample.LocalStorageAllowed);
        Assert.False(sample.ReplayAllowed);
    }

    [Fact]
    public async Task 진행중음식배달은_v2에만_비식별지역투영으로합성한다()
    {
        await using var db = CreateContext();
        var useCase = new 운영지역장면조회UseCase(
            db,
            new AdminCurrentUser(),
            new FoodReader(),
            new ActiveFoodReader(),
            new WarehouseReader(),
            new 음식배달완료WorldAreaResolver(),
            new Empty관찰운영검증ProjectionReader(),
            NullLogger<운영지역장면조회UseCase>.Instance);

        var v1 = await useCase.조회Async(Area, 0, OperationalWorldScenePolicy.SchemaVersionV1, default);
        var v2 = await useCase.조회Async(Area, 0, OperationalWorldScenePolicy.SchemaVersionV2, default);

        Assert.DoesNotContain(v1.Items, item => item.ItemKind == OperationalWorldSceneItemKinds.ActiveLifecycle);
        var active = Assert.Single(v2.Items, item => item.ItemKind == OperationalWorldSceneItemKinds.ActiveLifecycle);
        Assert.Equal(OperationalWorldOperatingSystemIds.FoodDelivery, active.OperatingSystemId);
        Assert.Equal(OperationalWorldSceneSourceKinds.OperationalProjection, active.SourceKindCode);
        Assert.Equal("semantic-place:area:food-delivery-route", active.SemanticPlaceStableId);
        Assert.False(active.LocalStorageAllowed);
        Assert.False(active.ReplayAllowed);
    }

    [Fact]
    public async Task 지원하지않는_v2판본은_조회단에서거절한다()
    {
        await using var db = CreateContext();
        var useCase = new 운영지역장면조회UseCase(
            db,
            new AdminCurrentUser(),
            new FoodReader(),
            new EmptyActiveFoodReader(),
            new WarehouseReader(),
            new 음식배달완료WorldAreaResolver(),
            new Empty관찰운영검증ProjectionReader(),
            NullLogger<운영지역장면조회UseCase>.Instance);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => useCase.조회Async(
            Area,
            0,
            "operational-world-scene.v999",
            CancellationToken.None));
        Assert.Contains("Unsupported", exception.Message, StringComparison.Ordinal);
    }

    private static SsalddelContext CreateContext()
        => new(
            new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options,
            new DummyEncryption());

    private sealed class AdminCurrentUser : ICurrentUserAccessor
    {
        public string? UserId => "admin";
        public string? Role => 역할명.서버관리자;
    }

    private sealed class FoodReader : I음식배달완료WorldSnapshot조회UseCase
    {
        public Task<음식배달완료WorldSnapshot목록응답> 지역목록Async(string areaStableId, int take, CancellationToken cancellationToken)
            => Task.FromResult(new 음식배달완료WorldSnapshot목록응답
            {
                AreaStableId = areaStableId,
                Items =
                [
                    new 음식배달완료WorldSnapshot
                    {
                        SnapshotStableId = "food-completed:public",
                        AreaStableId = areaStableId,
                        LifecycleRevision = 7,
                        CompletedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                        PublishedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(59)
                    }
                ]
            });
    }

    private sealed class ThrowingFoodReader : I음식배달완료WorldSnapshot조회UseCase
    {
        public Task<음식배달완료WorldSnapshot목록응답> 지역목록Async(string areaStableId, int take, CancellationToken cancellationToken)
            => throw new InvalidOperationException("temporary");
    }

    private sealed class EmptyActiveFoodReader : I진행중음식배달WorldProjectionReader
    {
        public Task<OperationalWorldSceneItem[]> 지역목록Async(
            string areaStableId,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.FromResult(Array.Empty<OperationalWorldSceneItem>());
    }

    private sealed class ActiveFoodReader : I진행중음식배달WorldProjectionReader
    {
        public Task<OperationalWorldSceneItem[]> 지역목록Async(
            string areaStableId,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.FromResult(new[]
            {
                new OperationalWorldSceneItem
                {
                    SnapshotStableId = "food-delivery-active:pseudonym",
                    WorkStableId = "food-delivery-work:pseudonym",
                    AreaStableId = areaStableId,
                    OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
                    ItemKind = OperationalWorldSceneItemKinds.ActiveLifecycle,
                    RoleCode = "DeliveryDriver",
                    ActivityCode = "픽업완료",
                    LifecycleStageCode = "픽업완료",
                    Revision = 5,
                    OccurredAtUtc = utcNow.AddSeconds(-1),
                    PublishedAtUtc = utcNow,
                    ExpiresAtUtc = utcNow.AddMinutes(2),
                    SemanticPlaceStableId = "semantic-place:area:food-delivery-route",
                    SourceKindCode = OperationalWorldSceneSourceKinds.OperationalProjection
                }
            });
    }

    private sealed class WarehouseReader : I창고WorldSnapshot조회UseCase
    {
        public Task<Result<WarehouseWorldSnapshotResponse>> 조회Async(long? warehouseId, CancellationToken cancellationToken)
            => Task.FromResult(Result.Ok(new WarehouseWorldSnapshotResponse
            {
                StableId = "warehouse-zone:7",
                Revision = "r1",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                Tasks =
                [
                    new WarehouseWorldTaskResponse
                    {
                        StableId = "warehouse-task:public",
                        WarehouseStableId = "warehouse:7",
                        TaskKind = "PutAway",
                        Status = "대기",
                        UpdatedAtUtc = DateTimeOffset.UtcNow
                    }
                ]
            }));
    }

    private sealed class VerificationReader : I관찰운영검증ProjectionReader
    {
        public Task<OperationalWorldSceneItem[]> 지역목록Async(
            string areaStableId,
            DateTime utcNow,
            CancellationToken cancellationToken)
            => Task.FromResult(new[]
            {
                new OperationalWorldSceneItem
                {
                    SnapshotStableId = "sample-observation:test:food-normal",
                    AreaStableId = areaStableId,
                    OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
                    ItemKind = OperationalWorldSceneItemKinds.CompletedLifecycle,
                    RoleCode = "FoodDeliveryTeam",
                    ActivityCode = "ReceiptConfirmed",
                    Revision = 1,
                    OccurredAtUtc = utcNow,
                    PublishedAtUtc = utcNow,
                    ExpiresAtUtc = utcNow.AddMinutes(30),
                    WorkStableId = "sample-work:test:food-normal",
                    LifecycleStageCode = "ReceiptConfirmed",
                    AttentionStateCode = OperationalWorldAttentionStateCodes.Completed,
                    ObjectKindCode = "FoodDelivery",
                    SemanticPlaceStableId = "synthetic-place:food-route-a",
                    SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample,
                    ScenarioRunStableId = "observable-operations-run:test"
                }
            });
    }

    private sealed class DummyEncryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
