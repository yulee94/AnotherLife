using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode.World
{
    public sealed class SceneContentDeliveryConfigurationTests
    {
        [Test]
        public void ApprovedHybridLocalAddressablesConfigurationIsExact()
        {
            Type configurator = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "AL.Editor.World.SceneContentDeliveryConfigurator",
                    throwOnError: false))
                .FirstOrDefault(type => type != null);
            Assert.That(configurator, Is.Not.Null, "Scene content delivery configurator is missing.");

            MethodInfo validate = configurator.GetMethod(
                "ValidateCurrentConfiguration",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(validate, Is.Not.Null);
            object report = validate.Invoke(null, null);
            Type reportType = report.GetType();

            Assert.That((bool)reportType.GetProperty("IsValid").GetValue(report), Is.True,
                reportType.GetMethod("Summarize").Invoke(report, null).ToString());
            Assert.That((int)reportType.GetProperty("GroupCount").GetValue(report), Is.EqualTo(11));
            Assert.That((int)reportType.GetProperty("EntryCount").GetValue(report), Is.EqualTo(78));
            Assert.That((int)reportType.GetProperty("UnexpectedEntryCount").GetValue(report), Is.Zero);
            Assert.That((bool)reportType.GetProperty("RemoteCatalogsEnabled").GetValue(report), Is.False);
            Assert.That((bool)reportType.GetProperty("AllGroupsUseLocalPaths").GetValue(report), Is.True);
        }
    }
}
