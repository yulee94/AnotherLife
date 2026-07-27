using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode.Territories
{
    public class TerritoryContractPlannerTests
    {
        [Test]
        public void CurrentBaselineProducesImmutableT1ThroughT5Inventory()
        {
            object planner = CreateBaselinePlanner();
            object states = InvokeStatic(PlannerType, "CreateCurrentBaselineStates");
            object query = Invoke(planner, "BuildQuery", states, Realm("Stonehold"));

            Assert.AreEqual("Available", Property(query, "Status").ToString());
            Assert.AreEqual("territory_current_v1", Property(query, "CatalogId"));

            object[] territories = Items(Property(query, "Territories"));
            Assert.AreEqual(new[] { "T1", "T2", "T3", "T4", "T5" }, territories.Select(TerritoryId).ToArray());
            Assert.AreEqual(new[] { "Stonehold", "Eldergrove", "Crownlands", "Umbral", "None" }, territories.Select(OwnerName).ToArray());
            Assert.AreEqual(new[] { 50L, 40L, 20L, 60L, 10L }, territories.Select(BonusAmount).ToArray());

            Assert.Throws<NotSupportedException>(() => ((IList)Property(query, "Territories")).Clear());
        }

        [Test]
        public void QueryRejectsDuplicateKnownTerritoryWithoutFirstRowFallback()
        {
            object planner = CreateBaselinePlanner();
            Array states = ArrayOf(StateRecordType,
                NewState("T1", "Stonehold", 0L),
                NewState("T1", "Eldergrove", 0L),
                NewState("T2", "Eldergrove", 0L),
                NewState("T3", "Crownlands", 0L),
                NewState("T4", "Umbral", 0L),
                NewState("T5", "None", 0L));

            object query = Invoke(planner, "BuildQuery", states, Realm("Stonehold"));

            Assert.AreEqual("Unavailable", Property(query, "Status").ToString());
            AssertDiagnostic(query, "DuplicateStateId", "T1");
            Assert.False(Items(Property(query, "Territories")).Any(item => TerritoryId(item) == "T1"));
        }

        [Test]
        public void QueryPreservesUnknownFutureTerritoryButExcludesItFromSupportedIncome()
        {
            object planner = CreateBaselinePlanner();
            Array states = ArrayOf(StateRecordType,
                NewState("T1", "Stonehold", 0L),
                NewState("T2", "Eldergrove", 0L),
                NewState("T3", "Crownlands", 0L),
                NewState("T4", "Umbral", 0L),
                NewState("T5", "None", 0L),
                NewState("T99", "Stonehold", 4L));

            object query = Invoke(planner, "BuildQuery", states, Realm("Stonehold"));
            object income = Invoke(planner, "PlanIncome", query, Realm("Stonehold"));

            Assert.AreEqual("Available", Property(query, "Status").ToString());
            AssertDiagnostic(query, "PreservedUnknownTerritory", "T99");
            Assert.True(Items(Property(query, "Territories")).Any(item => TerritoryId(item) == "T99" && !(bool)Property(item, "IsSupported")));

            object[] contributions = Items(Property(income, "Contributions"));
            Assert.AreEqual(new[] { "T1" }, contributions.Select(item => Property(item, "TerritoryId").ToString()).ToArray());
            Assert.AreEqual(50L, Property(contributions.Single(), "AmountPerMinute"));
        }

        [Test]
        public void SameOwnerCapturePlansNoMutationOrRewards()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "Stonehold");
            object authorization = NewAuthorization("auth-1", "T1", "Stonehold", "Stonehold", 0L);
            object request = NewCaptureRequest("op-1", "T1", "Stonehold", "Stonehold", "Stonehold", 0L, authorization);

            object plan = Invoke(planner, "PlanCapture", query, request);

            Assert.AreEqual("NoChangeSameOwner", Property(plan, "Status").ToString());
            Assert.AreEqual("Stonehold", Property(plan, "PreviousOwner").ToString());
            Assert.AreEqual("Stonehold", Property(plan, "NewOwner").ToString());
            Assert.AreEqual(0L, Property(plan, "PreviousRevision"));
            Assert.AreEqual(0L, Property(plan, "NewRevision"));
            Assert.AreEqual(0, Property(plan, "WarzoneCreditsDelta"));
            Assert.AreEqual(0, Property(plan, "QuestProgressDelta"));
        }

        [Test]
        public void NeutralCapturePlansOneRevisionAndCurrentRewards()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "Crownlands");
            object authorization = NewAuthorization("auth-2", "T5", "Crownlands", "None", 0L);
            object request = NewCaptureRequest("op-2", "T5", "Crownlands", "Crownlands", "None", 0L, authorization);

            object plan = Invoke(planner, "PlanCapture", query, request);

            Assert.AreEqual("Planned", Property(plan, "Status").ToString());
            Assert.AreEqual("None", Property(plan, "PreviousOwner").ToString());
            Assert.AreEqual("Crownlands", Property(plan, "NewOwner").ToString());
            Assert.AreEqual(0L, Property(plan, "PreviousRevision"));
            Assert.AreEqual(1L, Property(plan, "NewRevision"));
            Assert.AreEqual(100, Property(plan, "WarzoneCreditsDelta"));
            Assert.AreEqual(1, Property(plan, "QuestProgressDelta"));
        }

        [Test]
        public void CaptureRejectsStaleOwnerBeforeRewardPlanning()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "Crownlands");
            object authorization = NewAuthorization("auth-3", "T5", "Crownlands", "Stonehold", 0L);
            object request = NewCaptureRequest("op-3", "T5", "Crownlands", "Crownlands", "Stonehold", 0L, authorization);

            object plan = Invoke(planner, "PlanCapture", query, request);

            Assert.AreEqual("RejectedStaleOwner", Property(plan, "Status").ToString());
            Assert.AreEqual(0, Property(plan, "WarzoneCreditsDelta"));
            Assert.AreEqual(0, Property(plan, "QuestProgressDelta"));
            AssertDiagnostic(plan, "StaleOwner", "T5");
        }

        [Test]
        public void CaptureRejectsNoneRealmAndMissingAuthorization()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "Stonehold");

            object noRealmRequest = NewCaptureRequest("op-4", "T5", "None", "None", "None", 0L, NewAuthorization("auth-4", "T5", "None", "None", 0L));
            object noRealmPlan = Invoke(planner, "PlanCapture", query, noRealmRequest);
            Assert.AreEqual("RejectedNoCommittedRealm", Property(noRealmPlan, "Status").ToString());

            object missingAuthRequest = NewCaptureRequest("op-5", "T5", "Stonehold", "Stonehold", "None", 0L, null);
            object missingAuthPlan = Invoke(planner, "PlanCapture", query, missingAuthRequest);
            Assert.AreEqual("RejectedUnauthorized", Property(missingAuthPlan, "Status").ToString());
            AssertDiagnostic(missingAuthPlan, "MissingAuthorization", "T5");
        }

        [TestCase("Stonehold", "T1", "Stone", 50L)]
        [TestCase("Eldergrove", "T2", "Wood", 40L)]
        [TestCase("Crownlands", "T3", "Gold", 20L)]
        [TestCase("Umbral", "T4", "Food", 60L)]
        public void IncomeSnapshotUsesOneRevisionAndExactCurrentOwnedTotals(
            string committedRealm,
            string expectedTerritoryId,
            string expectedResourceType,
            long expectedAmountPerMinute)
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, committedRealm);
            object income = Invoke(planner, "PlanIncome", query, Realm(committedRealm));

            Assert.AreEqual("Available", Property(income, "Status").ToString());
            Assert.AreEqual(Property(query, "StateRevisionHash"), Property(income, "StateRevisionHash"));

            object[] contributions = Items(Property(income, "Contributions"));
            Assert.AreEqual(new[] { expectedTerritoryId }, contributions.Select(item => Property(item, "TerritoryId").ToString()).ToArray());
            Assert.AreEqual(expectedResourceType, Property(contributions.Single(), "ResourceType").ToString());
            Assert.AreEqual(expectedAmountPerMinute, Property(contributions.Single(), "AmountPerMinute"));
        }

        [Test]
        public void IncomeRejectsUncommittedProfileBeforeNeutralTerritoryCanContribute()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "None");
            object income = Invoke(planner, "PlanIncome", query, Realm("None"));

            Assert.AreEqual("Unavailable", Property(income, "Status").ToString());
            Assert.IsEmpty(Items(Property(income, "Contributions")));
            AssertDiagnostic(income, "NoCommittedRealm", string.Empty);
        }

        [Test]
        public void IncomeRejectsUndefinedCommittedProfileRealm()
        {
            object planner = CreateBaselinePlanner();
            object undefinedRealm = Enum.ToObject(RealmType, 999);
            object query = Invoke(planner, "BuildQuery", InvokeStatic(PlannerType, "CreateCurrentBaselineStates"), undefinedRealm);
            object income = Invoke(planner, "PlanIncome", query, undefinedRealm);

            Assert.AreEqual("Unavailable", Property(income, "Status").ToString());
            Assert.IsEmpty(Items(Property(income, "Contributions")));
            AssertDiagnostic(income, "InvalidCommittedRealm", string.Empty);
        }

        [Test]
        public void IncomeRejectsExpectedRealmThatDiffersFromCommittedProfile()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "Stonehold");
            object income = Invoke(planner, "PlanIncome", query, Realm("Crownlands"));

            Assert.AreEqual("Unavailable", Property(income, "Status").ToString());
            Assert.IsEmpty(Items(Property(income, "Contributions")));
            AssertDiagnostic(income, "ProfileRealmMismatch", string.Empty);
        }

        [Test]
        public void IncomeRejectsUndefinedExpectedRealm()
        {
            object planner = CreateBaselinePlanner();
            object query = QueryBaseline(planner, "Stonehold");
            object income = Invoke(planner, "PlanIncome", query, Enum.ToObject(RealmType, 999));

            Assert.AreEqual("Unavailable", Property(income, "Status").ToString());
            Assert.IsEmpty(Items(Property(income, "Contributions")));
            AssertDiagnostic(income, "InvalidExpectedRealm", string.Empty);
        }

        private static object QueryBaseline(object planner, string committedRealm)
        {
            return Invoke(planner, "BuildQuery", InvokeStatic(PlannerType, "CreateCurrentBaselineStates"), Realm(committedRealm));
        }

        private static object CreateBaselinePlanner()
        {
            return InvokeStatic(PlannerType, "CreateCurrentBaseline");
        }

        private static object NewState(string id, string owner, long revision)
        {
            return Activator.CreateInstance(StateRecordType, id, Realm(owner), revision);
        }

        private static object NewAuthorization(string authorizationId, string territoryId, string capturerRealm, string expectedPreviousOwner, long expectedRevision)
        {
            return Activator.CreateInstance(AuthorizationType, authorizationId, territoryId, Realm(capturerRealm), Realm(expectedPreviousOwner), expectedRevision);
        }

        private static object NewCaptureRequest(string operationId, string territoryId, string committedProfileRealm, string expectedCapturerRealm, string expectedPreviousOwner, long expectedRevision, object authorization)
        {
            return Activator.CreateInstance(RequestType, operationId, territoryId, Realm(committedProfileRealm), Realm(expectedCapturerRealm), Realm(expectedPreviousOwner), expectedRevision, authorization);
        }

        private static string TerritoryId(object snapshot)
        {
            return Property(Property(snapshot, "State"), "Id").ToString();
        }

        private static string OwnerName(object snapshot)
        {
            return Property(Property(snapshot, "State"), "Owner").ToString();
        }

        private static long BonusAmount(object snapshot)
        {
            return Convert.ToInt64(Property(Property(snapshot, "Definition"), "BonusAmount"));
        }

        private static void AssertDiagnostic(object result, string expectedCode, string expectedTerritoryId)
        {
            object[] diagnostics = Items(Property(result, "Diagnostics"));
            Assert.True(
                diagnostics.Any(diagnostic => Property(diagnostic, "Code").ToString() == expectedCode && Property(diagnostic, "TerritoryId").ToString() == expectedTerritoryId),
                $"Expected diagnostic {expectedCode} for {expectedTerritoryId}.");
        }

        private static object[] Items(object value)
        {
            return ((IEnumerable)value).Cast<object>().ToArray();
        }

        private static Array ArrayOf(Type elementType, params object[] values)
        {
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                array.SetValue(values[index], index);
            }

            return array;
        }

        private static object InvokeStatic(Type type, string methodName, params object[] args)
        {
            MethodInfo method = Method(type, methodName, args.Length, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return method.Invoke(null, args);
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = Method(target.GetType(), methodName, args.Length, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return method.Invoke(target, args);
        }

        private static MethodInfo Method(Type type, string methodName, int argumentCount, BindingFlags flags)
        {
            MethodInfo method = type.GetMethods(flags).FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == argumentCount);
            Assert.NotNull(method, $"Expected {type.FullName}.{methodName} with {argumentCount} arguments.");
            return method;
        }

        private static object Property(object target, string name)
        {
            Assert.NotNull(target, $"Expected target for property {name}.");
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {target.GetType().FullName}.{name}.");
            return property.GetValue(target);
        }

        private static object Realm(string value)
        {
            return Enum.Parse(RealmType, value);
        }

        private static Type PlannerType => RuntimeType("AL.RealmWar.Territories.Contracts.TerritoryContractPlanner");
        private static Type StateRecordType => RuntimeType("AL.RealmWar.Territories.Contracts.TerritoryStateRecord");
        private static Type AuthorizationType => RuntimeType("AL.RealmWar.Territories.Contracts.TerritoryCaptureAuthorization");
        private static Type RequestType => RuntimeType("AL.RealmWar.Territories.Contracts.TerritoryCaptureRequest");
        private static Type RealmType => RuntimeType("AL.Core.RealmId");

        private static Type RuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
            Assert.NotNull(type, $"Expected loaded runtime type {typeName}.");
            return type;
        }
    }
}
