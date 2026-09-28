using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using jeanf.EventSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Enforces the channel-hub rule (Documentation~/player-channels-hub.md):
    ///  - <see cref="PlayerChannelsSO"/> is the ONE asset naming the SO event channels the
    ///    player exchanges with a project;
    ///  - <see cref="PlayerEventBridge"/> is the ONLY script that references, raises or
    ///    subscribes to a channel; every other component talks over <see cref="PlayerEvents"/>;
    ///  - no packaged prefab wires a channel asset anywhere but on the bridge;
    ///  - every hub slot is actually forwarded by the bridge.
    /// A new channel field on a component fails here, before a prefab variant ever
    /// overrides it and a package update silently orphans that override.
    /// </summary>
    public class PlayerChannelsIsolationTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        // The one script allowed to hold / raise / subscribe channels.
        private static readonly Type[] AllowedComponents = { typeof(PlayerEventBridge) };


        // Runtime sources allowed to name channel types, relative to Runtime/scripts.
        private static readonly string[] SourceAllowlist =
        {
            "Events/PlayerChannelsSO.cs",   // the hub
            "Events/PlayerEventBridge.cs",  // the one forwarder
            "ActionManagement/ActionSO.cs", // project data: which channel an input action forwards to (raised by the bridge)
        };

        private static bool IsPackageAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return name.StartsWith("jeanf.universalplayer", StringComparison.Ordinal) && !name.Contains("tests")
                   || name == "xr.toolkit";
        }

        private static IEnumerable<Type> PackageMonoBehaviours() =>
            TypeCache.GetTypesDerivedFrom<MonoBehaviour>().Where(type => !type.IsAbstract && IsPackageAssembly(type.Assembly));

        private static IEnumerable<Type> PackageScriptableObjects() =>
            TypeCache.GetTypesDerivedFrom<ScriptableObject>().Where(type => !type.IsAbstract && IsPackageAssembly(type.Assembly));

        private static bool IsChannelType(Type type)
        {
            if (type == null) return false;
            if (type.IsArray) return IsChannelType(type.GetElementType());
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return IsChannelType(type.GetGenericArguments()[0]);
            return typeof(DescriptionBaseSO).IsAssignableFrom(type);
        }

        private static bool IsSerialized(FieldInfo field) =>
            !field.IsStatic
            && (field.IsPublic && field.GetCustomAttribute<NonSerializedAttribute>() == null
                || field.GetCustomAttribute<SerializeField>() != null
                || field.GetCustomAttribute<SerializeReference>() != null);

        [Test]
        public void PackageMonoBehaviours_NeverHoldEventChannelFields()
        {
            var violations = new List<string>();
            foreach (var type in PackageMonoBehaviours())
            {
                if (AllowedComponents.Contains(type)) continue;
                for (var t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
                    foreach (var field in t.GetFields(Instance))
                        if (IsSerialized(field) && IsChannelType(field.FieldType))
                            violations.Add($"{type.Name}.{field.Name} ({field.FieldType.Name})");
            }

            Assert.That(violations, Is.Empty,
                "Component(s) reference SO event channels directly:\n  " + string.Join("\n  ", violations) + "\n\n" +
                "Only PlayerEventBridge may hold a channel. Raise/subscribe on PlayerEvents instead; if the project " +
                "needs the signal, add a slot on PlayerChannelsSO and forward it in the bridge.");
        }

        [Test]
        public void PackageMonoBehaviours_NeverDeriveFromEventSystemComponents()
        {
            // The EventSystem listener/sender bases carry a channel field of their own.
            var eventSystem = typeof(DescriptionBaseSO).Assembly;
            var violations = PackageMonoBehaviours()
                .Where(type => !AllowedComponents.Contains(type))
                .Where(type => EnumerateBases(type).Any(b => b.Assembly == eventSystem))
                .Select(type => $"{type.Name} : {type.BaseType?.Name}")
                .ToList();

            Assert.That(violations, Is.Empty,
                "Component(s) inherit an EventSystem listener/sender (and its channel field):\n  " +
                string.Join("\n  ", violations) + "\n\nSubscribe on PlayerEvents instead (TeleportOnEvent is the example).");
        }

        private static IEnumerable<Type> EnumerateBases(Type type)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType) yield return t;
        }

        [Test]
        public void PackagedPrefabs_OnlyTheBridgeReferencesChannelAssets()
        {
            var violations = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PackagePaths.Runtime }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue; // missing script — PackageIntegrityTests report it
                    var type = component.GetType();
                    if (AllowedComponents.Contains(type)) continue;
                    foreach (var property in ChannelReferences(component))
                        violations.Add($"{Path.GetFileName(path)} → '{HierarchyPath(component.transform)}' {type.Name}.{property.name} = '{property.objectReferenceValue.name}'");
                }
            }

            Assert.That(violations, Is.Empty,
                "Packaged prefab(s) wire channel assets outside the bridge:\n  " + string.Join("\n  ", violations) + "\n\n" +
                "Remove the component/field; the signal must travel over PlayerEvents and, for the project, a PlayerChannelsSO slot.");
        }

        [Test]
        public void PackagedScriptableObjects_OnlyPlayerChannelsReferencesChannelAssets()
        {
            var violations = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { PackagePaths.Runtime }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null || asset is PlayerChannelsSO || asset is DescriptionBaseSO) continue;
                foreach (var property in ChannelReferences(asset))
                    violations.Add($"{path}: {asset.GetType().Name}.{property.name} = '{property.objectReferenceValue.name}'");
            }

            Assert.That(violations, Is.Empty,
                "Packaged asset(s) other than PlayerChannelsSO reference channel assets:\n  " + string.Join("\n  ", violations));
        }

        private static IEnumerable<SerializedProperty> ChannelReferences(UnityEngine.Object target)
        {
            var iterator = new SerializedObject(target).GetIterator();
            for (var enterChildren = true; iterator.Next(enterChildren); enterChildren = true)
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.name == "m_Script") continue;
                if (iterator.objectReferenceValue is DescriptionBaseSO) yield return iterator.Copy();
            }
        }

        [Test]
        public void RuntimeSources_MentionChannelTypesOnlyInTheEventsFolder()
        {
            var scripts = Path.Combine(PackagePaths.Runtime, "scripts").Replace('\\', '/');
            Assert.That(Directory.Exists(scripts), Is.True, $"'{scripts}' not found — did the package layout move?");

            var channelToken = new Regex(@"\b\w*EventChannelSO\b|\bDescriptionBaseSO\b|\b\w+EventListener\b|\b\w+EventSender\b|\.OnEventRaised\s*[+\-]=|\.RaiseEvent\(", RegexOptions.Compiled);
            var comments = new Regex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
            var violations = new List<string>();
            foreach (var file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                var relative = file.Replace('\\', '/').Substring(scripts.Length + 1);
                if (SourceAllowlist.Contains(relative)) continue;
                var code = comments.Replace(File.ReadAllText(file), string.Empty);
                var lines = code.Split('\n');
                for (var i = 0; i < lines.Length; i++)
                    if (channelToken.IsMatch(lines[i]))
                        violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
            }

            Assert.That(violations, Is.Empty,
                "Runtime source(s) outside Runtime/scripts/Events touch SO event channels:\n  " + string.Join("\n  ", violations) + "\n\n" +
                "The bridge is the single wiring point: route the signal through PlayerEvents (+ a PlayerChannelsSO slot when a project needs it).");
        }

        [Test]
        public void EveryHubSlot_IsForwardedByTheBridge()
        {
            var bridgeSource = Path.Combine(PackagePaths.Runtime, "scripts/Events/PlayerEventBridge.cs");
            Assert.That(File.Exists(bridgeSource), Is.True, $"'{bridgeSource}' not found — update this test if the bridge moved.");
            var source = File.ReadAllText(bridgeSource);

            var slots = typeof(PlayerChannelsSO).GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => IsChannelType(field.FieldType))
                .Select(field => field.Name)
                .ToList();
            Assert.That(slots, Is.Not.Empty, "PlayerChannelsSO has no channel slot — update this test alongside the refactor.");

            var unforwarded = slots.Where(slot => !Regex.IsMatch(source, $@"channels\.{Regex.Escape(slot)}\b")).ToList();
            Assert.That(unforwarded, Is.Empty,
                "PlayerChannelsSO slot(s) the bridge never touches: " + string.Join(", ", unforwarded) +
                " — a slot nobody forwards is silent in every direction. Wire it in PlayerEventBridge (outbound, inbound or both).");
        }

        [Test]
        public void PlayerChannelsSO_IsTheOnlyChannelHolderType()
        {
            // No second "hub" may appear: a ScriptableObject type of this package holding
            // channel fields other than PlayerChannelsSO (and ActionSO's single forward slot).
            var violations = new List<string>();
            foreach (var type in PackageScriptableObjects())
            {
                if (type == typeof(PlayerChannelsSO) || type == typeof(ActionSO)) continue;
                for (var t = type; t != null && t != typeof(ScriptableObject); t = t.BaseType)
                    foreach (var field in t.GetFields(Instance))
                        if (IsSerialized(field) && IsChannelType(field.FieldType))
                            violations.Add($"{type.Name}.{field.Name} ({field.FieldType.Name})");
            }

            Assert.That(violations, Is.Empty,
                "ScriptableObject(s) other than PlayerChannelsSO hold channel fields:\n  " + string.Join("\n  ", violations));
        }

        [Test]
        public void PackagedChannelAssets_AllLiveInRuntimeChannels()
        {
            // One folder for every channel asset the package ships, named after its hub
            // slot — no more channel files scattered next to the scripts that used to
            // hold them, and no slot pointing at another package's samples or at a
            // dev-project asset (consumers would get a missing reference).
            var channelsFolder = PackagePaths.Runtime + "/Channels/";
            var elsewhere = new List<string>();
            var found = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { PackagePaths.Runtime }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is DescriptionBaseSO)) continue;
                found++;
                if (!path.StartsWith(channelsFolder) || path.Substring(channelsFolder.Length).Contains("/"))
                    elsewhere.Add(path);
            }

            Assert.That(found, Is.GreaterThan(0), $"No channel asset found under '{PackagePaths.Runtime}' — did the package layout move?");
            Assert.That(elsewhere, Is.Empty,
                "Channel asset(s) outside Runtime/Channels/:\n  " + string.Join("\n  ", elsewhere) + "\n\n" +
                "Move them (keep the .meta so the guid survives) and name them after their PlayerChannelsSO slot.");

            // Every slot of the packaged hub names an asset the package owns, in that folder.
            var hub = AssetDatabase.LoadAssetAtPath<PlayerChannelsSO>(PackagePaths.Runtime + "/scripts/Events/UniversalPlayerChannels.asset");
            Assert.That(hub, Is.Not.Null, "UniversalPlayerChannels.asset not found under Runtime/scripts/Events/ — update this test if it moved.");
            var offFolder = ChannelReferences(hub)
                .Select(property => (slot: property.name, path: AssetDatabase.GetAssetPath(property.objectReferenceValue)))
                .Where(entry => !entry.path.StartsWith(channelsFolder))
                .Select(entry => $"{entry.slot} -> {entry.path}")
                .ToList();
            Assert.That(offFolder, Is.Empty,
                "Packaged hub slot(s) pointing outside Runtime/Channels/ (another package's samples, a project asset):\n  " +
                string.Join("\n  ", offFolder) + "\n\nCreate the channel asset in Runtime/Channels/ and point the slot at it.");
        }

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
                path = $"{parent.name}/{path}";
            return path;
        }
    }
}
