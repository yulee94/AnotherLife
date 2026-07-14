using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode
{
    public class SavePersistenceRegressionTests
    {
        [Test]
        public void EnsureSaveDefaultsInitializesNarrativeCompatibilityFields()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            object save = Activator.CreateInstance(saveType);

            SetField(save, "Reputation", null);
            SetField(save, "FactionReputations", null);
            SetField(save, "LordPersona", null);

            InvokeEnsureSaveDefaults(save);

            Assert.NotNull(GetField(save, "Reputation"));
            Assert.NotNull(GetField(save, "FactionReputations"));
            Assert.NotNull(GetField(save, "LordPersona"));
        }

        [Test]
        public void EnsureSaveDefaultsPreservesExistingNarrativeCompatibilityData()
        {
            Type saveType = GetRuntimeType("AL.Data.Runtime.SaveGameData");
            Type affinityType = GetRuntimeType("AL.Data.Runtime.NpcAffinityData");
            Type factionType = GetRuntimeType("AL.Data.Runtime.FactionRepData");
            Type personaType = GetRuntimeType("AL.Data.Runtime.PersonaData");
            object save = Activator.CreateInstance(saveType);

            object affinity = Activator.CreateInstance(affinityType);
            SetField(affinity, "NpcId", "ADVISOR_VALERIUS");
            SetField(affinity, "Affinity", 17.5f);
            IList reputation = CreateRuntimeList(affinityType);
            reputation.Add(affinity);

            object faction = Activator.CreateInstance(factionType);
            SetField(faction, "FactionId", "FACTION_VEIL_WATCH");
            SetField(faction, "Reputation", 9);
            IList factions = CreateRuntimeList(factionType);
            factions.Add(faction);

            object persona = Activator.CreateInstance(personaType);
            SetField(persona, "Warlord", 3);
            SetField(persona, "Diplomat", 7);
            SetField(persona, "Sage", 11);
            SetField(persona, "Rogue", 2);

            SetField(save, "Reputation", reputation);
            SetField(save, "FactionReputations", factions);
            SetField(save, "LordPersona", persona);

            InvokeEnsureSaveDefaults(save);

            Assert.AreSame(reputation, GetField(save, "Reputation"));
            Assert.AreSame(factions, GetField(save, "FactionReputations"));
            Assert.AreSame(persona, GetField(save, "LordPersona"));
            Assert.AreEqual("ADVISOR_VALERIUS", GetField(reputation[0], "NpcId"));
            Assert.AreEqual(17.5f, GetField(reputation[0], "Affinity"));
            Assert.AreEqual("FACTION_VEIL_WATCH", GetField(factions[0], "FactionId"));
            Assert.AreEqual(9, GetField(factions[0], "Reputation"));
            Assert.AreEqual(7, GetField(persona, "Diplomat"));
            Assert.AreEqual(11, GetField(persona, "Sage"));
        }

        [Test]
        public void CorruptedPrimaryRecoversLastKnownGoodBackup()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                object currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_BACKUP");
                Invoke(service, "Save");

                SetField(currentSave, "CurrentChapterId", "C1_PRIMARY");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                string tempPath = Path.Combine(root, "save.tmp.json");
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));

                File.WriteAllText(primaryPath, "{ this is not valid json");

                object recoveredService = CreateSaveService(root);
                Invoke(recoveredService, "Load");

                Assert.AreEqual("RecoveredFromBackup", GetProperty(recoveredService, "LastLoadStatus").ToString());
                object recoveredSave = GetProperty(recoveredService, "CurrentSave");
                Assert.AreEqual("C1_BACKUP", GetField(recoveredSave, "CurrentChapterId"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
                Assert.False(File.Exists(tempPath));
                Assert.That(File.ReadAllText(primaryPath), Does.Contain("C1_BACKUP"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static object CreateSaveService(string root)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            ConstructorInfo constructor = serviceType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.NotNull(constructor, "Expected the testable persistence-path constructor.");
            return constructor.Invoke(new object[] { root });
        }

        private static void InvokeEnsureSaveDefaults(object save)
        {
            Type serviceType = GetRuntimeType("AL.Services.Local.LocalSaveGameService");
            MethodInfo method = serviceType.GetMethod(
                "EnsureSaveDefaults",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(null, new[] { save });
        }

        private static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"Expected method {methodName}.");
            return method.Invoke(target, args);
        }

        private static IList CreateRuntimeList(Type elementType)
        {
            Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(elementType);
            return (IList)Activator.CreateInstance(listType);
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp")
                ?.GetType(typeName);
            Assert.NotNull(type, $"Expected runtime type {typeName} in Assembly-CSharp.");
            return type;
        }

        private static object GetProperty(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(property, $"Expected property {name}.");
            return property.GetValue(target);
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Expected field {name}.");
            field.SetValue(target, value);
        }
    }
}
