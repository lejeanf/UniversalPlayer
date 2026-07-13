using System.Collections.Generic;
using System.Linq;
using jeanf.EventSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Tools/UniversalPlayer/Hands Test Bench — test VR features without a headset
    /// (play mode): simulate the XR control scheme, show hands, apply any hand pose,
    /// fire a UI click through the real XRI pipeline, and trigger scene teleports.
    /// </summary>
    public class HandsTestBench : EditorWindow
    {
        private Vector2 _scroll;

        // poses
        private Pose[] _poses = new Pose[0];
        private string[] _poseNames = new string[0];
        private int _poseIndex;

        // ui click
        private XRRayInteractor[] _rayInteractors = new XRRayInteractor[0];
        private string[] _rayInteractorNames = new string[0];
        private int _rayIndex;
        private XRInputButtonReader _pressedReader;
        private XRInputButtonReader.InputSourceMode _previousMode;
        private int _releaseAtFrame = -1;

        // teleport
        private bool _teleportWithFade = true;

        [MenuItem("Tools/UniversalPlayer/Hands Test Bench")]
        public static void Open()
        {
            var window = GetWindow<HandsTestBench>("Hands Test Bench");
            window.minSize = new Vector2(340f, 420f);
        }

        private void OnEnable()
        {
            RefreshPoses();
            EditorApplication.update += ProcessPendingRelease;
        }

        private void OnDisable()
        {
            EditorApplication.update -= ProcessPendingRelease;
        }

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use the test bench.", MessageType.Info);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawControlSchemeSection();
            EditorGUILayout.Space(10);
            DrawHandsSection();
            EditorGUILayout.Space(10);
            DrawPoseSection();
            EditorGUILayout.Space(10);
            DrawUiClickSection();
            EditorGUILayout.Space(10);
            DrawTeleportSection();
            EditorGUILayout.EndScrollView();
        }

        #region control scheme

        private void DrawControlSchemeSection()
        {
            EditorGUILayout.LabelField("Control scheme (all listeners react)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current", BroadcastControlsStatus.controlScheme.ToString());

            var broadcaster = FindFirstObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
            if (broadcaster == null)
            {
                EditorGUILayout.HelpBox("No BroadcastControlsStatus in the scene — is the Player (variant) present?", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Simulate XR (headset on)"))
                    SimulateControlScheme(broadcaster, BroadcastControlsStatus.ControlScheme.XR);
                if (GUILayout.Button("Simulate Keyboard && Mouse"))
                    SimulateControlScheme(broadcaster, BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            }
        }

        private static void SimulateControlScheme(BroadcastControlsStatus broadcaster, BroadcastControlsStatus.ControlScheme scheme)
        {
            BroadcastControlsStatus.controlScheme = scheme;
            // Everything (package internals AND project channels via the PlayerEventBridge)
            // hangs off these two — raising them simulates a real device change end to end.
            BroadcastControlsStatus.SendControlScheme?.Invoke(scheme);
            PlayerEvents.RaiseHmdState(scheme == BroadcastControlsStatus.ControlScheme.XR);

            Debug.Log($"{XrStartupDiagnostics.LogPrefix} Hands Test Bench: simulated control scheme '{scheme}'.");
        }

        #endregion

        #region hands

        private void DrawHandsSection()
        {
            EditorGUILayout.LabelField("Hands", EditorStyles.boldLabel);
            var displayer = FindFirstObjectByType<HandsDisplayer>(FindObjectsInactive.Include);
            if (displayer == null)
            {
                EditorGUILayout.HelpBox("No HandsDisplayer in the scene — is the Player (variant) present?", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show hands")) displayer.ForceDisplay(true);
                if (GUILayout.Button("Hide hands")) displayer.ForceDisplay(false);
            }
            EditorGUILayout.HelpBox("Forced hands stay until the next real control-scheme change.", MessageType.None);
        }

        #endregion

        #region poses

        private void RefreshPoses()
        {
            _poses = AssetDatabase.FindAssets("t:Pose")
                .Select(guid => AssetDatabase.LoadAssetAtPath<Pose>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(pose => pose != null)
                .OrderBy(pose => pose.name)
                .ToArray();
            _poseNames = _poses.Select(pose => pose.name).ToArray();
            _poseIndex = Mathf.Clamp(_poseIndex, 0, Mathf.Max(0, _poses.Length - 1));
        }

        private void DrawPoseSection()
        {
            EditorGUILayout.LabelField("Hand poses", EditorStyles.boldLabel);
            if (_poses.Length == 0)
            {
                EditorGUILayout.HelpBox("No Pose assets found in the project.", MessageType.Info);
                if (GUILayout.Button("Refresh pose list")) RefreshPoses();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _poseIndex = EditorGUILayout.Popup(_poseIndex, _poseNames);
                if (GUILayout.Button("Refresh", GUILayout.Width(70f))) RefreshPoses();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply to Left")) ApplyPose(HandType.Left);
                if (GUILayout.Button("Apply to Right")) ApplyPose(HandType.Right);
                if (GUILayout.Button("Apply to Both")) { ApplyPose(HandType.Left); ApplyPose(HandType.Right); }
            }
            if (GUILayout.Button("Reset to default pose"))
            {
                foreach (var hand in ActiveHands(HandType.Left).Concat(ActiveHands(HandType.Right)))
                    hand.ApplyDefaultPose();
            }
        }

        private void ApplyPose(HandType handType)
        {
            var pose = _poses[_poseIndex];
            var hands = ActiveHands(handType).ToArray();
            if (hands.Length == 0)
            {
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} Hands Test Bench: no awake {handType} hand found — press 'Show hands' first.");
                return;
            }
            foreach (var hand in hands)
            {
                hand.ApplyPose(pose);
                Debug.Log($"{XrStartupDiagnostics.LogPrefix} Hands Test Bench: applied pose '{pose.name}' to '{hand.name}' ({handType}).");
            }
        }

        private IEnumerable<BaseHand> ActiveHands(HandType handType)
        {
            // ensure the hands are active so BaseHand.Awake has collected its joints
            FindFirstObjectByType<HandsDisplayer>(FindObjectsInactive.Include)?.ForceDisplay(true);
            return FindObjectsByType<BaseHand>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(hand => hand.HandType == handType && hand.Joints.Count > 0);
        }

        #endregion

        #region ui click

        private void DrawUiClickSection()
        {
            EditorGUILayout.LabelField("UI click (through the real XRI pipeline)", EditorStyles.boldLabel);

            if (GUILayout.Button("Find ray interactors"))
            {
                _rayInteractors = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(interactor => interactor.enableUIInteraction)
                    .ToArray();
                _rayInteractorNames = _rayInteractors.Select(FullPath).ToArray();
                _rayIndex = 0;
            }

            _rayInteractors = _rayInteractors.Where(interactor => interactor != null).ToArray();
            if (_rayInteractors.Length == 0)
            {
                EditorGUILayout.HelpBox("No active UI-enabled XRRayInteractor found. Simulate XR / show hands first, then press 'Find ray interactors'.", MessageType.Info);
                return;
            }

            _rayIndex = Mathf.Clamp(_rayIndex, 0, _rayInteractors.Length - 1);
            _rayIndex = EditorGUILayout.Popup("Interactor", _rayIndex, _rayInteractorNames);

            var selected = _rayInteractors[_rayIndex];
            var hasUiHit = selected.TryGetCurrentUIRaycastResult(out var uiHit);
            EditorGUILayout.LabelField("UI under ray", hasUiHit ? uiHit.gameObject.name : "(nothing)");

            using (new EditorGUI.DisabledScope(_pressedReader != null))
            {
                if (GUILayout.Button(_pressedReader != null ? "Clicking..." : "Simulate UI click (press + release)"))
                    BeginClick(selected);
            }
            EditorGUILayout.HelpBox("Aim the ray at a canvas (move the hand/controller object in the scene view), then click.", MessageType.None);
        }

        private void BeginClick(XRRayInteractor interactor)
        {
            _pressedReader = interactor.uiPressInput;
            _previousMode = _pressedReader.inputSourceMode;
            _pressedReader.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            _pressedReader.QueueManualState(true, 1f, true, false);
            _releaseAtFrame = Time.frameCount + 2;
        }

        private void ProcessPendingRelease()
        {
            if (_pressedReader == null || !Application.isPlaying)
            {
                _pressedReader = null;
                return;
            }
            if (Time.frameCount < _releaseAtFrame) return;

            _pressedReader.QueueManualState(false, 0f, false, true);
            _pressedReader.inputSourceMode = _previousMode;
            _pressedReader = null;
            Repaint();
        }

        private static string FullPath(Component component)
        {
            var path = component.name;
            for (var parent = component.transform.parent; parent != null; parent = parent.parent)
                path = $"{parent.name}/{path}";
            return path;
        }

        #endregion

        #region teleport

        private void DrawTeleportSection()
        {
            EditorGUILayout.LabelField("Teleport", EditorStyles.boldLabel);

            var targets = FindObjectsByType<SendTeleportTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(target => target.name)
                .ToArray();
            if (targets.Length == 0)
            {
                EditorGUILayout.HelpBox("No SendTeleportTarget in the scene.", MessageType.Info);
                return;
            }

            var listeners = FindObjectsByType<TeleportOnEvent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (listeners.Length == 0)
            {
                EditorGUILayout.HelpBox("No TeleportOnEvent in the scene — teleport events go nowhere. " +
                    "Add one (usually on the Player variant), listening on the same TeleportEventChannel, " +
                    "with OnEventRaised wired to its Teleport method.", MessageType.Error);
            }

            _teleportWithFade = EditorGUILayout.ToggleLeft("Fade to black during teleport", _teleportWithFade);
            foreach (var target in targets)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"{target.name}  {target.transform.position}", GUILayout.MinWidth(120f));
                    if (GUILayout.Button("Teleport", GUILayout.Width(80f)))
                    {
                        target.Teleport(_teleportWithFade);
                        Debug.Log($"{XrStartupDiagnostics.LogPrefix} Hands Test Bench: teleport requested via '{target.name}'.");
                    }
                }

                var mismatch = DescribeListenerMismatch(target, listeners);
                if (mismatch != null) EditorGUILayout.HelpBox(mismatch, MessageType.Warning);
            }
        }

        /// <summary>Explains why no listener would accept this target's teleport, or null when at least one matches.</summary>
        private static string DescribeListenerMismatch(SendTeleportTarget target, TeleportOnEvent[] listeners)
        {
            if (listeners.Length == 0) return null; // already reported once above

            var targetChannel = new SerializedObject(target).FindProperty("_teleportChannel")?.objectReferenceValue;
            if (targetChannel == null)
                return $"'{target.name}' has no TeleportEventChannel assigned — the Teleport button raises nothing.";

            var channelMatches = listeners.Where(listener =>
                new SerializedObject(listener).FindProperty("_channel")?.objectReferenceValue == targetChannel).ToArray();
            if (channelMatches.Length == 0)
                return $"No TeleportOnEvent listens on '{targetChannel.name}' (the channel '{target.name}' broadcasts on) — check the channel assets on both sides.";

            if (target.isUsingFilter)
            {
                var filterAccepted = channelMatches.Any(listener =>
                {
                    var filters = new SerializedObject(listener).FindProperty("listOfFilters");
                    if (filters == null || !filters.isArray) return false;
                    for (var i = 0; i < filters.arraySize; i++)
                        if (filters.GetArrayElementAtIndex(i).objectReferenceValue == target._filter) return true;
                    return false;
                });
                if (!filterAccepted)
                    return $"'{target.name}' uses filter '{(target._filter != null ? target._filter.name : "<none>")}' " +
                           "but no channel-matching TeleportOnEvent has it in its filter list — the teleport will be rejected.";
            }

            return null;
        }

        #endregion
    }
}
