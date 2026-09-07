using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Deliberately bounded runner for this fixture's parameterless [Test] methods only.
// It executes real NUnit assertions without loading Unity or claiming Editor integration.
public static class StrongholdTestRunner
{
    public static int Main()
    {
        var type = typeof(AL.Tests.EditMode.Strongholds.StrongholdPlannerTests);
        var methods = type.GetMethods().Where(method => method.IsDefined(typeof(TestAttribute), false)).ToArray();
        if (methods.Length == 0 || type.GetMethods().Any(method => method.GetCustomAttributes(false)
            .Any(attribute => attribute is TestCaseAttribute || attribute is TestCaseSourceAttribute ||
                attribute is SetUpAttribute || attribute is TearDownAttribute ||
                attribute is OneTimeSetUpAttribute || attribute is OneTimeTearDownAttribute)))
        {
            Console.WriteLine("Unsupported fixture lifecycle or empty fixture; use NUnitLite/Unity instead.");
            return 2;
        }
        int failed = 0;
        foreach (var method in methods.OrderBy(method => method.Name, StringComparer.Ordinal))
        {
            try
            {
                if (method.GetParameters().Length != 0 || method.ReturnType != typeof(void))
                    throw new InvalidOperationException("Runner supports only synchronous parameterless void tests.");
                method.Invoke(Activator.CreateInstance(type), null);
                Console.WriteLine("PASS " + method.Name);
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine("FAIL " + method.Name + ": " + (error.InnerException ?? error));
            }
        }
        Console.WriteLine($"Tests={methods.Length}; Passed={methods.Length - failed}; Failed={failed}");
        return failed == 0 ? 0 : 1;
    }
}
