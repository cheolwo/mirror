using System;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Simulation.Contracts;

namespace Ssalddel.Simulation.Domain
{
    /// <summary>기존 저장 판본과 hash를 유지하는 선택적 출고 상태 봉인.</summary>
    internal static class SimulationFulfillmentSaveReplay
    {
        internal static bool IsEnabled(SimulationSessionSavePackage package)
            => package.SchemaVersion == SimulationSaveSchemaVersions.V34;

        internal static SimulationSessionSavePackage Strip(SimulationSessionSavePackage package)
        {
            var clone = SimulationSaveReplayCloner.ClonePackage(package);
            clone.SchemaVersion = package.FulfillmentBaseSchemaVersion;
            clone.FulfillmentBaseSchemaVersion = string.Empty;
            clone.Fulfillment = null;
            clone.Snapshot.Fulfillment = null;
            clone.ReplayHash = SimulationReplayHasher.Calculate(clone);
            return clone;
        }

        internal static string CalculateHash(SimulationSessionSavePackage package)
        {
            var canonical = string.Join("|", SimulationSaveSchemaVersions.V34,
                package.FulfillmentBaseSchemaVersion, Strip(package).ReplayHash,
                SimulationFulfillmentEngine.BuildStateKey(package.Fulfillment));
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        internal static void Validate(SimulationSessionSavePackage package,
            Action<SimulationSessionSavePackage> validateBase)
        {
            if (string.IsNullOrWhiteSpace(package.FulfillmentBaseSchemaVersion)
                || package.FulfillmentBaseSchemaVersion == SimulationSaveSchemaVersions.V34
                || package.Fulfillment == null || package.Snapshot?.Fulfillment == null
                || package.CommandLog == null || package.SessionCreateRequest == null)
                throw new SimulationContractException("SimulationFulfillmentSaveStateInvalid");
            SimulationFulfillmentEngine.ValidateState(package.Fulfillment);
            if (!string.Equals(SimulationFulfillmentEngine.BuildStateKey(package.Fulfillment),
                    SimulationFulfillmentEngine.BuildStateKey(package.Snapshot.Fulfillment),
                    StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationFulfillmentSaveStateMismatch");
            if (!string.Equals(package.ReplayHash, CalculateHash(package), StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationReplayHashMismatch");
            validateBase(Strip(package));
        }

        internal static 경영SimulationSessionAggregate Restore(SimulationSessionSavePackage package,
            Func<SimulationSessionSavePackage, 경영SimulationSessionAggregate> restoreBase)
        {
            var restored = restoreBase(Strip(package));
            if (!string.Equals(SimulationFulfillmentEngine.BuildStateKey(restored.GetFulfillmentState()),
                    SimulationFulfillmentEngine.BuildStateKey(package.Fulfillment), StringComparison.Ordinal))
                throw new SimulationConflictException("SimulationFulfillmentReplayStateMismatch");
            return restored;
        }
    }
}
