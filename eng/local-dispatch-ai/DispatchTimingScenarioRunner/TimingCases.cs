using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.도메인.배차;

internal sealed record TimingRow(int schemaVersion, string caseId, string taskCode, string scenarioFamily,
    string[] sourceRecordIds, string[] sourceBranchIds, string split, object input, object target,
    object teacher, object provenance, object review);

internal static class TimingCases
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Families =
        ["collect", "combination-ready", "collection-expired", "urgent", "sparse", "unknown", "no-driver", "retry"];

    public static List<TimingRow> Generate(IReadOnlyList<JsonElement> records)
    {
        var policy = new FoodDispatchTimingPolicy();
        var settings = new FoodDispatchTimingSettings();
        var result = new List<TimingRow>(records.Count * Families.Length);
        foreach (var record in records.OrderBy(x => x.GetProperty("recordId").GetString(), StringComparer.Ordinal))
        foreach (var family in Families)
        {
            var recordId = record.GetProperty("recordId").GetString()!;
            var branchId = record.GetProperty("branchId").GetString()!;
            var random = new SeededNumbers(recordId + "|" + family + "|52");
            var age = family == "collection-expired" ? 30 + random.Next(26) : 1 + random.Next(24);
            var snapshotAge = random.Next(11);
            var latestSlack = family == "urgent" ? 1 + random.Next(15) : 80 + random.Next(240);
            var waiting = 2 + random.Next(49);
            var compatible = family == "combination-ready" ? 2 + random.Next(3) : random.Next(2);
            waiting = Math.Max(waiting, compatible);
            var input = new FoodDispatchTimingInput
            {
                NowUtc = Now, QueuedAtUtc = Now.AddSeconds(-age), SnapshotAtUtc = Now.AddSeconds(-snapshotAge),
                LatestDispatchAtUtc = Now.AddSeconds(latestSlack), Applicable = true,
                HasActiveOffer = false, IsRetry = family == "retry", DemandKnown = family != "unknown",
                DriverCapacityKnown = family != "unknown", DelayRisk = family == "urgent",
                PromisingPlanAvailable = family == "combination-ready" && random.Next(2) == 1,
                WaitingOrders = waiting, CompatibleOrders = compatible,
                AvailableDrivers = family == "no-driver" ? 0 : 1 + random.Next(10),
                ExpectedNewOrders = family == "unknown" ? null : family == "sparse" ? 0 : (1 + random.Next(100)) / 10m
            };
            var decision = policy.Evaluate(input, settings);
            var expected = family == "collect" ? "Collect" : family == "retry" ? "ExistingFlow" : "DispatchNow";
            if (decision.ActionCode != expected) throw new InvalidOperationException("TimingFamilyAssertionFailed");
            var reasonMatches = family switch
            {
                "collect" => decision.ReasonCode == "ShortCollectionWindow",
                "combination-ready" => decision.ReasonCode is "PromisingPlanAvailable" or "CompatibleOrderThresholdReached",
                "collection-expired" => decision.ReasonCode is "CollectionElapsed" or "DeadlineApproaching",
                "urgent" => decision.ReasonCode == "DelayRisk",
                "sparse" => decision.ReasonCode == "NoExpectedNewOrders",
                "unknown" => decision.ReasonCode == "DemandUnknown",
                "no-driver" => decision.ReasonCode == "NoAvailableDrivers",
                "retry" => decision.ReasonCode == "Retry",
                _ => false
            };
            if (!reasonMatches) throw new InvalidOperationException("TimingFamilyReasonMismatch");
            result.Add(new(1,
                "r52-" + TimingSources.Hash(Encoding.UTF8.GetBytes(recordId + "|DispatchTiming|" + family))[..24],
                "DispatchTiming", family, [recordId], [branchId], TimingSources.Split(branchId),
                new
                {
                    orderAgeSeconds = age, snapshotAgeSeconds = snapshotAge, latestDispatchInSeconds = latestSlack,
                    applicable = input.Applicable, hasActiveOffer = input.HasActiveOffer, isRetry = input.IsRetry,
                    demandKnown = input.DemandKnown, driverCapacityKnown = input.DriverCapacityKnown,
                    delayRisk = input.DelayRisk, promisingPlanAvailable = input.PromisingPlanAvailable,
                    waitingOrders = input.WaitingOrders, compatibleOrders = input.CompatibleOrders,
                    availableDrivers = input.AvailableDrivers, expectedNewOrders = input.ExpectedNewOrders,
                    settings = new
                    {
                        collectionSeconds = settings.CollectionSeconds, maxWaitSeconds = settings.MaxWaitSeconds,
                        minimumCompatibleOrders = settings.MinimumCompatibleOrders,
                        deadlineBufferSeconds = settings.DeadlineBufferSeconds, maxSnapshotAgeSeconds = settings.MaxSnapshotAgeSeconds
                    }
                },
                new
                {
                    actionCode = decision.ActionCode, reasonCode = decision.ReasonCode,
                    recheckInSeconds = decision.RecheckAtUtc is { } recheck ? (int?)(recheck - Now).TotalSeconds : null,
                    hardDeadlineInSeconds = (int)(decision.HardDeadlineUtc - Now).TotalSeconds,
                    allowedActions = decision.AllowedActions
                },
                new { kind = "CurrentPolicyDraft", policyVersion = decision.PolicyVersion, optimalityProven = false },
                new { synthetic = true, timingBasis = "SeededRelativeTimingNotHistoricalObservation",
                    realCustomerLocationsUsed = 0, realDriverLocationsUsed = 0, historicalTimingObserved = false },
                new { status = "Pending", eligibleForTraining = false }));
        }
        return result;
    }

    private sealed class SeededNumbers(string seed)
    {
        private int ordinal;
        public int Next(int limit)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed + "|" + ordinal++));
            return (int)(System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(hash) % limit);
        }
    }
}
