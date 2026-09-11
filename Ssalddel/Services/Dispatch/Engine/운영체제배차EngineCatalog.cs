using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.도메인.공통;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Engine;

public sealed record 운영체제배차Engine실행계획(
    string 운영체제Id,
    string CatalogRevision,
    OperatingSystemEngineCatalogEntry PrimaryEntry,
    I운송의뢰배차엔진 PrimaryEngine,
    IReadOnlyList<(OperatingSystemEngineCatalogEntry Entry, I운송의뢰배차엔진 Engine)> FallbackEngines,
    IReadOnlyList<(OperatingSystemEngineCatalogEntry Entry, I운송의뢰배차엔진 Engine)> ShadowEngines);

public interface I운영체제배차EngineCatalog
{
    bool TryResolve(
        운송원장 queue,
        out 운영체제배차Engine실행계획 plan,
        out string reason);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Domain,
    "배차 업무를 화물운송 OS 또는 음식배달 OS에 귀속하고 각 OS Catalog의 활성 Primary·Fallback·Shadow 엔진 구현을 해석한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 20,
    StepKey = "domain.operating-system-engine-catalog",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    Boundary = "Engine family만 공통으로 사용한다. 다른 OS 구현을 대체 엔진으로 선택하거나 엔진 결과로 영속 상태를 변경하지 않는다.")]
public sealed class 운영체제배차EngineCatalog : I운영체제배차EngineCatalog
{
    private readonly IReadOnlyDictionary<string, I운송의뢰배차엔진> engines;

    public 운영체제배차EngineCatalog(IEnumerable<I운송의뢰배차엔진> engines)
    {
        ArgumentNullException.ThrowIfNull(engines);
        var groups = engines.GroupBy(engine => engine.엔진코드, StringComparer.Ordinal).ToArray();
        var duplicates = groups
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"동일 구현 ID의 배차 엔진이 중복 등록되었습니다. Engines={string.Join(',', duplicates)}");
        }

        this.engines = groups.ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
    }

    public bool TryResolve(
        운송원장 queue,
        out 운영체제배차Engine실행계획 plan,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(queue);
        var operatingSystemId = queue.배차업무유형 switch
        {
            상태값.배차업무유형.용달운송 => OperatingSystemIds.DomesticCargoTransport,
            상태값.배차업무유형.음식배달 => OperatingSystemIds.FoodDelivery,
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(operatingSystemId))
        {
            plan = null!;
            reason = $"배차업무유형에 대응하는 운영 OS가 없습니다. Type={queue.배차업무유형}";
            return false;
        }

        if (!OperatingSystemEngineCatalog.TryGetActivePrimary(
                operatingSystemId,
                EngineFamilyIds.TransportRequestDispatch,
                out var primaryEntry))
        {
            plan = null!;
            reason = $"운영 OS에 활성 Primary 배차 엔진이 없습니다. OS={operatingSystemId}";
            return false;
        }

        if (!TryResolveEngine(primaryEntry, out var primary, out reason))
        {
            plan = null!;
            return false;
        }

        var entries = OperatingSystemEngineCatalog.GetByOperatingSystemAndFamily(
            operatingSystemId,
            EngineFamilyIds.TransportRequestDispatch);
        var fallbacks = ResolveOptional(entries, OperatingSystemEngineRoles.ApprovedSafeFallback);
        var shadows = ResolveOptional(entries, OperatingSystemEngineRoles.Shadow);
        plan = new 운영체제배차Engine실행계획(
            operatingSystemId,
            OperatingSystemEngineCatalog.CatalogRevision,
            primaryEntry,
            primary,
            fallbacks,
            shadows);
        reason = string.Empty;
        return true;
    }

    private bool TryResolveEngine(
        OperatingSystemEngineCatalogEntry entry,
        out I운송의뢰배차엔진 engine,
        out string reason)
    {
        if (!engines.TryGetValue(entry.ImplementationId, out engine!))
        {
            reason = $"Catalog에 등록된 배차 엔진 구현이 DI에 없습니다. Engine={entry.ImplementationId}";
            return false;
        }

        if (engine.운영체제Id != entry.OperatingSystemId
            || engine.논리엔진코드 != entry.EngineFamilyId)
        {
            reason = $"배차 엔진 구현의 OS 또는 family가 Catalog와 다릅니다. Engine={entry.ImplementationId}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private IReadOnlyList<(OperatingSystemEngineCatalogEntry Entry, I운송의뢰배차엔진 Engine)> ResolveOptional(
        IEnumerable<OperatingSystemEngineCatalogEntry> entries,
        string role)
    {
        var resolved = new List<(OperatingSystemEngineCatalogEntry, I운송의뢰배차엔진)>();
        foreach (var entry in entries.Where(item =>
                     item.Role == role
                     && item.ActivationStatus != OperatingSystemEngineActivationStatuses.Disabled))
        {
            if (!TryResolveEngine(entry, out var engine, out var reason))
            {
                throw new InvalidOperationException(reason);
            }

            resolved.Add((entry, engine));
        }

        return resolved;
    }
}
