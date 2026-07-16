using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void NarrativeCompatibilityFieldsCanMutateAndReloadAfterNormalization()
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
                SetField(currentSave, "Reputation", null);
                SetField(currentSave, "FactionReputations", null);
                SetField(currentSave, "LordPersona", null);
                InvokeEnsureSaveDefaults(currentSave);

                object reputationService = CreateRuntimeService(
                    "AL.Services.Local.ReputationService",
                    "AL.Core.Interfaces.ISaveGameService",
                    service);
                object factionService = CreateRuntimeService(
                    "AL.Services.Local.FactionService",
                    "AL.Core.Interfaces.ISaveGameService",
                    service);
                object personaService = CreateRuntimeService(
                    "AL.Services.Local.PersonaService",
                    "AL.Core.Interfaces.ISaveGameService",
                    service);

                Type personaTraitType = GetRuntimeType("AL.Core.Interfaces.PersonaTrait");
                object diplomat = Enum.Parse(personaTraitType, "Diplomat");

                Invoke(reputationService, "ChangeAffinity", "ADVISOR_VALERIUS", 5.5f);
                Invoke(factionService, "AdjustReputation", "FACTION_VEIL_WATCH", 12);
                Invoke(personaService, "AdjustTrait", diplomat, 3);

                object reloadedService = CreateSaveService(root);
                Invoke(reloadedService, "Load");
                object reloadedReputation = CreateRuntimeService(
                    "AL.Services.Local.ReputationService",
                    "AL.Core.Interfaces.ISaveGameService",
                    reloadedService);
                object reloadedFaction = CreateRuntimeService(
                    "AL.Services.Local.FactionService",
                    "AL.Core.Interfaces.ISaveGameService",
                    reloadedService);
                object reloadedPersona = CreateRuntimeService(
                    "AL.Services.Local.PersonaService",
                    "AL.Core.Interfaces.ISaveGameService",
                    reloadedService);

                Assert.AreEqual(5.5f, Invoke(reloadedReputation, "GetAffinity", "ADVISOR_VALERIUS"));
                Assert.AreEqual(12, Invoke(reloadedFaction, "GetReputation", "FACTION_VEIL_WATCH"));
                Assert.AreEqual(3, Invoke(reloadedPersona, "GetTraitValue", diplomat));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
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

        [Test]
        public void MissingPrimaryRecoversValidBackup()
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
                SetField(currentSave, "CurrentChapterId", "C1_BACKUP_ONLY");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                File.Copy(primaryPath, backupPath, true);
                File.Delete(primaryPath);

                object recoveredService = CreateSaveService(root);
                Invoke(recoveredService, "Load");

                Assert.AreEqual("RecoveredFromBackup", GetProperty(recoveredService, "LastLoadStatus").ToString());
                Assert.That((string)GetProperty(recoveredService, "LastLoadMessage"), Does.Contain("AL-SAVE-RECOVERED-BACKUP"));
                object recoveredSave = GetProperty(recoveredService, "CurrentSave");
                Assert.AreEqual("C1_BACKUP_ONLY", GetField(recoveredSave, "CurrentChapterId"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void BothInvalidSaveFilesAreQuarantinedAndReplaced()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                File.WriteAllText(primaryPath, "{ invalid primary");
                File.WriteAllText(backupPath, "{ invalid backup");

                object service = CreateSaveService(root);
                LogAssert.Expect(
                    LogType.Error,
                    "AL-SAVE-NEW-AFTER-CORRUPTION: No valid save or backup could be recovered. A new profile was created and corrupt files were quarantined where possible.");
                Invoke(service, "Load");

                Assert.AreEqual("CreatedNewAfterUnrecoverableCorruption", GetProperty(service, "LastLoadStatus").ToString());
                Assert.That((string)GetProperty(service, "LastLoadMessage"), Does.Contain("AL-SAVE-NEW-AFTER-CORRUPTION"));
                Assert.True(File.Exists(primaryPath));
                Assert.True(File.Exists(backupPath));
                Assert.AreEqual(1, Directory.GetFiles(root, "save.json.corrupt-*").Length);
                Assert.AreEqual(1, Directory.GetFiles(root, "save.backup.json.corrupt-*").Length);
                Assert.That(File.ReadAllText(primaryPath), Does.Contain("\"CurrentChapterId\": \"C1\""));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void StaleTempAndPreviousArtifactsAreCleanedBeforeLoad()
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
                SetField(currentSave, "CurrentChapterId", "C1_PRIMARY");
                Invoke(service, "Save");

                string tempPath = Path.Combine(root, "save.tmp.json");
                string previousPath = Path.Combine(root, "save.previous.json");
                File.WriteAllText(tempPath, "{ stale temp");
                File.WriteAllText(previousPath, "{ stale previous");

                object reloadedService = CreateSaveService(root);
                Invoke(reloadedService, "Load");

                Assert.AreEqual("LoadedPrimary", GetProperty(reloadedService, "LastLoadStatus").ToString());
                Assert.False(File.Exists(tempPath));
                Assert.False(File.Exists(previousPath));
                object reloadedSave = GetProperty(reloadedService, "CurrentSave");
                Assert.AreEqual("C1_PRIMARY", GetField(reloadedSave, "CurrentChapterId"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void InvalidPrimaryNeverRotatesIntoBackupDuringSave()
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
                SetField(currentSave, "CurrentChapterId", "C1_BACKUP_SAFE");
                Invoke(service, "Save");

                string primaryPath = Path.Combine(root, "save.json");
                string backupPath = Path.Combine(root, "save.backup.json");
                File.Copy(primaryPath, backupPath, true);
                File.WriteAllText(primaryPath, "{ corrupt primary must not become backup");

                currentSave = GetProperty(service, "CurrentSave");
                SetField(currentSave, "CurrentChapterId", "C1_VALIDATED_CANDIDATE");
                Invoke(service, "Save");

                Assert.AreEqual("SavedPrimary", GetProperty(service, "LastSaveStatus").ToString());
                Assert.That(File.ReadAllText(backupPath), Does.Not.Contain("corrupt primary"));
                Assert.That(File.ReadAllText(primaryPath), Does.Contain("C1_VALIDATED_CANDIDATE"));
                Assert.AreEqual(1, Directory.GetFiles(root, "save.json.corrupt-*").Length);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void QuarantineRetentionIsBoundedPerSourceFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                for (int i = 0; i < 5; i++)
                {
                    File.WriteAllText(Path.Combine(root, $"save.json.corrupt-2026010100000{i}-{Guid.NewGuid():N}"), "old");
                }

                string primaryPath = Path.Combine(root, "save.json");
                File.WriteAllText(primaryPath, "{ invalid primary");

                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                Assert.LessOrEqual(Directory.GetFiles(root, "save.json.corrupt-*").Length, 3);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void DeleteSaveRemovesPrimaryBackupTransientAndQuarantineArtifacts()
        {
            string root = Path.Combine(Path.GetTempPath(), "AnotherLife-SaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                object service = CreateSaveService(root);
                Type realmType = GetRuntimeType("AL.Core.RealmId");
                object noRealm = Enum.Parse(realmType, "None");
                Invoke(service, "CreateNewSave", noRealm);

                string[] extraFiles =
                {
                    "save.tmp.json",
                    "save.previous.json",
                    $"save.json.corrupt-20260101000000-{Guid.NewGuid():N}",
                    $"save.backup.json.corrupt-20260101000000-{Guid.NewGuid():N}"
                };

                foreach (string fileName in extraFiles)
                {
                    File.WriteAllText(Path.Combine(root, fileName), "artifact");
                }

                Invoke(service, "DeleteSave");

                Assert.False((bool)Invoke(service, "HasSave"));
                Assert.Null(GetProperty(service, "CurrentSave"));
                Assert.AreEqual("None", GetProperty(service, "LastLoadStatus").ToString());
                Assert.AreEqual("None", GetProperty(service, "LastSaveStatus").ToString());
                Assert.IsEmpty(Directory.GetFiles(root, "save*.json*"));
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

        private static object CreateRuntimeService(string serviceTypeName, string constructorArgumentTypeName, object argument)
        {
            Type serviceType = GetRuntimeType(serviceTypeName);
            Type constructorArgumentType = GetRuntimeType(constructorArgumentTypeName);
            ConstructorInfo constructor = serviceType.GetConstructor(new[] { constructorArgumentType });
            Assert.NotNull(constructor, $"Expected constructor for {serviceTypeName}.");
            return constructor.Invoke(new[] { argument });
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
