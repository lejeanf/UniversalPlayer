using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace jeanf.universalplayer
{
    public static class InputActionAssetChecks
    {
        public const string ProjectCheck = "Input actions";
        public const string SceneCheck = "Scene: input action wiring";
        public const string PackageAssetGuid = "110d9016d6486d84697b9d7a66a57d45";
        private const string LogPrefix = "[UniversalPlayer.Fix]";

        public sealed class Report
        {
            public readonly List<string> MissingSchemes = new List<string>();
            public readonly List<string> MissingMaps = new List<string>();
            public readonly List<string> MissingActions = new List<string>();
            public readonly List<string> MissingBindings = new List<string>();

            public bool IsComplete =>
                MissingSchemes.Count == 0 && MissingMaps.Count == 0 && MissingActions.Count == 0 && MissingBindings.Count == 0;

            public bool LacksActions => MissingMaps.Count > 0 || MissingActions.Count > 0;

            public IEnumerable<string> Summary()
            {
                if (MissingMaps.Count > 0) yield return $"{MissingMaps.Count} action map(s): {Preview(MissingMaps)}";
                if (MissingActions.Count > 0) yield return $"{MissingActions.Count} action(s): {Preview(MissingActions)}";
                if (MissingBindings.Count > 0) yield return $"{MissingBindings.Count} binding(s): {Preview(MissingBindings)}";
                if (MissingSchemes.Count > 0) yield return $"{MissingSchemes.Count} control scheme(s): {Preview(MissingSchemes)}";
            }
        }

        public static InputActionAsset ReferenceAsset()
        {
            var path = AssetDatabase.GUIDToAssetPath(PackageAssetGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }

        public static Report Compare(InputActionAsset local, InputActionAsset reference)
        {
            var report = new Report();
            if (local == null || reference == null) return report;

            foreach (var scheme in reference.controlSchemes)
                if (local.controlSchemes.All(s => s.name != scheme.name)) report.MissingSchemes.Add(scheme.name);

            foreach (var referenceMap in reference.actionMaps)
            {
                var localMap = local.FindActionMap(referenceMap.name);
                if (localMap == null)
                {
                    report.MissingMaps.Add(referenceMap.name);
                    continue;
                }

                foreach (var referenceAction in referenceMap.actions)
                {
                    var localAction = localMap.FindAction(referenceAction.name);
                    if (localAction == null)
                    {
                        report.MissingActions.Add($"{referenceMap.name}/{referenceAction.name}");
                        continue;
                    }
                    foreach (var missing in MissingBindings(localAction, referenceAction))
                        report.MissingBindings.Add($"{referenceMap.name}/{referenceAction.name}: {missing}");
                }
            }
            return report;
        }

        private static IEnumerable<string> MissingBindings(InputAction local, InputAction reference)
        {
            var referenceBindings = reference.bindings;
            for (var i = 0; i < referenceBindings.Count; i++)
            {
                var binding = referenceBindings[i];
                if (binding.isPartOfComposite) continue;
                if (binding.isComposite)
                {
                    var localComposite = FindComposite(local, binding);
                    if (localComposite < 0)
                    {
                        yield return $"{binding.name} ({binding.path})";
                        continue;
                    }
                    for (var p = i + 1; p < referenceBindings.Count && referenceBindings[p].isPartOfComposite; p++)
                        if (!CompositeHasPart(local, localComposite, referenceBindings[p]))
                            yield return $"{binding.name}/{referenceBindings[p].name} ({referenceBindings[p].path})";
                    continue;
                }
                if (!local.bindings.Any(b => !b.isComposite && !b.isPartOfComposite && SamePath(b.path, binding.path)))
                    yield return binding.path;
            }
        }

        private static int FindComposite(InputAction action, InputBinding composite)
        {
            var bindings = action.bindings;
            for (var i = 0; i < bindings.Count; i++)
                if (bindings[i].isComposite && SamePath(bindings[i].path, composite.path)
                    && string.Equals(bindings[i].name, composite.name, System.StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static bool CompositeHasPart(InputAction action, int compositeIndex, InputBinding part)
        {
            var bindings = action.bindings;
            for (var i = compositeIndex + 1; i < bindings.Count && bindings[i].isPartOfComposite; i++)
                if (string.Equals(bindings[i].name, part.name, System.StringComparison.OrdinalIgnoreCase)
                    && SamePath(bindings[i].path, part.path))
                    return true;
            return false;
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(a ?? "", b ?? "", System.StringComparison.OrdinalIgnoreCase);

        public static int AddMissing(InputActionAsset local, InputActionAsset reference)
        {
            if (local == null || reference == null) return 0;
            var added = 0;

            foreach (var scheme in reference.controlSchemes)
            {
                if (local.controlSchemes.Any(s => s.name == scheme.name)) continue;
                var syntax = local.AddControlScheme(scheme.name);
                foreach (var requirement in scheme.deviceRequirements)
                {
                    if (requirement.isOptional) syntax.WithOptionalDevice(requirement.controlPath);
                    else syntax.WithRequiredDevice(requirement.controlPath);
                }
                added++;
            }

            foreach (var referenceMap in reference.actionMaps)
            {
                var localMap = local.FindActionMap(referenceMap.name);
                if (localMap == null)
                {
                    localMap = local.AddActionMap(referenceMap.name);
                    added++;
                }

                foreach (var referenceAction in referenceMap.actions)
                {
                    var localAction = localMap.FindAction(referenceAction.name);
                    if (localAction == null)
                    {
                        localAction = localMap.AddAction(referenceAction.name, referenceAction.type,
                            interactions: referenceAction.interactions, processors: referenceAction.processors,
                            expectedControlLayout: referenceAction.expectedControlType);
                        added++;
                    }
                    added += AddMissingBindings(localAction, referenceAction);
                }
            }
            return added;
        }

        private static int AddMissingBindings(InputAction local, InputAction reference)
        {
            var added = 0;
            var referenceBindings = reference.bindings;
            for (var i = 0; i < referenceBindings.Count; i++)
            {
                var binding = referenceBindings[i];
                if (binding.isPartOfComposite) continue;
                if (binding.isComposite)
                {
                    if (FindComposite(local, binding) >= 0) continue;
                    var composite = local.AddCompositeBinding(binding.path, binding.interactions, binding.processors);
                    for (var p = i + 1; p < referenceBindings.Count && referenceBindings[p].isPartOfComposite; p++)
                        composite.With(referenceBindings[p].name, referenceBindings[p].path, referenceBindings[p].groups, referenceBindings[p].processors);
                    added++;
                    continue;
                }
                if (local.bindings.Any(b => !b.isComposite && !b.isPartOfComposite && SamePath(b.path, binding.path))) continue;
                local.AddBinding(binding.path, binding.interactions, binding.processors, binding.groups);
                added++;
            }
            return added;
        }

        public static bool Save(InputActionAsset asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".inputactions") || !path.StartsWith("Assets/")) return false;
            File.WriteAllText(path, asset.ToJson());
            AssetDatabase.ImportAsset(path);
            return true;
        }

        public static InputActionAsset PlayerActionAsset(GameObject playerRoot)
        {
            var playerInput = playerRoot != null ? playerRoot.GetComponentInChildren<PlayerInput>(true) : null;
            return playerInput != null ? playerInput.actions : null;
        }

        public static List<InputActionAsset> ProjectPlayerActionAssets()
        {
            var assets = new List<InputActionAsset>();
            void Add(InputActionAsset asset)
            {
                if (asset != null && !assets.Contains(asset)) assets.Add(asset);
            }

            Add(PlayerActionAsset(ProjectSetupChecks.ScenePlayerRoot()));

            var packageRoot = ProjectSetupChecks.PackageRoot();
            var playerPrefab = packageRoot != null ? AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSetupChecks.PlayerPrefabPath()) : null;
            if (playerPrefab != null)
                foreach (var variant in ProjectSetupChecks.FindPlayerVariants(playerPrefab, packageRoot))
                    Add(PlayerActionAsset(variant));
            return assets;
        }

        public static SetupValidator.CheckResult CheckProjectInputActions()
        {
            var reference = ReferenceAsset();
            if (reference == null)
                return new SetupValidator.CheckResult(ProjectCheck, SetupValidator.Severity.Warning,
                    "The package input action asset (UniversalPlayer_InputActions) could not be located — is the Universal Player package installed correctly?",
                    "Check Runtime/InputActions/UniversalPlayer_InputActions.inputactions exists in the package.");

            var locals = ProjectPlayerActionAssets().Where(asset => asset != reference).ToList();
            if (locals.Count == 0)
                return new SetupValidator.CheckResult(ProjectCheck, SetupValidator.Severity.Pass,
                    "The Player runs on the package input action asset.");

            var problems = new List<string>();
            var fail = false;
            foreach (var local in locals)
            {
                var report = Compare(local, reference);
                if (report.IsComplete) continue;
                fail |= report.LacksActions;
                problems.Add($"'{AssetDatabase.GetAssetPath(local)}' lacks {string.Join("; ", report.Summary())}");
            }

            if (problems.Count == 0)
                return new SetupValidator.CheckResult(ProjectCheck, SetupValidator.Severity.Pass,
                    $"Project action asset(s) contain every map, action, binding and control scheme of the package asset: {string.Join(", ", locals.Select(a => a.name))}.");

            return new SetupValidator.CheckResult(ProjectCheck, fail ? SetupValidator.Severity.Fail : SetupValidator.Severity.Warning,
                string.Join(" ", problems) + " — actions the package prefab expects (hand poses, tracked pose drivers, draw item, teleport) stay dead or unbound.",
                "Press Fix (or Tools/Jeanf/UniversalPlayer/Repair Input Actions): the missing maps, actions, bindings and control schemes are copied from the package asset into the project asset.");
        }

        [MenuItem("Tools/Jeanf/UniversalPlayer/Repair Input Actions")]
        public static void RepairProjectInputActions()
        {
            var reference = ReferenceAsset();
            if (reference == null)
            {
                Debug.LogWarning($"{LogPrefix} package input action asset not found — nothing repaired.");
                return;
            }
            foreach (var local in ProjectPlayerActionAssets().Where(asset => asset != reference))
            {
                var added = AddMissing(local, reference);
                if (added == 0)
                {
                    Debug.Log($"{LogPrefix} '{local.name}' already complete.");
                    continue;
                }
                var saved = Save(local);
                Debug.Log($"{LogPrefix} '{AssetDatabase.GetAssetPath(local)}': {added} element(s) added from the package asset{(saved ? "" : " (asset is not a writable .inputactions file under Assets — changes not saved)")}.", local);
            }
        }

        public static HashSet<InputActionAsset> ReferencedActionAssets(GameObject playerRoot)
        {
            var assets = new HashSet<InputActionAsset>();
            if (playerRoot == null) return assets;
            foreach (var component in playerRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                var enterChildren = true;
                while (property.NextVisible(enterChildren))
                {
                    enterChildren = property.propertyType != SerializedPropertyType.String;
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (property.objectReferenceValue is InputActionReference reference && reference.asset != null)
                        assets.Add(reference.asset);
                }
            }
            return assets;
        }

        public static List<InputActionAsset> EnabledActionAssets(InputActionManager manager)
        {
            var assets = new List<InputActionAsset>();
            if (manager == null) return assets;
            var list = new SerializedObject(manager).FindProperty("m_ActionAssets");
            if (list == null) return assets;
            for (var i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is InputActionAsset asset && !assets.Contains(asset))
                    assets.Add(asset);
            return assets;
        }

        public static List<InputActionAsset> UnenabledReferencedAssets(GameObject playerRoot, out InputActionManager manager)
        {
            manager = playerRoot != null ? playerRoot.GetComponentInChildren<InputActionManager>(true) : null;
            var enabled = EnabledActionAssets(manager);
            return ReferencedActionAssets(playerRoot).Where(asset => !enabled.Contains(asset)).OrderBy(a => a.name).ToList();
        }

        public static SetupValidator.CheckResult CheckInputActionWiring(GameObject playerRoot)
        {
            var missing = UnenabledReferencedAssets(playerRoot, out var manager);
            if (manager == null)
                return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Fail,
                    "No InputActionManager on the player — the XRI maps (hand poses, tracked pose drivers, teleport, UI press) are never enabled; PlayerInput only enables its default map.",
                    "It ships on the Player prefab under Settings/Input; re-add it on your variant and list every input action asset the rig references.");

            if (missing.Count == 0)
                return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Pass,
                    $"Every input action asset referenced on the player is enabled by its InputActionManager ({string.Join(", ", EnabledActionAssets(manager).Select(a => a.name))}).");

            return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Fail,
                $"Input action asset(s) referenced on the player but NOT enabled by its InputActionManager: {string.Join(", ", missing.Select(a => $"'{a.name}'"))} — " +
                "their actions never fire: tracked pose drivers stay still (hands do not move), hand poses, draw item and teleport stay dead while PlayerInput's own map still works.",
                "Press Fix (or Tools/Jeanf/UniversalPlayer/Wire Input Action Assets): the missing asset(s) are added to the InputActionManager's Action Assets on the scene player.");
        }

        [MenuItem("Tools/Jeanf/UniversalPlayer/Wire Input Action Assets")]
        public static void WireSceneInputActionAssets() => WireInputActionAssets(ProjectSetupChecks.ScenePlayerRoot());

        public static int WireInputActionAssets(GameObject playerRoot)
        {
            var missing = UnenabledReferencedAssets(playerRoot, out var manager);
            if (manager == null)
            {
                Debug.LogWarning($"{LogPrefix} no InputActionManager on the player — nothing wired.");
                return 0;
            }
            if (missing.Count == 0)
            {
                Debug.Log($"{LogPrefix} InputActionManager already enables every referenced input action asset.");
                return 0;
            }

            var serialized = new SerializedObject(manager);
            var list = serialized.FindProperty("m_ActionAssets");
            foreach (var asset in missing)
            {
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = asset;
            }
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(manager);
            if (manager.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Debug.Log($"{LogPrefix} added {missing.Count} input action asset(s) to '{manager.name}': {string.Join(", ", missing.Select(a => a.name))}.", manager);
            return missing.Count;
        }

        private static string Preview(List<string> items) =>
            string.Join(", ", items.Take(5)) + (items.Count > 5 ? ", ..." : "");
    }
}
