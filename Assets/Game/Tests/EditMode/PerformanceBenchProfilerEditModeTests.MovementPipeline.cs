#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using Core.GameRuntime;
using NUnit.Framework;

namespace ChainRush.Tests.EditMode
{
    public sealed partial class PerformanceBenchProfilerEditModeTests
    {
        [Test]
        public void MovementPipelineAccounting_RequiresSeparateNativeAccessAndCurrentTimingSchema()
        {
            // minimum seam: the export validator must reject an older or partial
            // schema instead of presenting native lookups as ECS work.
            var stageType = typeof(GameRuntimeDiagnostics).GetNestedType("MovementEvidenceStageType", BindingFlags.NonPublic);
            var ownerType = typeof(GameRuntimeDiagnostics).GetNestedType("AccessOwnerType", BindingFlags.NonPublic);
            var stages = new StringBuilder();
            foreach (string stage in Enum.GetNames(stageType))
            { if (stages.Length != 0) stages.Append(','); stages.Append("[\"").Append(stage).Append("\",1,0,0,0]"); }
            var owners = new StringBuilder();
            foreach (string owner in Enum.GetNames(ownerType))
            {
                if (owners.Length != 0) owners.Append(','); owners.Append("{\"owner\":\"").Append(owner).Append('"');
                foreach (string operation in new[] { "Open", "Close", "Refresh", "Read", "Write", "HandleCheck", "PresenceLookup" })
                    owners.Append(",\"").Append(operation).Append("\":0");
                owners.Append('}');
            }
            string native = ",\"nativeAccessOwners\":[" + owners + "]";
            string workers = ",\"workerAccessOwners\":[" + owners.ToString().Replace("\"Open\":0", "\"Rows\":0,\"Open\":0") + "]";
            var topologyRows = new StringBuilder();
            foreach (string origin in new[] { "General", "MovementKinematics", "NavigationBypass", "MovementPhysical" })
            {
                if (topologyRows.Length != 0) topologyRows.Append(',');
                topologyRows.Append("{\"origin\":\"").Append(origin).Append('"');
                foreach (string field in new[] { "requests", "existing", "inserted", "failed", "batches", "growths", "lookups", "hits", "misses" }) topologyRows.Append(",\"").Append(field).Append("\":0");
                topologyRows.Append('}');
            }
            string topology = ",\"topologyWork\":{\"complete\":true,\"coordinateDelta\":0,\"retiredCoordinates\":0,\"initialCoordinates\":0,\"finalCoordinates\":0,\"origins\":[" + topologyRows + "]}";
            string demand = ",\"preparationByRequestOutcome\":{\"groups\":[{},{},{},{},{}]}";
            string preparation = ",\"preparationWork\":{\"EntityPureReads\":0,\"EntityExistsReads\":0,\"EntityReadInvalidations\":0,\"EntityAccessReopens\":0,\"EntityStampCaptures\":0,\"EntityStampChecks\":0,\"PreparedBypassIntents\":0,\"UsedPreparedBypasses\":0,\"UnusedPreparedBypasses\":0,\"LateBypasses\":0}";
            string json = "{\"movementEvidence\":{\"costAccountingVersion\":7,\"observedStepNanoseconds\":10,\"accountedRootNanoseconds\":0,\"stages\":["
                + stages + "],\"accessOwners\":[" + owners + "]" + native + workers + topology + demand + preparation + "}}";
            var validate = BenchType("PerformanceBenchProfilerAdapter").GetMethod("ValidateCostAccounting", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.DoesNotThrow(() => validate.Invoke(null, new object[] { json }));
            var missing = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new object[] { json.Replace(native, "") }));
            Assert.IsInstanceOf<InvalidOperationException>(missing.InnerException);
            var old = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new object[] { json.Replace("\"costAccountingVersion\":7", "\"costAccountingVersion\":3") }));
            Assert.IsInstanceOf<InvalidOperationException>(old.InnerException);
            foreach (string invalid in new[] { json.Replace(preparation, ""), json.Replace("\"PreparedBypassIntents\":0", "\"PreparedBypassIntents\":1"), json.Replace(workers, ""), json.Replace(topology, ""), json.Replace("\"complete\":true", "\"complete\":false"), json.Replace("\"coordinateDelta\":0", "\"coordinateDelta\":1"), json.Replace("\"requests\":0", "\"requests\":1"), json.Replace("\"lookups\":0", "\"lookups\":1") })
            {
                var incomplete = Assert.Throws<TargetInvocationException>(() => validate.Invoke(null, new object[] { invalid }));
                Assert.IsInstanceOf<InvalidOperationException>(incomplete.InnerException);
            }
        }
    }
}
#endif
