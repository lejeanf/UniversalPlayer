#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using UnityEngine;
using UnityEngine.SceneManagement;

namespace jeanf.universalplayer
{
#if !UNITY_EDITOR
public class PoseWindow {}
#endif
#if UNITY_EDITOR
    /// <summary>
    /// Wizard-style pose editor: 1) pick or create a pose, 2) say whether it is a
    /// grab pose for a held object or a hand-only gesture, 3) assign the object when
    /// one is needed — the hands then spawn automatically and 4) are edited directly
    /// in the Scene view (Move/Rotate tools, blue joint dots, auto-pose, IK targets),
    /// 5) saved live or on demand.
    ///
    /// The hands are parented to an internal UNIT-SCALE anchor that follows the
    /// object — never to the object itself. A scaled placeholder (e.g. a stretched
    /// cube standing in for the tablet) therefore cannot stretch or shear the hands,
    /// and the saved attach offsets stay in real-world units, valid for the real
    /// unscaled mesh.
    /// </summary>
    public class PoseWindow : EditorWindow
    {
        // Step 1 — the pose being edited (index 0 of the popup is "(select a pose)")
        private Pose activePose;
        private Pose[] _allPoses = new Pose[0];
        private string[] _allPoseNames = new string[0];
        private int _poseBrowserIndex;

        // Steps 2 & 3 — what the pose is for
        private enum PoseKind { HeldObject, HandGesture }
        private PoseKind _poseKind = PoseKind.HeldObject;
        private GameObject _previewObject;
        private bool _handsPlaced;

        // Scene objects: the PoseHelper (spawns the preview hands) and the
        // unit-scale anchor the hands are parented to (follows the object).
        private GameObject poseHelper;
        private Transform _handAnchor;
        private HandManager handManager;

        // Step 4 — editing state
        private enum EditedSide { Both, Left, Right }
        private EditedSide _editedSide = EditedSide.Both;

        // Mirror plane: through the object's center by default (its local X plane);
        // "between the hands" reflects one hand onto the other's current spot.
        private enum MirrorPlaneMode { ObjectX, ObjectY, ObjectZ, BetweenHands }
        private static readonly string[] MirrorPlaneLabels =
            { "Object center — X", "Object center — Y", "Object center — Z", "Between the hands" };
        private MirrorPlaneMode _mirrorPlaneMode = MirrorPlaneMode.ObjectX;
        private bool _showMirrorPlane = true;

        // Player-view reference for gesture poses: where the head sits and which way
        // it looks, in the anchor frame (= the controllers' neutral frame). A correct
        // rest pose reads: both thumbs up, palms facing each other across the view.
        private bool _showViewReference = true;
        private bool _isolate = true;
        private bool _autoPoseOnMove;
        // Hand flexibility (0 = stiff, 1 = loose): scales finger ROM, DIP/PIP coupling
        // and how much a curled finger drags its neighbours.
        private float _flexibility = 0.5f;
        private int _gestureIndex;
        private bool _ikTargets;

        // VR controller reference models: at runtime the hand root and the controller
        // model share the tracked-controller origin, so an identity-parented model
        // under each preview hand shows exactly how the pose sits on the controller.
        private bool _showControllers;
        private bool _showColliders;
        private readonly Dictionary<PreviewHand, GameObject> _controllerVisuals = new Dictionary<PreviewHand, GameObject>();
        private const string LeftControllerModelGuid = "1392f805216c47742996d4742c80721c";  // XR Controller Left.prefab
        private const string RightControllerModelGuid = "9f3369e30fbd31f4bb596b1a99babe83"; // XR Controller Right.prefab

        // Step 5 — saving
        private bool _liveSave = true;
        private bool _advanced;
        private Pose _mixPose;
        private Pose _overwriteTarget;

        // Scene-view joint editing: the joint currently carrying a rotation handle
        // (a hand ROOT selected via its orange dot additionally gets a move gizmo).
        private Transform _selectedJoint;
        // Fingertip IK: the selected tip gets a full position gizmo (axes + plane
        // squares); its target position persists so the handle doesn't chase the tip.
        private Transform _selectedIkTip;
        private Vector3 _ikHandlePosition;

        private Vector2 _scroll;
        private readonly Dictionary<PreviewHand, Vector3> _naturalHandScale = new Dictionary<PreviewHand, Vector3>();
        private readonly Dictionary<PreviewHand, (Vector3 pos, Quaternion rot)> _lastHandPose =
            new Dictionary<PreviewHand, (Vector3, Quaternion)>();
        // Bone lengths are sacred: every joint's local position at spawn, re-imposed
        // each Scene frame so no tool can ever stretch a finger — only rotate it.
        private readonly Dictionary<Transform, Vector3> _jointRestLocalPositions = new Dictionary<Transform, Vector3>();

        private bool SetupComplete => activePose != null
            && (_poseKind == PoseKind.HandGesture || _previewObject != null);
        private bool ReadyToEdit => _handsPlaced && SetupComplete
            && handManager != null && handManager.HandsExist;

        [MenuItem("Tools/UniversalPlayer/Pose Editor")]
        public static void OpenFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Pose Editor works in Edit Mode — exit Play Mode first " +
                    "(use Tools/UniversalPlayer/Hands Test Bench to apply poses during Play Mode).");
                return;
            }
            GetWindow<PoseWindow>("Hand Poser");
        }

        /// <summary>Open the editor on a pose; a context object (e.g. the PoseContainer's) pre-fills step 3.</summary>
        public static void Open(Pose pose, GameObject context = null)
        {
            var window = GetWindow<PoseWindow>("Hand Poser");
            if (context != null)
            {
                window._previewObject = context;
                window._poseKind = PoseKind.HeldObject;
            }
            if (pose != null) window.SelectPose(pose);
        }

        private void OnEnable()
        {
            CreatePoseHelper();
            RefreshPoseList();
            EditorApplication.playModeStateChanged += CloseWindow;
            EditorSceneManager.sceneClosing += CloseWindow;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            SceneVisibilityManager.instance.ExitIsolation();
            DestroyPoseHelper();
            DestroyAnchor();
            EditorApplication.playModeStateChanged -= CloseWindow;
            EditorSceneManager.sceneClosing -= CloseWindow;
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        private void OnUndoRedo()
        {
            // An undo jump is not the user moving a hand: forget the last-known poses
            // so auto-pose-on-move doesn't re-wrap fingers over the state undo restored.
            _lastHandPose.Clear();
            Repaint();
            SceneView.RepaintAll();
        }

        // ---------------------------------------------------------------- scene setup

        private void CreatePoseHelper()
        {
            if (poseHelper) return;

            var helperPrefab = Resources.Load("PoseHelper");
            if (helperPrefab == null)
            {
                Debug.LogError("Pose Editor: 'PoseHelper' prefab not found in any Resources folder — " +
                    "it ships in Runtime/Hands/Prefabs/Resources/. Was it moved or renamed?");
                return;
            }

            poseHelper = (GameObject)PrefabUtility.InstantiatePrefab(helperPrefab);
            poseHelper.hideFlags = HideFlags.DontSave;
            handManager = poseHelper.GetComponent<HandManager>();

            FixPreviewHandMaterials();

            // Preview hands need no physics, and the authored MeshCollider sits frozen
            // in bind pose — its wireframe gizmo just hides the hand while editing.
            foreach (var handCollider in poseHelper.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(handCollider);

            _naturalHandScale.Clear();
            _lastHandPose.Clear();
            _jointRestLocalPositions.Clear();
            _controllerVisuals.Clear(); // children of the previous hands — died with them
            if (handManager != null && handManager.HandsExist)
            {
                // Natural world scale, captured before any reparenting — restored
                // whenever the hands land under the (unit-scale) anchor.
                _naturalHandScale[handManager.LeftHand] = handManager.LeftHand.transform.lossyScale;
                _naturalHandScale[handManager.RightHand] = handManager.RightHand.transform.lossyScale;

                foreach (var hand in new[] { handManager.LeftHand, handManager.RightHand })
                {
                    foreach (var joint in hand.Joints)
                    {
                        if (joint != null) _jointRestLocalPositions[joint] = joint.localPosition;
                    }
                }

                // The hands appear only once steps 1-3 are complete (auto-spawn).
                handManager.LeftHand.gameObject.SetActive(false);
                handManager.RightHand.gameObject.SetActive(false);
            }
        }

        // Joints may only ROTATE — restore every joint's rest offset so no tool
        // (IK, undo glitches, accidental drags) can change the distance between joints.
        private void EnforceJointDistances()
        {
            foreach (var pair in _jointRestLocalPositions)
            {
                if (pair.Key == null) continue;
                if ((pair.Key.localPosition - pair.Value).sqrMagnitude > 1e-12f)
                    pair.Key.localPosition = pair.Value;
            }
        }

        private void DestroyPoseHelper()
        {
            if (poseHelper != null) DestroyImmediate(poseHelper);
            poseHelper = null;
            handManager = null;
        }

        private void EnsureAnchor()
        {
            if (_handAnchor != null) return;
            var anchor = new GameObject("UniversalPlayer_PoseAnchor")
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };
            _handAnchor = anchor.transform;
        }

        private void DestroyAnchor()
        {
            if (_handAnchor != null) DestroyImmediate(_handAnchor.gameObject);
            _handAnchor = null;
        }

        /// <summary>
        /// The auto-spawn step: once a pose (and, for grab poses, an object) is
        /// chosen, the hands are parented to the unit-scale anchor, posed, scaled
        /// back to their natural size and shown. Called on every wizard change.
        /// </summary>
        private void PlaceHands()
        {
            _handsPlaced = false;
            if (handManager == null || !handManager.HandsExist) return;

            if (!SetupComplete)
            {
                handManager.LeftHand.gameObject.SetActive(false);
                handManager.RightHand.gameObject.SetActive(false);
                ApplyIsolation();
                return;
            }

            EnsureAnchor();
            SyncAnchorToTarget();
            handManager.UpdateHandsForSetup(activePose, _handAnchor);
            NormalizeHandScales();
            _handsPlaced = true;
            ApplySideVisibility();
            ApplyIsolation();
            SyncControllerVisuals();
            SceneView.RepaintAll();
        }

        // ---- VR controller reference models (toggle in step 4) ----

        private void SyncControllerVisuals()
        {
            if (!_showControllers || handManager == null || !handManager.HandsExist || !_handsPlaced)
            {
                foreach (var visual in _controllerVisuals.Values)
                {
                    if (visual != null) DestroyImmediate(visual);
                }
                _controllerVisuals.Clear();
                return;
            }
            EnsureControllerVisual(handManager.LeftHand, LeftControllerModelGuid);
            EnsureControllerVisual(handManager.RightHand, RightControllerModelGuid);
        }

        private void EnsureControllerVisual(PreviewHand hand, string modelGuid)
        {
            if (hand == null) return;
            if (_controllerVisuals.TryGetValue(hand, out var existing) && existing != null) return;

            var path = AssetDatabase.GUIDToAssetPath(modelGuid);
            var prefab = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Pose Editor: controller model prefab not found (guid {modelGuid}) — " +
                    "'Show VR controllers' has nothing to display. Were the XR Origin Pieces prefabs moved?");
                return;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = $"ControllerReference_{hand.HandType}";
            visual.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            // Identity under the hand root = the runtime layout (both live at the
            // tracked-controller origin); child of the hand, so it follows the hand,
            // hides with the side toggle and joins the isolation set automatically.
            visual.transform.SetParent(hand.transform, false);
            // Keep the controller true-size even if the hand carries scale.
            var parentScale = hand.transform.lossyScale;
            var authored = prefab.transform.localScale;
            visual.transform.localScale = new Vector3(
                SafeDivide(authored.x, parentScale.x),
                SafeDivide(authored.y, parentScale.y),
                SafeDivide(authored.z, parentScale.z));

            foreach (var visualCollider in visual.GetComponentsInChildren<Collider>(true))
                DestroyImmediate(visualCollider);
            FixBrokenMaterials(visual, new Color(0.3f, 0.3f, 0.32f));

            _controllerVisuals[hand] = visual;
        }

        // The anchor mirrors the object's position/rotation (NEVER its scale); for
        // hand-only gestures it parks at the Scene-view pivot instead.
        private void SyncAnchorToTarget()
        {
            if (_handAnchor == null) return;
            if (_poseKind == PoseKind.HeldObject && _previewObject != null)
            {
                _handAnchor.SetPositionAndRotation(_previewObject.transform.position, _previewObject.transform.rotation);
            }
            else if (!_handsPlaced)
            {
                var sceneView = SceneView.lastActiveSceneView;
                _handAnchor.SetPositionAndRotation(sceneView != null ? sceneView.pivot : Vector3.zero, Quaternion.identity);
            }
        }

        // The anchor is unit-scale, so this normally just restores the spawn scale —
        // the divide keeps it correct even if the anchor ever inherits scale.
        private void NormalizeHandScales()
        {
            foreach (var pair in _naturalHandScale)
            {
                var hand = pair.Key;
                if (hand == null) continue;
                var parentScale = hand.transform.parent != null ? hand.transform.parent.lossyScale : Vector3.one;
                hand.transform.localScale = new Vector3(
                    SafeDivide(pair.Value.x, parentScale.x),
                    SafeDivide(pair.Value.y, parentScale.y),
                    SafeDivide(pair.Value.z, parentScale.z));
            }
        }

        private static float SafeDivide(float a, float b) => Mathf.Approximately(b, 0f) ? 1f : a / b;

        // Same look in URP and HDRP: hand materials authored for the other pipeline
        // render pink — swap those instances (in memory only, the helper is DontSave)
        // for a plain skin-tone material in the active pipeline.
        private void FixPreviewHandMaterials()
        {
            if (poseHelper != null) FixBrokenMaterials(poseHelper, new Color(0.85f, 0.68f, 0.58f));
        }

        private static void FixBrokenMaterials(GameObject root, Color fallbackColor)
        {
            Material replacement = null;
            foreach (var childRenderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = childRenderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!PipelineMaterialGuard.IsBroken(materials[i])) continue;
                    replacement ??= PipelineMaterialGuard.SafeOpaque(fallbackColor);
                    materials[i] = replacement;
                    changed = true;
                }
                if (changed) childRenderer.sharedMaterials = materials;
            }
        }

        /// <summary>
        /// Prefab-mode-style isolation: the Scene view shows only the preview hands
        /// and the preview object (skybox included); the rest of the world hides.
        /// Purely a Scene-view visibility state — nothing in the scene is modified.
        /// </summary>
        private void ApplyIsolation()
        {
            var visibility = SceneVisibilityManager.instance;
            visibility.ExitIsolation();
            if (!_isolate || !_handsPlaced) return;

            var keepVisible = new List<GameObject>();
            if (poseHelper != null) keepVisible.Add(poseHelper);
            if (handManager != null && handManager.HandsExist)
            {
                keepVisible.Add(handManager.LeftHand.gameObject);
                keepVisible.Add(handManager.RightHand.gameObject);
            }
            if (_previewObject != null) keepVisible.Add(_previewObject);
            if (keepVisible.Count > 0) visibility.Isolate(keepVisible.ToArray(), true);
        }

        // Editing left shows the LEFT hand on the object (and vice versa); the edited
        // hand gets selected so the normal Move/Rotate tools adjust its offset.
        private void ApplySideVisibility()
        {
            if (handManager == null || !handManager.HandsExist || !_handsPlaced) return;
            handManager.LeftHand.gameObject.SetActive(_editedSide != EditedSide.Right);
            handManager.RightHand.gameObject.SetActive(_editedSide != EditedSide.Left);
            if (_editedSide == EditedSide.Left) Selection.activeGameObject = handManager.LeftHand.gameObject;
            else if (_editedSide == EditedSide.Right) Selection.activeGameObject = handManager.RightHand.gameObject;
        }

        private IEnumerable<PreviewHand> VisibleHands()
        {
            if (handManager == null || !handManager.HandsExist) yield break;
            if (_editedSide != EditedSide.Right) yield return handManager.LeftHand;
            if (_editedSide != EditedSide.Left) yield return handManager.RightHand;
        }

        // ---------------------------------------------------------------- scene view

        /// <summary>
        /// Scene-view joint tools: every preview-hand joint shows a clickable dot;
        /// clicking one attaches a rotation handle so fingers can be posed directly
        /// in the Scene view (Esc or the window button deselects).
        /// </summary>
        private void OnSceneGUI(SceneView sceneView)
        {
            if (handManager == null || !handManager.HandsExist) return;

            if (_handsPlaced) SyncAnchorToTarget(); // hands follow the object if it moves
            EnforceJointDistances(); // rotation only — bone lengths never change

            if ((_selectedJoint != null || _selectedIkTip != null) && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Escape)
            {
                _selectedJoint = null;
                _selectedIkTip = null;
                Event.current.Use();
                Repaint();
            }

            DrawJointDots(handManager.LeftHand, sceneView);
            DrawJointDots(handManager.RightHand, sceneView);
            DrawRootDot(handManager.LeftHand);
            DrawRootDot(handManager.RightHand);

            if (_selectedJoint != null) DrawJointRotationHandle();

            HandleHandMovement(handManager.LeftHand);
            HandleHandMovement(handManager.RightHand);

            if (_ikTargets)
            {
                DrawFingertipTargets(handManager.LeftHand);
                DrawFingertipTargets(handManager.RightHand);
            }

            if (_showColliders)
            {
                DrawPlannedColliders(handManager.LeftHand);
                DrawPlannedColliders(handManager.RightHand);
            }

            DrawMirrorPlane();
            DrawViewReference();
        }

        // Head marker + forward arrow + a faint view frustum, sitting behind the
        // hands the way the player's head does behind resting controllers.
        private void DrawViewReference()
        {
            if (!_showViewReference || _poseKind != PoseKind.HandGesture || !ReadyToEdit) return;
            if (_handAnchor == null || Event.current.type != EventType.Repaint) return;

            var forward = _handAnchor.forward;
            var up = _handAnchor.up;
            var right = _handAnchor.right;

            var midpoint = Vector3.zero;
            var handCount = 0;
            foreach (var hand in VisibleHands())
            {
                if (hand == null || !hand.gameObject.activeInHierarchy) continue;
                midpoint += hand.transform.position;
                handCount++;
            }
            midpoint = handCount > 0 ? midpoint / handCount : _handAnchor.position;

            // Resting controllers sit roughly 35 cm ahead of and 22 cm below the eyes.
            var head = midpoint - forward * 0.35f + up * 0.22f;
            var color = new Color(0.85f, 0.85f, 0.9f, 0.85f);
            Handles.color = color;
            Handles.DrawWireDisc(head, up, 0.09f);
            Handles.DrawWireDisc(head, right, 0.09f);
            Handles.DrawWireDisc(head, forward, 0.09f);
            Handles.ArrowHandleCap(0, head + forward * 0.09f, Quaternion.LookRotation(forward), 0.28f, EventType.Repaint);
            Handles.Label(head + up * 0.14f, "player view — thumbs up, palms facing each other");

            // Faint frustum: four lines to a small view rectangle ahead of the head.
            var frustumCenter = head + forward * 0.55f;
            var frustumRight = right * 0.3f;
            var frustumUp = up * 0.2f;
            var corners = new[]
            {
                frustumCenter - frustumRight - frustumUp, frustumCenter - frustumRight + frustumUp,
                frustumCenter + frustumRight + frustumUp, frustumCenter + frustumRight - frustumUp
            };
            var faint = new Color(color.r, color.g, color.b, 0.35f);
            Handles.color = faint;
            foreach (var corner in corners) Handles.DrawLine(head, corner);
            Handles.DrawSolidRectangleWithOutline(corners, new Color(0f, 0f, 0f, 0f), faint);
        }

        // Wireframes of the per-phalanx boxes the hand gets at runtime (the preview
        // hands themselves carry no colliders — this draws HandColliderBuilder's plan).
        private void DrawPlannedColliders(PreviewHand hand)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy || Event.current.type != EventType.Repaint) return;
            var color = new Color(0.25f, 1f, 0.45f, 0.85f);
            foreach (var box in HandColliderBuilder.PlanFingerBoxes(hand.transform))
            {
                using (new Handles.DrawingScope(color, box.Bone.localToWorldMatrix))
                {
                    Handles.DrawWireCube(box.Center, box.Size);
                }
            }
        }

        // Moving a hand (offset editing with the normal Move/Rotate tools) re-wraps the
        // fingers around the preview object when auto-pose-on-move is enabled.
        // Local space on purpose: the anchor following a moving OBJECT must not count
        // as the user moving the HAND.
        private void HandleHandMovement(PreviewHand hand)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy) return;
            var current = (hand.transform.localPosition, hand.transform.localRotation);
            if (_lastHandPose.TryGetValue(hand, out var last)
                && ((last.pos - current.localPosition).sqrMagnitude > 1e-8f || Quaternion.Angle(last.rot, current.localRotation) > 0.01f))
            {
                if (_autoPoseOnMove && _poseKind == PoseKind.HeldObject && _previewObject != null)
                {
                    Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Auto Pose (hand moved)");
                    PoseSceneTools.AutoPose(hand, _previewObject, 0.006f, _flexibility);
                }
                LiveSave();
            }
            _lastHandPose[hand] = current;
        }

        // Click a pink cube to select a fingertip; the selected one carries a full
        // position gizmo (axis arrows + two-axis plane squares, like a normal
        // GameObject) — screen-space free-dragging felt random.
        private void DrawFingertipTargets(PreviewHand hand)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy) return;
            foreach (var chain in PoseSceneTools.Chains(hand))
            {
                var tip = chain.Tip;
                if (tip == _selectedIkTip)
                {
                    EditorGUI.BeginChangeCheck();
                    _ikHandlePosition = Handles.PositionHandle(_ikHandlePosition, Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Fingertip IK");
                        PoseSceneTools.SolveChainToTarget(chain, _ikHandlePosition);
                        LiveSave();
                        Repaint();
                    }
                    Handles.color = new Color(1f, 0.3f, 0.8f, 0.9f);
                    Handles.DrawDottedLine(_ikHandlePosition, tip.position, 4f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        Handles.CubeHandleCap(0, tip.position, Quaternion.identity,
                            HandleUtility.GetHandleSize(tip.position) * 0.04f, EventType.Repaint);
                    }
                }
                else
                {
                    Handles.color = new Color(1f, 0.3f, 0.8f, _selectedIkTip == null ? 0.9f : 0.45f);
                    var size = HandleUtility.GetHandleSize(tip.position) * 0.06f;
                    if (Handles.Button(tip.position, Quaternion.identity, size, size, Handles.CubeHandleCap))
                    {
                        _selectedIkTip = tip;
                        _ikHandlePosition = tip.position;
                        _selectedJoint = null; // one active gizmo at a time
                        Repaint();
                    }
                }
            }
        }

        // The hand ROOT gets its own (orange, bigger) dot: selecting it shows a
        // combined move + rotate gizmo for offsetting the whole hand — the preview
        // hands are hidden from the hierarchy, so this is THE way to grab the root.
        private void DrawRootDot(PreviewHand hand)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy) return;
            var root = hand.transform;
            if (root == _selectedJoint) return;
            Handles.color = new Color(1f, 0.6f, 0.15f, 0.95f);
            var size = HandleUtility.GetHandleSize(root.position) * 0.1f;
            if (Handles.Button(root.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
            {
                _selectedJoint = root;
                _selectedIkTip = null;
                Repaint();
            }
        }

        private bool IsHandRoot(Transform candidate)
        {
            return handManager != null && handManager.HandsExist
                && (candidate == handManager.LeftHand.transform || candidate == handManager.RightHand.transform);
        }

        private void DrawJointDots(PreviewHand hand, SceneView sceneView)
        {
            if (hand == null || !hand.gameObject.activeInHierarchy || hand.Joints == null) return;
            var camera = sceneView != null ? sceneView.camera : null;
            if (camera == null) return;

            // Joints live INSIDE the flesh, so real depth-testing hides every dot
            // behind the hand surface. Depth is conveyed by grading instead: near
            // dots are big and opaque, far dots small and faint.
            var cameraPosition = camera.transform.position;
            float near = float.MaxValue, far = float.MinValue;
            foreach (var joint in hand.Joints)
            {
                if (joint == null) continue;
                var distance = (joint.position - cameraPosition).sqrMagnitude;
                near = Mathf.Min(near, distance);
                far = Mathf.Max(far, distance);
            }
            var range = Mathf.Max(far - near, 1e-6f);

            // While a joint is selected, only the dots UNDER its rotation rings lock
            // (grabbing a ring must not steal a neighbouring joint) — every other dot
            // stays clickable so switching joints is one click, no Esc needed.
            var handleCenterGui = Vector2.zero;
            var handleRadiusGui = 0f;
            if (_selectedJoint != null)
            {
                handleCenterGui = HandleUtility.WorldToGUIPoint(_selectedJoint.position);
                var handleEdge = _selectedJoint.position
                    + camera.transform.right * HandleUtility.GetHandleSize(_selectedJoint.position);
                handleRadiusGui = Vector2.Distance(handleCenterGui, HandleUtility.WorldToGUIPoint(handleEdge)) * 1.15f;
            }

            foreach (var joint in hand.Joints)
            {
                if (joint == null || joint == _selectedJoint) continue;
                var depth = ((joint.position - cameraPosition).sqrMagnitude - near) / range;
                // Constant SCREEN size (world-fixed dots ballooned when zooming in),
                // shrunk and faded with depth.
                var size = HandleUtility.GetHandleSize(joint.position) * Mathf.Lerp(0.07f, 0.042f, depth);
                var alpha = Mathf.Lerp(0.95f, 0.35f, depth);
                var lockedUnderHandle = _selectedJoint != null
                    && Vector2.Distance(HandleUtility.WorldToGUIPoint(joint.position), handleCenterGui) < handleRadiusGui;

                Handles.color = new Color(0.15f, 0.35f, 0.95f, lockedUnderHandle ? alpha * 0.25f : alpha);
                if (!lockedUnderHandle)
                {
                    if (Handles.Button(joint.position, Quaternion.identity, size, size, Handles.SphereHandleCap))
                    {
                        _selectedJoint = joint;
                        _selectedIkTip = null; // one active gizmo at a time
                        Repaint();
                    }
                }
                else if (Event.current.type == EventType.Repaint)
                {
                    Handles.SphereHandleCap(0, joint.position, Quaternion.identity, size, EventType.Repaint);
                }
            }
        }

        private void DrawJointRotationHandle()
        {
            var isRoot = IsHandRoot(_selectedJoint);
            var size = HandleUtility.GetHandleSize(_selectedJoint.position) * 0.06f;
            Handles.color = new Color(1f, 0.85f, 0.25f, 1f);
            Handles.SphereHandleCap(0, _selectedJoint.position, Quaternion.identity, size, EventType.Repaint);
            Handles.Label(_selectedJoint.position + Vector3.up * size * 3f,
                isRoot ? "hand root (move + rotate)" : _selectedJoint.name);

            // The root is the only transform allowed to MOVE (joints only rotate —
            // bone lengths are enforced): it gets the full position gizmo too.
            if (isRoot)
            {
                EditorGUI.BeginChangeCheck();
                var position = Handles.PositionHandle(_selectedJoint.position, _selectedJoint.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_selectedJoint, "Move Hand Root");
                    _selectedJoint.position = position;
                    LiveSave();
                    Repaint();
                }
            }

            EditorGUI.BeginChangeCheck();
            var rotation = Handles.RotationHandle(_selectedJoint.rotation, _selectedJoint.position);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_selectedJoint, isRoot ? "Rotate Hand Root" : "Rotate Hand Joint");
                _selectedJoint.rotation = rotation;
                LiveSave();
                Repaint();
            }
        }

        // ---------------------------------------------------------------- window GUI

        private void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            if (handManager == null)
            {
                EditorGUILayout.HelpBox("Pose helper could not be created (see console).", MessageType.Error);
                if (GUILayout.Button("Retry")) CreatePoseHelper();
                return;
            }

            // Vertical scrolling only — fixed-width children used to force a horizontal bar.
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUIStyle.none, GUI.skin.verticalScrollbar);

            DrawPoseStep();
            EditorGUILayout.Space(10);
            DrawTargetStep();
            EditorGUILayout.Space(10);
            DrawEditStep();
            EditorGUILayout.Space(10);
            DrawSaveStep();
            EditorGUILayout.Space(14);
            DrawResetFooter();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPoseStep()
        {
            EditorGUILayout.LabelField("1 — Pose", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                var newIndex = EditorGUILayout.Popup(_poseBrowserIndex, _allPoseNames);
                using (new EditorGUI.DisabledScope(_allPoses.Length == 0))
                {
                    if (GUILayout.Button("<", GUILayout.Width(24f)))
                        newIndex = newIndex <= 1 ? _allPoses.Length : newIndex - 1;
                    if (GUILayout.Button(">", GUILayout.Width(24f)))
                        newIndex = newIndex >= _allPoses.Length ? 1 : newIndex + 1;
                }
                if (GUILayout.Button("Refresh", GUILayout.Width(60f)))
                {
                    RefreshPoseList();
                    GUIUtility.ExitGUI();
                }

                if (newIndex != _poseBrowserIndex)
                {
                    _poseBrowserIndex = newIndex;
                    activePose = newIndex > 0 ? _allPoses[newIndex - 1] : null;
                    PlaceHands();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create new pose...")) CreateNewPose();
                using (new EditorGUI.DisabledScope(activePose == null))
                {
                    if (GUILayout.Button("Duplicate as...")) DuplicateActivePose();
                }
            }
        }

        private void DrawTargetStep()
        {
            using (new EditorGUI.DisabledScope(activePose == null))
            {
                EditorGUILayout.LabelField("2 — What is this pose for?", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                _poseKind = (PoseKind)GUILayout.Toolbar((int)_poseKind,
                    new[] { "Holding an object", "Hand gesture (no object)" });
                var changed = EditorGUI.EndChangeCheck();

                if (_poseKind == PoseKind.HeldObject)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("3 — Object", EditorStyles.boldLabel);
                    EditorGUI.BeginChangeCheck();
                    _previewObject = (GameObject)EditorGUILayout.ObjectField("Held object", _previewObject, typeof(GameObject), true);
                    changed |= EditorGUI.EndChangeCheck();

                    if (_previewObject == null)
                        EditorGUILayout.HelpBox("Assign the scene object the hands should hold — they spawn on it " +
                            "automatically. A scaled placeholder is fine: the hands keep their real size.", MessageType.Info);
                    else
                        DrawPoseContainerAssign();
                }

                if (changed) PlaceHands();
            }
        }

        // Offered only when it applies: put the edited pose on the preview object's
        // PoseContainer so grabbing it in-game uses this pose.
        private void DrawPoseContainerAssign()
        {
            if (activePose == null || _previewObject == null) return;
            if (!_previewObject.TryGetComponent(out PoseContainer container) || container.pose == activePose) return;

            if (GUILayout.Button($"Use '{activePose.name}' as this object's grab pose (PoseContainer)"))
            {
                Undo.RecordObject(container, "Assign Pose To Container");
                container.pose = activePose;
                EditorUtility.SetDirty(container);
                if (_previewObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(_previewObject.scene);
            }
        }

        private void DrawEditStep()
        {
            EditorGUILayout.LabelField("4 — Edit (in the Scene view)", EditorStyles.boldLabel);

            if (!ReadyToEdit)
            {
                EditorGUILayout.HelpBox(activePose == null
                        ? "Pick or create a pose first."
                        : "Assign the held object above — the hands spawn automatically.",
                    MessageType.None);
                return;
            }

            EditorGUILayout.HelpBox(_selectedJoint != null
                    ? (IsHandRoot(_selectedJoint)
                        ? "Offsetting the hand root (move + rotate gizmo) — Esc deselects."
                        : $"Rotating joint '{_selectedJoint.name}' — click any dot outside the rings to switch " +
                          "(dots under the rings are locked); Esc deselects.")
                    : "Orange dot = hand root (move + rotate). Blue dots = joints (rotate). " +
                      "Pink cubes = fingertip IK targets (full move gizmo). Esc deselects.",
                MessageType.None);
            if ((_selectedJoint != null || _selectedIkTip != null) && GUILayout.Button("Deselect"))
            {
                _selectedJoint = null;
                _selectedIkTip = null;
            }

            var newSide = (EditedSide)GUILayout.Toolbar((int)_editedSide, new[] { "Both hands", "Left only", "Right only" });
            if (newSide != _editedSide)
            {
                _editedSide = newSide;
                ApplySideVisibility();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mirror plane", GUILayout.Width(80f));
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(_previewObject == null))
                {
                    // Without an object every mode means "between the hands".
                    var shownMode = _previewObject != null ? _mirrorPlaneMode : MirrorPlaneMode.BetweenHands;
                    var newMode = (MirrorPlaneMode)EditorGUILayout.Popup((int)shownMode, MirrorPlaneLabels);
                    if (_previewObject != null) _mirrorPlaneMode = newMode;
                }
                _showMirrorPlane = GUILayout.Toggle(_showMirrorPlane, "Show", GUI.skin.button, GUILayout.Width(50f));
                if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mirror left → right")) MirrorPose(handManager.LeftHand, handManager.RightHand);
                if (GUILayout.Button("Mirror right → left")) MirrorPose(handManager.RightHand, handManager.LeftHand);
            }

            // Gesture presets: one-click starting poses, refined afterwards by hand.
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Gesture", GUILayout.Width(80f));
                var names = new string[HandGesturePresets.All.Length];
                for (var i = 0; i < names.Length; i++) names[i] = HandGesturePresets.All[i].Name;
                _gestureIndex = EditorGUILayout.Popup(_gestureIndex, names);
                if (GUILayout.Button(new GUIContent("Apply", HandGesturePresets.All[_gestureIndex].Tooltip), GUILayout.Width(70f)))
                    ApplyGestureToVisibleHands(HandGesturePresets.All[_gestureIndex]);
            }
            EditorGUILayout.LabelField(HandGesturePresets.All[_gestureIndex].Tooltip, EditorStyles.miniLabel);

            EditorGUILayout.LabelField(new GUIContent("Flexibility",
                "Stiff (crisp presets, tighter ROM) → Loose (fingers reach further and drag their neighbours)."));
            _flexibility = EditorGUILayout.Slider(_flexibility, 0f, 1f);

            if (_poseKind == PoseKind.HeldObject)
            {
                if (GUILayout.Button("Auto-pose fingers on the object")) AutoPoseVisibleHands();
                _autoPoseOnMove = EditorGUILayout.ToggleLeft("Re-wrap fingers while moving a hand", _autoPoseOnMove);
            }
            _ikTargets = EditorGUILayout.ToggleLeft("Fingertip IK targets (drag the pink cubes)", _ikTargets);
            EditorGUI.BeginChangeCheck();
            _showControllers = EditorGUILayout.ToggleLeft("Show VR controllers (how the pose sits on the controller)", _showControllers);
            if (EditorGUI.EndChangeCheck()) SyncControllerVisuals();
            EditorGUI.BeginChangeCheck();
            _showColliders = EditorGUILayout.ToggleLeft("Show finger colliders (the boxes the hand gets at runtime)", _showColliders);
            if (_poseKind == PoseKind.HandGesture)
                _showViewReference = EditorGUILayout.ToggleLeft("Show player view (head + forward — thumbs point up)", _showViewReference);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            if (_showColliders && GUILayout.Button("Bake finger colliders into the hand prefabs..."))
                BakeFingerCollidersIntoPrefabs();
            EditorGUI.BeginChangeCheck();
            _isolate = EditorGUILayout.ToggleLeft("Hide the world while editing (prefab-mode style)", _isolate);
            if (EditorGUI.EndChangeCheck()) ApplyIsolation();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Revert hands to saved pose")) RevertVisibleHands();
                if (GUILayout.Button("Reset fingers to default")) ResetVisibleHands();
            }

            if (GUILayout.Button("Frame hands in Scene view")) FrameHandsInSceneView();
        }

        private void DrawSaveStep()
        {
            EditorGUILayout.LabelField("5 — Save", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!ReadyToEdit))
            {
                _liveSave = EditorGUILayout.ToggleLeft(
                    activePose != null ? $"Save every change into '{activePose.name}' automatically" : "Save every change automatically",
                    _liveSave);

                if (!_liveSave && GUILayout.Button(activePose != null ? $"Save to '{activePose.name}'" : "Save"))
                {
                    handManager.SavePose(activePose);
                    AssetDatabase.SaveAssets();
                }

                _advanced = EditorGUILayout.Foldout(_advanced, "Advanced", true);
                if (_advanced)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.LabelField("Mix: apply another pose's fingers to one hand, then save.", EditorStyles.miniLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _mixPose = (Pose)EditorGUILayout.ObjectField(_mixPose, typeof(Pose), false);
                            using (new EditorGUI.DisabledScope(_mixPose == null))
                            {
                                if (GUILayout.Button("→ Left", GUILayout.Width(64f))) MixIntoHand(handManager.LeftHand);
                                if (GUILayout.Button("→ Right", GUILayout.Width(64f))) MixIntoHand(handManager.RightHand);
                            }
                        }

                        EditorGUILayout.LabelField("Overwrite a DIFFERENT pose asset with the current hands.", EditorStyles.miniLabel);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            _overwriteTarget = (Pose)EditorGUILayout.ObjectField(_overwriteTarget, typeof(Pose), false);
                            using (new EditorGUI.DisabledScope(_overwriteTarget == null))
                            {
                                if (GUILayout.Button("Overwrite", GUILayout.Width(80f))) SaveIntoExistingPose();
                            }
                        }
                    }
                }
            }
        }

        private void DrawResetFooter()
        {
            if (GUILayout.Button("Reset editor (respawn hands)")) ResetEditor();
            EditorGUILayout.LabelField("Recovers from any broken state — keeps the pose and object selection.", EditorStyles.miniLabel);
        }

        // ---------------------------------------------------------------- pose actions

        private void RefreshPoseList()
        {
            _allPoses = AssetDatabase.FindAssets("t:Pose")
                .Select(guid => AssetDatabase.LoadAssetAtPath<Pose>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(pose => pose != null)
                .OrderBy(pose => pose.name)
                .ToArray();

            _allPoseNames = new string[_allPoses.Length + 1];
            _allPoseNames[0] = "(select or create a pose)";
            for (var i = 0; i < _allPoses.Length; i++) _allPoseNames[i + 1] = _allPoses[i].name;

            var index = System.Array.IndexOf(_allPoses, activePose);
            _poseBrowserIndex = index >= 0 ? index + 1 : 0;
        }

        private void SelectPose(Pose pose)
        {
            activePose = pose;
            RefreshPoseList();
            PlaceHands();
            Repaint();
        }

        private void CreateNewPose()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create pose", "NewPose", "asset",
                "Where to store the new Pose asset.");
            if (string.IsNullOrEmpty(path)) return;

            var pose = CreateInstance<Pose>();
            AssetDatabase.CreateAsset(pose, path);

            // A pose needs valid joint data from the start: seed it with the default hand pose.
            if (handManager != null && handManager.HandsExist)
            {
                handManager.LeftHand.ApplyDefaultPoseForeSetup();
                handManager.RightHand.ApplyDefaultPoseForeSetup();
                handManager.SavePose(pose);
            }
            AssetDatabase.SaveAssets();

            SelectPose(pose);
            EditorGUIUtility.PingObject(pose);
        }

        private void DuplicateActivePose()
        {
            if (activePose == null) return;
            var path = EditorUtility.SaveFilePanelInProject("Duplicate pose as...", $"{activePose.name}_Copy", "asset",
                $"Where to store the copy of '{activePose.name}' — editing continues on the copy.");
            if (string.IsNullOrEmpty(path)) return;

            var copy = CreateInstance<Pose>();
            copy.leftHandInfo = CloneHandInfo(activePose.leftHandInfo);
            copy.rightHandInfo = CloneHandInfo(activePose.rightHandInfo);
            AssetDatabase.CreateAsset(copy, path);
            // Unsaved live edits carry over into the copy (the original stays as saved).
            if (ReadyToEdit) handManager.SavePose(copy);
            AssetDatabase.SaveAssets();

            activePose = copy;
            RefreshPoseList();
            EditorGUIUtility.PingObject(copy);
        }

        private static HandInfo CloneHandInfo(HandInfo source)
        {
            if (source == null) return HandInfo.Empty;
            return new HandInfo
            {
                attachPosition = source.attachPosition,
                attachRotation = source.attachRotation,
                fingerRotations = new List<Quaternion>(source.fingerRotations)
            };
        }

        private void SaveIntoExistingPose()
        {
            if (_overwriteTarget == null) return;
            if (!EditorUtility.DisplayDialog("Overwrite pose?",
                    $"Replace BOTH hands of '{_overwriteTarget.name}' with the current preview hands? " +
                    "Every object using this pose asset changes with it.",
                    "Overwrite", "Cancel")) return;

            handManager.SavePose(_overwriteTarget);
            AssetDatabase.SaveAssets();
            SelectPose(_overwriteTarget);
            EditorGUIUtility.PingObject(_overwriteTarget);
        }

        private void LiveSave()
        {
            if (!_liveSave || activePose == null || !ReadyToEdit) return;
            // The asset joins the undo group of the edit that triggered the save, so
            // Ctrl+Z reverts the hands AND the pose data together.
            Undo.RecordObject(activePose, "Edit Pose");
            handManager.SavePose(activePose);
        }

        // ---------------------------------------------------------------- edit actions

        private void ApplyGestureToVisibleHands(HandGesturePresets.Gesture gesture)
        {
            foreach (var hand in VisibleHands())
            {
                Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, $"Gesture: {gesture.Name}");
                HandGesturePresets.Apply(hand, gesture, _flexibility);
            }
            LiveSave();
            Repaint();
        }

        private void AutoPoseVisibleHands()
        {
            if (_previewObject == null) return;
            foreach (var hand in VisibleHands())
            {
                Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Auto Pose");
                PoseSceneTools.AutoPose(hand, _previewObject, 0.006f, _flexibility);
            }
            LiveSave();
        }

        private void RevertVisibleHands()
        {
            foreach (var hand in VisibleHands())
            {
                Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Revert To Saved Pose");
                hand.ApplyPoseForSetup(activePose);
            }
        }

        private void ResetVisibleHands()
        {
            foreach (var hand in VisibleHands())
            {
                Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Reset Fingers");
                hand.ApplyDefaultPoseForeSetup();
            }
            LiveSave();
        }

        private void MirrorPose(PreviewHand sourceHand, PreviewHand targetHand)
        {
            if (!TryGetMirrorPlane(out var planePoint, out var planeNormal)) return;
            Undo.RegisterFullObjectHierarchyUndo(targetHand.gameObject, "Mirror Pose");
            // Mirroring into a hidden hand looks like nothing happened — show both.
            if (!targetHand.gameObject.activeSelf)
            {
                _editedSide = EditedSide.Both;
                ApplySideVisibility();
            }
            targetHand.MirrorAndApplyPose(sourceHand, planePoint, planeNormal);
            LiveSave();
        }

        // The active mirror plane: an axis plane through the object's bounds center,
        // or the perpendicular bisector of the two hand roots. Object modes fall
        // back to between-hands when no object is assigned (gesture poses).
        private bool TryGetMirrorPlane(out Vector3 point, out Vector3 normal)
        {
            if (_mirrorPlaneMode != MirrorPlaneMode.BetweenHands && _previewObject != null)
            {
                point = ObjectBoundsCenter();
                normal = _mirrorPlaneMode == MirrorPlaneMode.ObjectX ? _previewObject.transform.right
                    : _mirrorPlaneMode == MirrorPlaneMode.ObjectY ? _previewObject.transform.up
                    : _previewObject.transform.forward;
                return true;
            }

            point = Vector3.zero;
            normal = Vector3.zero;
            if (handManager == null || !handManager.HandsExist) return false;
            var left = handManager.LeftHand.transform.position;
            var right = handManager.RightHand.transform.position;
            var separation = right - left;
            if (separation.sqrMagnitude > 1e-8f)
            {
                point = (left + right) * 0.5f;
                normal = separation.normalized;
            }
            else
            {
                point = _handAnchor != null ? _handAnchor.position : left;
                normal = _handAnchor != null ? _handAnchor.right : Vector3.right;
            }
            return true;
        }

        private Vector3 ObjectBoundsCenter()
        {
            var renderers = _previewObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return _previewObject.transform.position;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        private void DrawMirrorPlane()
        {
            if (!_showMirrorPlane || !ReadyToEdit || Event.current.type != EventType.Repaint) return;
            if (!TryGetMirrorPlane(out var point, out var normal)) return;

            var extent = 0.18f;
            if (_previewObject != null)
            {
                var renderers = _previewObject.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    extent = Mathf.Max(extent, bounds.extents.magnitude * 1.1f);
                }
            }

            var rotation = Quaternion.LookRotation(normal);
            var right = rotation * Vector3.right * extent;
            var up = rotation * Vector3.up * extent;
            var corners = new[] { point - right - up, point - right + up, point + right + up, point + right - up };
            Handles.DrawSolidRectangleWithOutline(corners,
                new Color(0.4f, 0.7f, 1f, 0.06f), new Color(0.4f, 0.7f, 1f, 0.75f));
            Handles.color = new Color(0.4f, 0.7f, 1f, 0.9f);
            Handles.ArrowHandleCap(0, point, rotation, extent * 0.35f, EventType.Repaint);
        }

        private void MixIntoHand(PreviewHand hand)
        {
            if (hand == null || _mixPose == null) return;
            var info = _mixPose.GetHandInfo(hand.HandType);
            if (info == null) return;
            Undo.RegisterFullObjectHierarchyUndo(hand.gameObject, "Mix Pose Into Hand");
            hand.gameObject.SetActive(true);
            // Fingers only (the hand keeps its placement), instantly — LiveSave
            // reads the joints right after, it must not race a tween.
            hand.ApplyFingerRotations(info.fingerRotations, true);
            LiveSave();
        }

        private void FrameHandsInSceneView()
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;
            if (_poseKind == PoseKind.HandGesture && _handAnchor != null)
                _handAnchor.SetPositionAndRotation(sceneView.pivot, Quaternion.identity);
            var center = _poseKind == PoseKind.HeldObject && _previewObject != null
                ? _previewObject.transform.position
                : _handAnchor != null ? _handAnchor.position : sceneView.pivot;
            sceneView.Frame(new Bounds(center, Vector3.one * 0.6f), false);
        }

        /// <summary>
        /// Bakes the per-phalanx BoxColliders into every hand prefab in Assets/
        /// (a hand = a prefab containing BlendableHand), replacing its bind-pose
        /// MeshCollider. Idempotent — already-baked bones are skipped, and the
        /// runtime builder (BlendableHand.Awake) then finds nothing left to do.
        /// </summary>
        private void BakeFingerCollidersIntoPrefabs()
        {
            var candidates = new List<string>();
            try
            {
                var guids = AssetDatabase.FindAssets("t:Prefab");
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.StartsWith("Assets/")) continue; // immutable package content can't be saved
                    EditorUtility.DisplayProgressBar("Bake finger colliders", $"Scanning prefabs... {path}", (float)i / guids.Length);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null && prefab.GetComponentInChildren<BlendableHand>(true) != null)
                        candidates.Add(path);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (candidates.Count == 0)
            {
                EditorUtility.DisplayDialog("Bake finger colliders",
                    "No hand prefabs found (a hand is a prefab containing BlendableHand).", "OK");
                return;
            }

            // Leaf hand prefabs first: prefabs that NEST them (e.g. Player) then see
            // the boxes inherited from the source and skip, instead of duplicating
            // them as instance overrides.
            candidates = candidates.OrderBy(path => AssetDatabase.GetDependencies(path, true).Length).ToList();

            var fileNames = string.Join("\n", candidates.Select(System.IO.Path.GetFileName));
            if (!EditorUtility.DisplayDialog("Bake finger colliders?",
                    "Replace the MeshCollider with per-phalanx BoxColliders in:\n\n" + fileNames +
                    "\n\nAlready-baked hands are skipped.", "Bake", "Cancel")) return;

            var totalBoxes = 0;
            var changedPrefabs = 0;
            foreach (var path in candidates)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var hadMeshColliders = root.GetComponentsInChildren<MeshCollider>(true).Length > 0;
                    var built = 0;
                    foreach (var hand in root.GetComponentsInChildren<BlendableHand>(true))
                    {
                        var meshRenderer = hand.GetComponent<SkinnedMeshRenderer>() != null
                            ? hand.GetComponent<SkinnedMeshRenderer>()
                            : hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
                        var bonesRoot = meshRenderer != null && meshRenderer.rootBone != null
                            ? meshRenderer.rootBone
                            : hand.transform;
                        built += HandColliderBuilder.ReplaceWithFingerBoxes(hand.transform, bonesRoot);
                    }
                    if (built > 0 || hadMeshColliders)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        changedPrefabs++;
                        totalBoxes += built;
                        Debug.Log($"Bake finger colliders: '{path}' — {built} box(es) added" +
                            (hadMeshColliders ? ", MeshCollider removed." : "."));
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Bake finger colliders",
                $"{changedPrefabs} prefab(s) updated, {totalBoxes} box collider(s) baked.", "OK");
        }

        private void ResetEditor()
        {
            _selectedJoint = null;
            _selectedIkTip = null;
            _handsPlaced = false;
            DestroyPoseHelper();
            DestroyAnchor();
            CreatePoseHelper();
            PlaceHands();
        }

        // ---------------------------------------------------------------- lifecycle

        private void CloseWindow(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode)
                Close();
        }

        private void CloseWindow(Scene scene, bool removingScene)
        {
            if (removingScene)
                Close();
        }
    }
#endif

}
