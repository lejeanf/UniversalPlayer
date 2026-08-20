using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Hand checks for Tools/UniversalPlayer/ValidateSetup. Every VR hand failure used
    /// to surface only as a runtime warning — headset on, in Play Mode — or not at all:
    /// unassigned HandsDisplayer slots (hands never appear), a HandPoseManager with no
    /// interactor (grabs never pose the fingers), a pose saved against another rig
    /// (silently ignored by BaseHand.ApplyFingerRotations), a pose with no bone names
    /// (fingers scrambled by index mapping), an empty pose driver (fingers never close),
    /// or a rig whose bones carry no collider. All of it is visible at edit time, so it
    /// is checked here instead.
    /// </summary>
    public static class HandSetupChecks
    {
        /// <summary>All hand checks against the open scene's player. Kept UI-free so tests and CI can call it.</summary>
        public static List<SetupValidator.CheckResult> RunHandChecks()
        {
            var results = new List<SetupValidator.CheckResult>();

            var broadcaster = Object.FindFirstObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
            if (broadcaster == null)
            {
                results.Add(new SetupValidator.CheckResult("Scene: hands", SetupValidator.Severity.Warning,
                    "No player (BroadcastControlsStatus) in the open scene — hand checks skipped.",
                    "Open a scene containing the Player variant and validate again."));
                return results;
            }

            var playerRoot = broadcaster.transform.root.gameObject;

            // PreviewHands are the pose editor's authoring rigs, not runtime hands.
            var hands = playerRoot.GetComponentsInChildren<BaseHand>(true)
                .Where(hand => hand != null && !(hand is PreviewHand))
                .ToArray();

            results.Add(CheckHandsVisible(playerRoot));
            results.Add(CheckVisibilityAuthority(playerRoot));
            results.Add(CheckHandPoseManagers(playerRoot));
            results.Add(CheckFingerRoots(hands));

            var poses = CollectProjectPoses(playerRoot);
            results.Add(CheckPosesMatchRig(poses, hands));
            results.Add(CheckPoseBoneNames(poses));

            results.Add(CheckPoseDriver(playerRoot));
            results.Add(CheckPointingRay(playerRoot));
            results.Add(CheckHandColliders(playerRoot));
            return results;
        }

        // 1. HandsDisplayer is the ONLY thing that shows the hands (it toggles the two
        // referenced objects on every control-scheme change). An empty slot is a silent
        // `?.SetActive` no-op: that hand simply never appears in VR.
        private static SetupValidator.CheckResult CheckHandsVisible(GameObject playerRoot)
        {
            var displayer = playerRoot.GetComponentInChildren<HandsDisplayer>(true);
            if (displayer == null)
                return new SetupValidator.CheckResult("Scene: hand visibility", SetupValidator.Severity.Fail,
                    "No HandsDisplayer on the scene player — NOTHING shows or hides the VR hands (they stay in whatever " +
                    "state the prefab was saved in).",
                    "It ships on the package Player.prefab; re-add it on your Player VARIANT and assign its Left/Right hand objects.");

            var so = new SerializedObject(displayer);
            var problems = new List<string>();
            foreach (var slot in new[] { "leftHand", "rightHand" })
            {
                var property = so.FindProperty(slot);
                if (property == null)
                {
                    problems.Add($"field '{slot}' no longer exists on HandsDisplayer (renamed?)");
                    continue;
                }

                var target = property.objectReferenceValue as GameObject;
                if (target == null)
                {
                    problems.Add($"{slot} is not assigned — that hand NEVER appears in VR");
                    continue;
                }

                if (!target.transform.IsChildOf(playerRoot.transform))
                    problems.Add($"{slot} points at '{target.name}', which is not under the player — it will be toggled " +
                                 "out of place (a leftover reference from an older hand hierarchy)");
            }

            if (problems.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand visibility", SetupValidator.Severity.Pass,
                    "HandsDisplayer drives both hand objects.");

            return new SetupValidator.CheckResult("Scene: hand visibility", SetupValidator.Severity.Fail,
                $"HandsDisplayer on '{displayer.gameObject.name}': {string.Join("; ", problems)}.",
                "Select the HandsDisplayer on your Player VARIANT and assign Left/Right hand to the hand objects of " +
                "the CURRENT hierarchy (a package update can move them — that is how the hands vanished before).");
        }

        // 2. Two things toggling the same hand objects fight each other and the hands end
        // up hidden. HandsDisplayer (control-scheme driven) is the single authority; the
        // XRI sample's HandsAndControllersManager applies the opposite hand-tracking logic.
        private static SetupValidator.CheckResult CheckVisibilityAuthority(GameObject playerRoot)
        {
            var displayers = playerRoot.GetComponentsInChildren<HandsDisplayer>(true);
            if (displayers.Length > 1)
                return new SetupValidator.CheckResult("Scene: hand visibility authority", SetupValidator.Severity.Fail,
                    $"{displayers.Length} HandsDisplayer components on the player " +
                    $"({string.Join(", ", displayers.Select(d => d.gameObject.name))}) — they toggle the same objects " +
                    "and the last one to run wins, so the hands flicker or stay hidden.",
                    "Keep exactly one HandsDisplayer on the Player variant and delete the others.");

            var intruder = playerRoot.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(c => c != null && c.GetType().Name == "HandsAndControllersManager");

            if (intruder != null)
                return new SetupValidator.CheckResult("Scene: hand visibility authority", SetupValidator.Severity.Fail,
                    $"The XRI-sample HandsAndControllersManager is on '{intruder.gameObject.name}' — it toggles the SAME " +
                    "Left/Right Controller objects as HandsDisplayer but with hand-tracking logic, so the two fight and " +
                    "the hands end up hidden.",
                    "Remove that component from your Player variant — HandsDisplayer owns hand visibility.");

            return new SetupValidator.CheckResult("Scene: hand visibility authority", SetupValidator.Severity.Pass,
                "HandsDisplayer is the single hand-visibility authority.");
        }

        // 3. HandPoseManager is what turns an XRI selection into a hand pose. With no
        // targetInteractor its Init() returns before subscribing: grabbing an object
        // never closes the fingers, and nothing is logged. HandType.None makes
        // Pose.GetHandInfo return Empty, so that hand can never be posed at all.
        private static SetupValidator.CheckResult CheckHandPoseManagers(GameObject playerRoot)
        {
            var managers = playerRoot.GetComponentsInChildren<HandPoseManager>(true);
            if (managers.Length == 0)
                return new SetupValidator.CheckResult("Scene: hand pose managers", SetupValidator.Severity.Fail,
                    "No HandPoseManager on the scene player — the hands have no pose driver at all (no default pose, " +
                    "no grab pose, and ControllerHandPoseDriver finds nothing to drive).",
                    "HandPoseManager ships on each hand of the package Player.prefab; restore it on your Player VARIANT.");

            var problems = new List<string>();
            foreach (var manager in managers)
            {
                if (manager.HandType == HandType.None)
                    problems.Add($"'{manager.gameObject.name}' has Hand Type = None — Pose.GetHandInfo returns empty, " +
                                 "so NO pose ever applies to it");
                if (manager.targetInteractor == null)
                    problems.Add($"'{manager.gameObject.name}' has no Target Interactor — grabbing an object never " +
                                 "poses this hand (OnValidate only auto-fills it when an XRBaseInteractor is a PARENT)");
            }

            // Several hands per side is the NORMAL layout: each side carries a physics hand
            // (rigidbody follower) plus the non-physical one HandsPhysics shows when the
            // physics hand lags behind. They share the side, so only a MISSING side is wrong.
            foreach (var side in new[] { HandType.Left, HandType.Right })
            {
                if (!managers.Any(m => m.HandType == side))
                    problems.Add($"no hand declares Hand Type = {side} — that side can never be posed");
            }

            // A hand typed against its own name is the real "wrong side" bug: it silently
            // receives the other hand's pose data (mirrored fingers).
            foreach (var manager in managers)
            {
                var named = SideFromName(manager.gameObject.name);
                if (named != HandType.None && manager.HandType != HandType.None && named != manager.HandType)
                    problems.Add($"'{manager.gameObject.name}' is named as a {named} hand but declares Hand Type = " +
                                 $"{manager.HandType} — it receives the {manager.HandType} pose data (mirrored fingers)");
            }

            if (problems.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand pose managers", SetupValidator.Severity.Pass,
                    $"{managers.Length} HandPoseManager(s) covering both sides " +
                    $"({managers.Count(m => m.HandType == HandType.Left)} left, " +
                    $"{managers.Count(m => m.HandType == HandType.Right)} right), each wired to its interactor.");

            return new SetupValidator.CheckResult("Scene: hand pose managers", SetupValidator.Severity.Fail,
                string.Join("; ", problems) + ".",
                "On your Player VARIANT: set each hand's Hand Type to its own side (the physics hand and the " +
                "non-physical hand of a side share it) and assign its Target Interactor (that controller's " +
                "Direct/Near-Far interactor).");
        }

        // 4. BaseHand builds its joint list from fingerRoots in Awake. Empty roots means
        // zero joints: every ApplyPose call runs and changes nothing — the fingers are frozen.
        private static SetupValidator.CheckResult CheckFingerRoots(BaseHand[] hands)
        {
            if (hands.Length == 0)
                return new SetupValidator.CheckResult("Scene: hand rigs", SetupValidator.Severity.Fail,
                    "No hand (BaseHand) found under the scene player — there is no hand rig to pose.",
                    "Check that the hand objects still exist on your Player VARIANT (a package update can drop " +
                    "overrides pointing at removed objects — see the variant-overrides check).");

            var problems = new List<string>();
            foreach (var hand in hands)
            {
                var roots = hand.FingerRoots;
                var declared = roots?.Count ?? 0;
                var assigned = 0;
                for (var i = 0; i < declared; i++)
                {
                    if (roots[i] != null) assigned++;
                }

                if (assigned == 0)
                {
                    problems.Add($"'{hand.name}' has no Finger Roots — it collects 0 joints, so EVERY pose applies to " +
                                 "nothing (fingers frozen)");
                    continue;
                }

                if (declared != assigned)
                    problems.Add($"'{hand.name}' has {declared - assigned} empty Finger Root slot(s) — those finger " +
                                 "chains are missing from the pose");

                if (CountJoints(hand) == 0)
                    problems.Add($"'{hand.name}': its Finger Roots contain no bone — the rig under them is gone");
            }

            if (problems.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand rigs", SetupValidator.Severity.Pass,
                    $"{hands.Length} hand rig(s) expose their finger chains " +
                    $"({string.Join(", ", hands.Select(h => $"{h.name}: {CountJoints(h)} joints"))}).");

            return new SetupValidator.CheckResult("Scene: hand rigs", SetupValidator.Severity.Fail,
                string.Join("; ", problems) + ".",
                "Select the hand on your Player VARIANT and assign every finger chain root (one per finger) in " +
                "Finger Roots — they must point at bones of the hand's own skeleton.");
        }

        // 5. A pose stores one rotation per joint of the rig it was authored on. Applied to
        // a rig with a different joint count, BaseHand.ApplyFingerRotations refuses it — the
        // hand keeps its previous pose, which reads as "the pose does nothing".
        private static SetupValidator.CheckResult CheckPosesMatchRig(Dictionary<Pose, string> poses, BaseHand[] hands)
        {
            if (poses.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand poses vs rig", SetupValidator.Severity.Pass,
                    "No pose is referenced by the player or the scene's grabbable objects — nothing to verify.");

            // A side carries several rigs (physics + non-physical hand). They normally share
            // a joint count, but a pose is ignored on EVERY rig it does not match — so
            // compare against each distinct count, not just the first hand of the side.
            var rigs = hands
                .Where(hand => hand.HandType != HandType.None)
                .GroupBy(hand => (side: hand.HandType, joints: CountJoints(hand)))
                .Select(group => (group.Key.side, group.Key.joints, names: string.Join(", ", group.Select(h => h.name))))
                .ToArray();

            if (rigs.Length == 0)
                return new SetupValidator.CheckResult("Scene: hand poses vs rig", SetupValidator.Severity.Warning,
                    "No typed hand rig in the scene — cannot compare the project's poses against it.",
                    "Fix the hand rig / Hand Type problems reported above, then validate again.");

            var failures = new List<string>();
            foreach (var entry in poses)
            {
                foreach (var rig in rigs)
                {
                    var info = rig.side == HandType.Left ? entry.Key.leftHandInfo : entry.Key.rightHandInfo;
                    var rotations = info?.fingerRotations?.Count ?? 0;
                    if (rotations == 0) continue;      // no data for this hand — legitimate for one-handed poses
                    if (rotations == rig.joints) continue;

                    failures.Add($"'{entry.Key.name}' (used by {entry.Value}): {rig.side} hand stores {rotations} " +
                                 $"rotations, the rig has {rig.joints} joints ({rig.names})");
                }
            }

            if (failures.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand poses vs rig", SetupValidator.Severity.Pass,
                    $"All {poses.Count} referenced pose(s) match the scene's hand rig(s).");

            return new SetupValidator.CheckResult("Scene: hand poses vs rig", SetupValidator.Severity.Fail,
                $"{failures.Count} pose/rig mismatch(es) — these poses are SILENTLY IGNORED at runtime: " +
                string.Join("; ", failures) + ".",
                "Each of those poses was saved against a different hand rig. Open it in " +
                "Tools/UniversalPlayer/Pose Editor, re-apply it on the current hand and re-save.");
        }

        // 6. Poses saved before bone names existed map their rotations BY INDEX, which
        // lands on the wrong fingers as soon as the runtime hand orders its finger roots
        // differently from the authoring hand. Runtime only warns once, per hand.
        private static SetupValidator.CheckResult CheckPoseBoneNames(Dictionary<Pose, string> poses)
        {
            if (poses.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand pose bone names", SetupValidator.Severity.Pass,
                    "No pose is referenced by the player or the scene's grabbable objects — nothing to verify.");

            var legacy = new List<string>();
            foreach (var entry in poses)
            {
                if (LacksBoneNames(entry.Key.leftHandInfo) || LacksBoneNames(entry.Key.rightHandInfo))
                    legacy.Add($"'{entry.Key.name}' (used by {entry.Value})");
            }

            if (legacy.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand pose bone names", SetupValidator.Severity.Pass,
                    $"All {poses.Count} referenced pose(s) carry bone names (applied by name, order-independent).");

            var preview = string.Join(", ", legacy.Take(6)) + (legacy.Count > 6 ? ", ..." : "");
            return new SetupValidator.CheckResult("Scene: hand pose bone names", SetupValidator.Severity.Warning,
                $"{legacy.Count} pose(s) carry NO bone names and map by list index — the fingers can be scrambled on a " +
                $"hand whose Finger Roots are ordered differently ({preview}).",
                "Run Tools/UniversalPlayer/Migrate Poses (add bone names) once in Edit Mode, then re-test the poses.");
        }

        private static bool LacksBoneNames(HandInfo info)
        {
            var rotations = info?.fingerRotations?.Count ?? 0;
            if (rotations == 0) return false; // no data for this hand — nothing to name
            var names = info.jointNames?.Count ?? 0;
            return names != rotations;
        }

        // 7. The driver is what makes grip/trigger close the fingers. Disabled or with no
        // fist pose assigned, the hands keep their default pose forever: they look alive
        // in the editor and dead in the headset.
        private static SetupValidator.CheckResult CheckPoseDriver(GameObject playerRoot)
        {
            var driver = playerRoot.GetComponentInChildren<ControllerHandPoseDriver>(true);
            if (driver == null)
                return new SetupValidator.CheckResult("Scene: hand pose driver", SetupValidator.Severity.Fail,
                    "No ControllerHandPoseDriver on the scene player — grip and trigger never animate the fingers " +
                    "(and FingerPointingRay has no POINT state, so the pointing ray never shows).",
                    "It ships on the package Player.prefab root; restore it on your Player VARIANT.");

            var so = new SerializedObject(driver);
            var problems = new List<string>();
            var severity = SetupValidator.Severity.Pass;

            var enabledProperty = so.FindProperty("driveEnabled");
            if (enabledProperty != null && !enabledProperty.boolValue)
            {
                problems.Add("Drive Enabled is OFF — grip/trigger never change the hand pose");
                severity = SetupValidator.Severity.Warning;
            }

            var fists = new[] { "semiClosedFistPose", "closedFistPose", "fullClosedFistPose" };
            var assignedFists = fists.Count(slot => so.FindProperty(slot)?.objectReferenceValue != null);
            if (assignedFists == 0)
            {
                problems.Add("no fist pose assigned — the fingers NEVER close (grip and trigger fall back to the default pose)");
                severity = SetupValidator.Severity.Fail;
            }
            else if (assignedFists < fists.Length)
            {
                problems.Add($"{fists.Length - assignedFists} of 3 fist poses empty — those grip levels degrade to the " +
                             "nearest assigned pose (no visible difference between them)");
                if (severity == SetupValidator.Severity.Pass) severity = SetupValidator.Severity.Warning;
            }

            var touch = so.FindProperty("gripTouchThreshold");
            var hard = so.FindProperty("gripHardThreshold");
            if (touch != null && hard != null && touch.floatValue >= hard.floatValue)
            {
                problems.Add($"Grip Touch Threshold ({touch.floatValue:0.##}) is not below Grip Hard Threshold " +
                             $"({hard.floatValue:0.##}) — the semi-closed fist is UNREACHABLE (the hard grip always wins)");
                severity = SetupValidator.Severity.Fail;
            }

            if (problems.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand pose driver", SetupValidator.Severity.Pass,
                    "ControllerHandPoseDriver is enabled with its fist poses assigned.");

            return new SetupValidator.CheckResult("Scene: hand pose driver", severity,
                $"ControllerHandPoseDriver on '{driver.gameObject.name}': {string.Join("; ", problems)}.",
                "Author the poses with Tools/UniversalPlayer/Pose Editor and assign them on your Player VARIANT " +
                "(Point / Semi-Closed / Closed / Full Closed Fist), and keep Grip Touch below Grip Hard.");
        }

        // 8. The ray is resolved with GetComponentInChildren from the ray's own object, so
        // a driver sitting outside that subtree is simply not found and the ray never shows.
        private static SetupValidator.CheckResult CheckPointingRay(GameObject playerRoot)
        {
            var ray = playerRoot.GetComponentInChildren<FingerPointingRay>(true);
            if (ray == null)
                return new SetupValidator.CheckResult("Scene: finger pointing ray", SetupValidator.Severity.Warning,
                    "No FingerPointingRay on the scene player — VR has no pointing ray on hover (and PerformAction " +
                    "has no XR ray to raycast along).",
                    "It ships on the package Player.prefab root; restore it on your Player VARIANT.");

            if (ray.GetComponentInChildren<ControllerHandPoseDriver>(true) == null)
                return new SetupValidator.CheckResult("Scene: finger pointing ray", SetupValidator.Severity.Fail,
                    $"FingerPointingRay on '{ray.gameObject.name}' cannot reach a ControllerHandPoseDriver (it looks on " +
                    "its own object and BELOW) — with no POINT pose state the ray never shows.",
                    "Put FingerPointingRay on the same object as ControllerHandPoseDriver (the Player root), as on the " +
                    "package Player.prefab.");

            var mask = new SerializedObject(ray).FindProperty("physicsHoverMask");
            if (mask != null && mask.intValue == 0)
                return new SetupValidator.CheckResult("Scene: finger pointing ray", SetupValidator.Severity.Warning,
                    $"FingerPointingRay on '{ray.gameObject.name}' has Physics Hover Mask = Nothing — the ray only shows " +
                    "on XRI interactables, seats, pickables and tooltips, never on your project's own raycast targets.",
                    "Set the same layers as your desktop reticle / click handler on the Player VARIANT " +
                    "(leave it empty only if the project has no custom interactable layer).");

            return new SetupValidator.CheckResult("Scene: finger pointing ray", SetupValidator.Severity.Pass,
                "FingerPointingRay is paired with the pose driver and has its hover layers set.");
        }

        // 9. A skinned hand's MeshCollider is frozen in bind pose, so BlendableHand swaps it
        // for one box per phalanx at runtime — but only if the rig's bones are named like the
        // CC rig. When the plan is empty the hand ends up with NO collider at all.
        private static SetupValidator.CheckResult CheckHandColliders(GameObject playerRoot)
        {
            var blendables = playerRoot.GetComponentsInChildren<BlendableHand>(true);
            if (blendables.Length == 0)
                return new SetupValidator.CheckResult("Scene: hand colliders", SetupValidator.Severity.Warning,
                    "No BlendableHand on the scene player — nothing builds the per-finger colliders, so the hands cannot " +
                    "touch anything (and hand appearance/blend shapes are not registered either).",
                    "BlendableHand ships on each hand mesh of the package Player.prefab; restore it on your Player VARIANT.");

            var problems = new List<string>();
            foreach (var blendable in blendables)
            {
                var skin = blendable.GetComponent<SkinnedMeshRenderer>() ?? blendable.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (skin == null)
                {
                    problems.Add($"'{blendable.name}' has no SkinnedMeshRenderer — no mesh, no bones, no collider");
                    continue;
                }

                var bonesRoot = skin.rootBone != null ? skin.rootBone : blendable.transform;
                if (HandColliderBuilder.PlanFingerBoxes(bonesRoot).Count == 0)
                    problems.Add($"'{blendable.name}': no phalanx bone found under '{bonesRoot.name}' (expected names " +
                                 "containing Proximal/Intermediate/Distal, like the CC hand rig) — at runtime this hand " +
                                 "gets NO collider at all");
            }

            if (problems.Count == 0)
                return new SetupValidator.CheckResult("Scene: hand colliders", SetupValidator.Severity.Pass,
                    $"{blendables.Length} hand mesh(es) expose phalanx bones — per-finger colliders will be built at runtime.");

            return new SetupValidator.CheckResult("Scene: hand colliders", SetupValidator.Severity.Fail,
                string.Join("; ", problems) + ".",
                "Rename the finger bones to the CC convention (…Proximal/…Intermediate/…Distal), point the " +
                "SkinnedMeshRenderer's Root Bone at the hand skeleton, or add the colliders manually.");
        }

        /// <summary>
        /// Every Pose the PROJECT actually uses, mapped to what references it: the hands'
        /// default poses, the driver's grip/trigger poses, and the grab poses of the open
        /// scene's interactables. (PoseAssetTests only covers the poses shipped IN the package.)
        /// </summary>
        private static Dictionary<Pose, string> CollectProjectPoses(GameObject playerRoot)
        {
            var poses = new Dictionary<Pose, string>();

            void Add(Pose pose, string owner)
            {
                if (pose != null && !poses.ContainsKey(pose)) poses.Add(pose, owner);
            }

            foreach (var hand in playerRoot.GetComponentsInChildren<BaseHand>(true))
            {
                if (hand == null || hand is PreviewHand) continue;
                Add(hand.DefaultPose, $"{hand.name}: default pose");
            }

            var driver = playerRoot.GetComponentInChildren<ControllerHandPoseDriver>(true);
            if (driver != null)
            {
                var so = new SerializedObject(driver);
                foreach (var slot in new[] { "pointPose", "semiClosedFistPose", "closedFistPose", "fullClosedFistPose" })
                    Add(so.FindProperty(slot)?.objectReferenceValue as Pose, $"pose driver: {slot}");
            }

            foreach (var container in Object.FindObjectsByType<PoseContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(container.pose, $"{container.name} (PoseContainer)");

            foreach (var pickable in Object.FindObjectsByType<PickableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Add(pickable.HandPose, $"{pickable.name} (PickableObject)");

            return poses;
        }

        /// <summary>
        /// The side a hand object's NAME claims, or None when it says nothing (or both).
        /// Used to catch a hand typed against its own name — the mirrored-pose bug.
        /// </summary>
        private static HandType SideFromName(string objectName)
        {
            var lowered = objectName.ToLowerInvariant();
            var left = lowered.Contains("left");
            var right = lowered.Contains("right");
            if (left == right) return HandType.None; // neither, or ambiguous
            return left ? HandType.Left : HandType.Right;
        }

        /// <summary>
        /// Replicates BaseHand.CollectJoints, which only runs in Awake and is therefore
        /// unavailable at edit time. Inactive children are included on purpose: the hands
        /// are switched off outside XR, but they carry the same joints once enabled.
        /// </summary>
        private static int CountJoints(BaseHand hand)
        {
            var count = 0;
            if (hand.FingerRoots == null) return 0;
            foreach (var root in hand.FingerRoots)
            {
                if (root != null) count += root.GetComponentsInChildren<Transform>(true).Length;
            }
            return count;
        }
    }
}
