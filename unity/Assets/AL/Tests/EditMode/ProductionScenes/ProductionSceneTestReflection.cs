using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace AL.Tests.EditMode.ProductionScenes
{
    /// <summary>
    /// Reflection glue for the ProductionScenes EditMode tests. The AL.EditMode.Tests assembly cannot
    /// reference the predefined Assembly-CSharp / Assembly-CSharp-Editor, so (as with the existing
    /// Bootloader integrity tests) production and editor-tool types are reached by reflection.
    /// </summary>
    internal static class ProductionSceneTestReflection
    {
        internal const string DescriptorType = "AL.Core.Scenes.ProductionSceneDescriptor";
        internal const string MarkerType = "AL.Core.Scenes.SceneStartupMarker";
        internal const string GeneratorType = "AL.EditorTools.ALVerticalSliceSceneGenerator";
        internal const string ValidatorType = "AL.EditorTools.ProductionSceneValidator";
        internal const string BuildSettingsValidatorType = "AL.EditorTools.ProductionBuildSettingsValidator";
        internal const string BuildSceneEntryType = "AL.EditorTools.ProductionBuildSceneEntry";
        internal const string PlayerBuilderType = "AL.EditorTools.ProductionPlayerBuilder";
        internal const string PlayerLaunchValidatorType = "AL.EditorTools.ProductionPlayerLaunchLogValidator";
        internal const string IosBuilderType = "AL.EditorTools.ProductionIosBuilder";

        internal static Type Runtime(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail($"Expected type {fullName} in a loaded assembly.");
            return null;
        }

        internal static object StaticProperty(string typeFullName, string propertyName)
        {
            PropertyInfo property = Runtime(typeFullName)
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(property, $"{typeFullName}.{propertyName} static property not found.");
            return property.GetValue(null);
        }

        internal static object StaticField(string typeFullName, string fieldName)
        {
            FieldInfo field = Runtime(typeFullName)
                .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, $"{typeFullName}.{fieldName} static field not found.");
            return field.GetValue(null);
        }

        internal static void SetStaticField(string typeFullName, string fieldName, object value)
        {
            FieldInfo field = Runtime(typeFullName)
                .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field, $"{typeFullName}.{fieldName} static field not found.");
            field.SetValue(null, value);
        }

        internal static object StaticMethod(string typeFullName, string methodName, params object[] args)
        {
            Type type = Runtime(typeFullName);
            MethodInfo method = type
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"{typeFullName}.{methodName}({args.Length} args) not found.");
            return method.Invoke(null, args);
        }

        internal static object Prop(object target, string propertyName)
        {
            Assert.NotNull(target, $"Cannot read property {propertyName} from null.");
            PropertyInfo property = target.GetType()
                .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(property, $"{target.GetType().Name}.{propertyName} property not found.");
            return property.GetValue(target);
        }

        internal static object Field(object target, string fieldName)
        {
            Assert.NotNull(target, $"Cannot read field {fieldName} from null.");
            FieldInfo field = target.GetType()
                .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            return field.GetValue(target);
        }

        internal static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType()
                .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        internal static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Length == args.Length);
            Assert.NotNull(method, $"{target.GetType().Name}.{methodName}({args.Length} args) not found.");
            return method.Invoke(target, args);
        }

        internal static string PropString(object target, string propertyName)
        {
            return (Prop(target, propertyName) ?? string.Empty).ToString();
        }

        internal static bool PropBool(object target, string propertyName)
        {
            return (bool)Prop(target, propertyName);
        }

        internal static int PropInt(object target, string propertyName)
        {
            return (int)Prop(target, propertyName);
        }

        internal static IReadOnlyList<object> AsObjects(object enumerable)
        {
            var list = new List<object>();
            if (enumerable is IEnumerable items)
            {
                foreach (object item in items)
                {
                    list.Add(item);
                }
            }

            return list;
        }

        internal static IReadOnlyList<string> AsStrings(object enumerable)
        {
            return AsObjects(enumerable).Select(item => item?.ToString() ?? string.Empty).ToList();
        }

        // Descriptor accessors -------------------------------------------------

        internal static IReadOnlyList<object> DescriptorAll()
        {
            return AsObjects(StaticProperty(DescriptorType, "All"));
        }

        internal static IReadOnlyList<object> DescriptorProduction()
        {
            return AsObjects(StaticProperty(DescriptorType, "ProductionScenes"));
        }

        internal static IReadOnlyList<object> DescriptorShellFoundation()
        {
            return AsObjects(StaticProperty(DescriptorType, "ShellFoundationOrdered"));
        }

        internal static string SourceVersion()
        {
            return (string)StaticField(DescriptorType, "SourceVersion");
        }

        internal static object RecordById(string sceneId)
        {
            return DescriptorAll().FirstOrDefault(r => PropString(r, "SceneId") == sceneId);
        }

        internal static IReadOnlyList<object> Transitions(object record)
        {
            return AsObjects(Prop(record, "TransitionTargets"));
        }
    }
}
