using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using jeanf.propertyDrawer;
using jeanf.validationTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Guards the editor-validation contract the package builds on the propertyDrawer
    /// toolkit ([Validation] fields, RequiredIf gates, IValidatable components):
    ///  - every prefab shipped under Runtime/ is clean, so a freshly spawned player never
    ///    opens with an orange "needs setup" banner (a false alarm on a clean setup is
    ///    what trains people to ignore the real ones);
    ///  - [Validation] only sits on field types the scanner can judge (Unity object,
    ///    string, list) — on anything else it would never fire;
    ///  - every [Validation] on a [DrawIf] field carries a RequiredIf gate (the inspector applies
    ///    both attributes, the scanner only reads the gate);
    ///  - every RequiredIf gate names a real bool member (a typo makes the check always-on).
    /// </summary>
    public class ValidationCoverageTests
    {
        private const BindingFlags AllInstance =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static IEnumerable<Assembly> PackageAssemblies => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name;
                return name.StartsWith("jeanf.universalplayer", StringComparison.Ordinal) && !name.Contains("tests");
            });

        private static IEnumerable<(Type type, FieldInfo field, ValidationAttribute attribute)> ValidatedFields()
        {
            foreach (var assembly in PackageAssemblies)
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                foreach (var field in type.GetFields(AllInstance))
                {
                    var attribute = field.GetCustomAttribute<ValidationAttribute>(false);
                    if (attribute != null) yield return (type, field, attribute);
                }
            }
        }

        [Test]
        public void PackagedPrefabs_HaveNoValidationIssues()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PackagePaths.Runtime });
            Assert.That(guids, Is.Not.Empty, $"No prefab found under '{PackagePaths.Runtime}' — did the package layout move?");

            var issues = new List<ValidationIssue>();
            var report = new List<string>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue; // missing script — Unity flags those itself
                    // FadeMask.IsValid depends on the ACTIVE render pipeline (the packaged profile is
                    // URP; HDRP projects override it on their variant) — FadeProfileTests cover it.
                    if (component is FadeMask) continue;

                    issues.Clear();
                    ValidationScanner.GetIssues(component, issues);
                    foreach (var issue in issues)
                    {
                        var where = string.IsNullOrEmpty(issue.FieldName) ? string.Empty : $".{issue.FieldName}";
                        report.Add($"{path} › {HierarchyPath(component.transform)} : {component.GetType().Name}{where} — {issue.Message}");
                    }
                }
            }

            Assert.That(report, Is.Empty,
                "A prefab the package ships would open ORANGE (unassigned [Validation] field or IValidatable " +
                "reporting false). Either wire the reference in the prefab, or — if it is legitimately optional — " +
                "drop the [Validation] attribute / gate it with RequiredIf:\n  " + string.Join("\n  ", report));
        }

        [Test]
        public void ValidationAttribute_OnlySitsOnFieldTypesTheScannerCanJudge()
        {
            var offenders = ValidatedFields()
                .Where(v => !IsJudgeable(v.field.FieldType))
                .Select(v => $"{v.type.Name}.{v.field.Name} ({v.field.FieldType.Name})")
                .ToList();

            Assert.That(offenders, Is.Empty,
                "[Validation] only fires for Unity object references, strings and lists of those — " +
                "ValidationScanner.IsSet treats every other type as 'set', so the attribute would be dead. " +
                "Express the rule with IValidatable instead:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void EveryValidationOnADrawIfField_IsGatedWithRequiredIf()
        {
            var offenders = ValidatedFields()
                .Where(v => v.field.IsDefined(typeof(DrawIfAttribute), false) && string.IsNullOrEmpty(v.attribute.RequiredIf))
                .Select(v => $"{v.type.Name}.{v.field.Name}")
                .ToList();

            Assert.That(offenders, Is.Empty,
                "The inspector applies [DrawIf] and [Validation] together (propertyDrawer 1.5.0), but the scanner behind " +
                "the banner / hierarchy dot / console does not read [DrawIf] — an ungated [Validation] on a DrawIf field " +
                "reports a field the inspector hides. Gate it: RequiredIf = nameof(theSameBool) (or \"!bool\"):\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void EveryRequiredIfGate_NamesABoolMember()
        {
            var offenders = new List<string>();
            foreach (var (type, field, attribute) in ValidatedFields())
            {
                var condition = attribute.RequiredIf;
                if (string.IsNullOrEmpty(condition)) continue;
                if (condition[0] == '!') condition = condition.Substring(1);
                if (!HasBoolMember(type, condition))
                    offenders.Add($"{type.Name}.{field.Name} → RequiredIf = \"{attribute.RequiredIf}\"");
            }

            Assert.That(offenders, Is.Empty,
                "A RequiredIf gate must name a bool field or readable bool property on the same component; " +
                "an unresolvable name makes the field ALWAYS required (typo guard):\n  " + string.Join("\n  ", offenders));
        }

        private static bool IsJudgeable(Type fieldType)
            => typeof(UnityEngine.Object).IsAssignableFrom(fieldType)
               || fieldType == typeof(string)
               || typeof(IList).IsAssignableFrom(fieldType);

        private static bool HasBoolMember(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, AllInstance);
                if (field != null && field.FieldType == typeof(bool)) return true;
                var property = current.GetProperty(name, AllInstance);
                if (property != null && property.PropertyType == typeof(bool) && property.CanRead) return true;
            }
            return false;
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return path;
        }
    }
}
