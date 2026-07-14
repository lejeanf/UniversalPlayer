using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using jeanf.EventSystem;
using Debug = UnityEngine.Debug;
using jeanf.propertyDrawer;
using jeanf.validationTools;
using LitMotion;
using UnityEngine.UIElements;
using System;

namespace jeanf.universalplayer
{
    public class TakeObject : MonoBehaviour, IDebugBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        #region variables
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        //Camera
        [Validation("Main camera is required — the desktop grab raycast is cast from it. Object pickup is dead without it.")]
        [SerializeField] Camera mainCamera;

        //TakeObject
        Transform objectInHandTransform; //Really useful ?

        public PickableObject _objectInHand { get { return objectInHand; } set { objectInHand = value; } }

        //InputActions
        [Validation("Take action is required — it is the desktop grab/drop button. Object pickup is dead without it.")]
        [SerializeField] InputActionReference takeAction;
        [SerializeField] InputActionReference scrollAction;

        //Object Movement
        [Space(20)]
        // Read only by the DrawIf inspector attributes below, never by code.
#pragma warning disable 0414
        [SerializeField] private bool advancedSettings = false;
#pragma warning restore 0414
        private float objectDistance = .5f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(.1f, .9f)]
        [SerializeField]
        private float minDistance = .5f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(1f, 2f)]
        [SerializeField]
        private float maxDistance = 1.25f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(.0001f, 0.1f)]
        [SerializeField]
        private float scrollStep = .001f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(.5f, 10f)]
        [SerializeField] private float maxDistanceCheck = 2f;
        private MotionHandle _positionHandle;
        private MotionHandle _rotationHandle;
      

        [SerializeField] private LayerMask layerMask;
        int roomId;

        [Range(0.01f, 0.5f)]
        [SerializeField] private float sliderMotionDuration;

        //Taken Object status channels
        [Header("Broadcasting On")]
        [SerializeField] GameObjectEventChannelSO objectDropped;
        [SerializeField] GameObjectIntBoolEventChannelSO objectTakenChannel;
        public static event Action<bool, HandType> OnGrabDeactivateCollider;
        public static event Action<string> OnVrGrabSwapPrimaryItem;
        [Header("Listening On")]
        [SerializeField] IntEventChannelSO roomIdChannelSO;
        [SerializeField] GameObjectEventChannelSO snapEventChannelSO;

        [Header("XR")]
        [SerializeField] NearFarInteractor rightInteractor;
        [SerializeField] NearFarInteractor leftInteractor;

        [Header("Item anchors")]
        [Tooltip("Resolves where a held item docks (camera / right / left hand). Left empty = found on the rig. Without it, items fall back to the legacy camera hold.")]
        [SerializeField] private PlayerItemAnchors itemAnchors;
        private bool anchorsSearched;
        [Tooltip("Used to equip a picked-up item whose Carry Slot is Primary (the tablet). Left empty = found on the rig.")]
        [SerializeField] private PrimaryItemController primaryItemController;
        private bool primaryItemControllerSearched;
        // Which hand the desktop-held item ended up in — the old code had no per-object
        // record of the side at all (only three separate fields that lost it).
        private HandType _heldHand = HandType.None;

        [Header("Objects in players's hand")]
        PickableObject objectRightHand;
        PickableObject objectLeftHand;
        PickableObject objectInHand;
        // The Primary-slot item we carry — remembered even while HOLSTERED, which is how
        // the draw binding can bring it back after it has been put away.
        PickableObject carriedPrimary;

        // Telemetry for the F8 overlay — pickup fails silently in four different ways,
        // so the overlay reports the whole chain instead of leaving "nothing happens".
        internal InputAction DebugTakeAction => takeAction != null ? takeAction.action : null;
        internal LayerMask DebugLayerMask => layerMask;
        internal float DebugMaxDistance => maxDistanceCheck;
        internal Camera DebugCamera => mainCamera;
        internal PickableObject DebugObjectInHand => objectInHand;
        internal bool DebugUiOwnsPress => UiOwnsThePress();

        bool objectIsSnapping;
        #endregion

        #region Default MonoBehaviour Methods

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();


        private void Subscribe()
        {
            if (takeAction) takeAction.action.performed += ctx => DispatchAction();
            else
                Debug.LogWarning($"{LogPrefix} TakeObject on '{name}': the Take action is not assigned — nothing can " +
                    "EVER be picked up (no press ever reaches this component). Wire FPS/TakeObject on the Player prefab.", this);
            if(scrollAction) scrollAction.action.performed += ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            try
            {
                roomIdChannelSO.OnEventRaised += AssignRoomId;
            }
            catch { }
            SnapObject.OnSnapMove += SetObjectPosition;
            SnapObject.OnSnap += UpdateSnapStatus;
            SnapObject.OnSnapRotate += SetObjectRotation;
            PrimaryItemController.PrimaryItemStateChanged += OnPrimaryItemStateChanged;
        }

        private void Unsubscribe()
        {
            if(takeAction) takeAction.action.performed -= ctx => DispatchAction();
            if(scrollAction) scrollAction.action.performed -= ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            try
            {
                roomIdChannelSO.OnEventRaised -= AssignRoomId;
            }
            catch { }
            DisablePositionHandle();
            DisableRotationHandle();
            SnapObject.OnSnapMove -= SetObjectPosition;
            SnapObject.OnSnap -= UpdateSnapStatus;
            SnapObject.OnSnapRotate -= SetObjectRotation;
            PrimaryItemController.PrimaryItemStateChanged -= OnPrimaryItemStateChanged;

        }
        #endregion

        #region Take Handling methods
        //Check, when received input action for take, if there's already an object in hand or not
        private void DispatchAction()
        {
            // The first thing to confirm when "nothing happens": did the press even get
            // here? If this line never prints, the problem is the ACTION (unassigned,
            // disabled, or scheme-masked), not the pickup logic below.
            if (_isDebug) Debug.Log($"{LogPrefix} take/drop pressed (holding: {(objectInHand != null ? objectInHand.name : "nothing")})", this);

            if (objectInHand == null)
            {
                Take();
            }
            else
            {
                // A CARRIED (slotted) item is put away with its DRAW binding only —
                // 1 / dpad-up. Take/Interact must never drop it, because that same
                // button is what clicks the tablet's UI: dropping the tablet every
                // time you press a button on it is unusable.
                if (objectInHand.IsCarried)
                {
                    if (_isDebug) Debug.Log($"{LogPrefix} '{objectInHand.name}' is a carried item — " +
                        "press its draw binding (1 / dpad-up) to put it away, not Take.", this);
                    return;
                }

                // An ordinary held object still drops on the take button, but not while
                // the press is aimed at world UI.
                if (DesktopWorldUiInteractor.UiHoverActive) return;
                Release();
            }
        }

        /// <summary>
        /// True when world-space UI should swallow this press. UI that IS the face of a
        /// pickable does NOT swallow it: a tablet's screen is a world canvas, and it has
        /// to be pickable off the table before it can ever be used. Only UI that belongs
        /// to nothing takeable (a wall panel in front of a crate) wins over the grab.
        /// </summary>
        private static bool UiOwnsThePress()
        {
            if (!DesktopWorldUiInteractor.UiHoverActive) return false;
            var hovered = DesktopWorldUiInteractor.UiHoverTarget;
            return hovered == null || hovered.GetComponentInParent<PickableObject>() == null;
        }

        //Checks for raycast hit, if object is pickable then pick it
        private void Take()
        {
            if (UiOwnsThePress())
            {
                if (_isDebug) Debug.Log($"{LogPrefix} take blocked: the reticle is on world UI that belongs to nothing " +
                    $"takeable ('{DesktopWorldUiInteractor.UiHoverTarget?.name}') — that press belongs to the UI.", this);
                return;
            }

            if (mainCamera == null)
            {
                Debug.LogWarning($"{LogPrefix} TakeObject on '{name}': Main Camera is not assigned — the grab raycast " +
                    "is cast from it, so pickup is dead. Wire it on the Player prefab.", this);
                return;
            }

            RaycastHit hit;
            // New Input System only — Input.mousePosition throws when the legacy
            // Input Manager is disabled. No mouse (gamepad/VR) = screen centre,
            // which is where the locked cursor sits anyway.
            var pointer = Mouse.current != null
                ? (Vector3)Mouse.current.position.ReadValue()
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            Ray ray = mainCamera.ScreenPointToRay(pointer);

            if (!Physics.Raycast(ray, out hit, maxDistanceCheck, layerMask))
            {
                if (_isDebug) DiagnoseFailedTake(ray, null);
                return;
            }

            // GetComponentInParent, NOT GetComponent: a tablet's collider usually sits on
            // a child mesh while PickableObject lives on the root, and the old lookup then
            // found nothing and failed silently. This also matches how ReticleHoverFeedback
            // and FingerPointingRay detect a pickable — so what the reticle highlights is
            // exactly what can be taken.
            var pickable = hit.collider.GetComponentInParent<PickableObject>();
            if (pickable == null)
            {
                if (_isDebug) DiagnoseFailedTake(ray, hit.collider);
                return;
            }

            if (_isDebug) Debug.Log($"{LogPrefix} taking '{pickable.name}' (slot: {pickable.Slot}, anchor: {pickable.Anchor})", pickable);

            objectInHand = pickable;
            AttachHeld(pickable, HandType.None); // desktop: no grabbing hand — the item's own anchor decides
            objectTakenChannel?.RaiseEvent(hit.transform.gameObject, roomId, true);

            // A slotted item STAYS WITH THE PLAYER once picked. Primary is the tablet's
            // slot: equip it through the state every other system already listens to
            // (cursor, look, tooltips) rather than inventing a parallel path. Remember
            // it, so the draw binding can stow it and bring it back later.
            if (pickable.Slot == CarrySlot.Primary)
            {
                // The slot holds exactly ONE item: whatever was in it goes back to the
                // world (per ITS release target) before the new one moves in. Without
                // this the old tablet is simply forgotten — still parented to the camera
                // and invisible if it was holstered.
                EvictCarriedPrimary(pickable);
                carriedPrimary = pickable;
                ResolvePrimaryItemController()?.SetState(true);
            }
        }

        /// <summary>
        /// Says WHY a take did nothing — every gate in Take() is otherwise silent, which
        /// is exactly what makes "I click the tablet and nothing happens" unfalsifiable.
        /// Tick Is Debug on TakeObject to get this in the console.
        /// </summary>
        private void DiagnoseFailedTake(Ray ray, Collider hitCollider)
        {
            if (layerMask.value == 0)
            {
                Debug.LogWarning($"{LogPrefix} TakeObject: the Layer Mask is 'Nothing', so the grab raycast can never " +
                    "hit anything. Set it to the layers your pickable objects live on.", this);
                return;
            }

            if (hitCollider != null)
            {
                Debug.Log($"{LogPrefix} take failed: the ray hit '{hitCollider.name}' (layer " +
                    $"'{LayerMask.LayerToName(hitCollider.gameObject.layer)}') but there is no PickableObject on it or " +
                    "any of its PARENTS. Put PickableObject on the object the collider belongs to (or a parent of it).", hitCollider);
                return;
            }

            // Nothing hit within the mask+range. Re-cast unmasked to say what IS in front,
            // which turns "nothing happens" into a specific, fixable cause.
            if (!Physics.Raycast(ray, out var any, 25f, ~0, QueryTriggerInteraction.Collide))
            {
                Debug.Log($"{LogPrefix} take failed: no collider at all within 25m of the reticle. Does the tablet have " +
                    "a Collider? (A canvas on its own is not hittable by a physics raycast.)", this);
                return;
            }

            var pickable = any.collider.GetComponentInParent<PickableObject>();
            var layer = any.collider.gameObject.layer;
            var inMask = (layerMask.value & (1 << layer)) != 0;
            Debug.Log($"{LogPrefix} take failed. Nearest collider ahead: '{any.collider.name}' at {any.distance:F2}m, " +
                $"layer '{LayerMask.LayerToName(layer)}'. PickableObject found: {(pickable != null ? $"YES ('{pickable.name}')" : "NO")}. " +
                $"Layer in TakeObject's mask: {(inMask ? "YES" : "NO -> ADD IT")}. " +
                $"Within range: {(any.distance <= maxDistanceCheck ? "YES" : $"NO -> it is {any.distance:F2}m away but Max Distance Check is {maxDistanceCheck}m")}.",
                any.collider);
        }

        /// <summary>
        /// The draw binding (1 / dpad-up) toggles the primary item — this is what makes
        /// it actually go away and come back for a plain PickableObject. Without it, the
        /// press flipped the state but the tablet stayed stuck to the camera.
        ///
        /// An item carrying a PrimaryItemBehaviour already owns its own placement and
        /// visibility, so we do NOT touch its transform — we would only fight it.
        /// </summary>
        private void OnPrimaryItemStateChanged(bool drawn)
        {
            if (carriedPrimary == null) return;

            if (carriedPrimary.GetComponent<PrimaryItemBehaviour>() != null)
            {
                objectInHand = drawn ? carriedPrimary : null; // it places itself; just track the hand
                return;
            }

            if (drawn) DrawCarried(carriedPrimary);
            else HolsterCarried(carriedPrimary);
        }

        /// <summary>
        /// The Primary slot holds one item. Taking a new one returns the previous
        /// occupant to the world using its OWN release target (Original Spot by default,
        /// so it goes back where it came from), whether it was drawn or holstered.
        /// </summary>
        private void EvictCarriedPrimary(PickableObject replacement)
        {
            if (carriedPrimary == null || carriedPrimary == replacement) return;

            var previous = carriedPrimary;
            carriedPrimary = null;
            if (objectInHand == previous) objectInHand = null;

            // Its own behaviour owns its placement — don't fight it, just let go.
            if (previous.GetComponent<PrimaryItemBehaviour>() != null)
            {
                if (_isDebug) Debug.Log($"{LogPrefix} primary slot: '{previous.name}' replaced by '{replacement.name}' " +
                    "(its PrimaryItemBehaviour owns where it goes).", previous);
                return;
            }

            ReturnToWorld(previous);
            if (_isDebug) Debug.Log($"{LogPrefix} primary slot: '{previous.name}' returned to the world, " +
                $"replaced by '{replacement.name}' ({previous.ReleaseMode}).", previous);
        }

        /// <summary>Stow a carried item: send it home, hide it, and let go of it.</summary>
        private void HolsterCarried(PickableObject pickable)
        {
            if (objectInHand == pickable) objectInHand = null;
            var t = pickable.transform;
            t.SetParent(pickable.OriginalParent);
            t.SetPositionAndRotation(pickable.OriginalPosition, pickable.OriginalRotation);
            RestorePhysics(pickable);
            SetPickableVisible(pickable, false);
            if (_isDebug) Debug.Log($"{LogPrefix} holstered '{pickable.name}'", pickable);
        }

        /// <summary>Bring a carried item back out: show it and re-dock it at its anchor.</summary>
        private void DrawCarried(PickableObject pickable)
        {
            SetPickableVisible(pickable, true);
            objectInHand = pickable;
            AttachHeld(pickable, HandType.None);
            if (_isDebug) Debug.Log($"{LogPrefix} drew '{pickable.name}'", pickable);
        }

        // Renderers, not the GameObject: disabling the object would kill the item's own
        // scripts (a tablet's UI keeps running while stowed).
        private static void SetPickableVisible(PickableObject pickable, bool visible)
        {
            foreach (var renderer in pickable.GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
            foreach (var canvas in pickable.GetComponentsInChildren<Canvas>(true)) canvas.enabled = visible;
        }

        private PrimaryItemController ResolvePrimaryItemController()
        {
            if (primaryItemController == null && !primaryItemControllerSearched)
            {
                primaryItemControllerSearched = true;
                primaryItemController = FindFirstObjectByType<PrimaryItemController>(FindObjectsInactive.Include);
            }
            return primaryItemController;
        }

        /// <summary>
        /// Parents a taken object to the anchor IT asks for (camera dock, right/left
        /// hand) instead of the old hardwired camera, applies its local offset (a hand
        /// pose supplies its own) and wraps the hand around it.
        /// </summary>
        private void AttachHeld(PickableObject pickable, HandType grabbedWith)
        {
            var anchors = ResolveAnchors();
            Transform anchor = null;
            _heldHand = HandType.None;
            if (anchors != null) anchor = anchors.Resolve(pickable, grabbedWith, out _heldHand);
            // No rig component (Player variant predating it): the old camera hold still works.
            if (anchor == null) anchor = mainCamera != null ? mainCamera.transform : null;
            if (anchor == null) return;

            var t = pickable.transform;
            t.SetParent(anchor, false);
            pickable.GetHeldOffset(_heldHand, out var localPosition, out var localRotation);
            t.localPosition = localPosition;
            t.localRotation = localRotation;

            // A camera-docked item keeps the scroll-adjustable hold distance: its local
            // z IS that distance, so seed it from the authored pose.
            if (pickable.Anchor == HeldAnchor.Camera)
                objectDistance = Mathf.Clamp(localPosition.z, minDistance, maxDistance);

            SuspendPhysics(pickable);
            if (anchors != null) anchors.ApplyHandPose(_heldHand, pickable.HandPose);
        }

        private static void SuspendPhysics(PickableObject pickable)
        {
            if (pickable.Rigidbody == null) return;
            pickable.Rigidbody.freezeRotation = true;
            pickable.Rigidbody.useGravity = false;
        }

        private static void RestorePhysics(PickableObject pickable)
        {
            if (pickable.Rigidbody == null) return;
            pickable.Rigidbody.useGravity = pickable.InitialUseGravity;
            pickable.Rigidbody.linearDamping = pickable.InitialDrag;
            pickable.Rigidbody.angularDamping = pickable.InitialAngularDrag;
            pickable.Rigidbody.freezeRotation = false;
        }

        // Drops the object in hand WHERE ITS RELEASE TARGET SAYS. The old code always
        // reparented to the transform.parent captured at Awake; a world location or a
        // project-placed drop was impossible, and the pose was only restored when
        // "return to initial position" was ticked.
        private void Release()
        {
            var pickable = objectInHand;
            objectInHand = null;
            if (pickable == null) return;

            var anchors = ResolveAnchors();
            if (anchors != null) anchors.ClearHandPose(_heldHand, pickable.HandPose);
            _heldHand = HandType.None;

            DisablePositionHandle();

            // A slotted item stays with the player: releasing it HOLSTERS it (the draw
            // binding brings it back) instead of dropping it back into the world. The
            // state change is what actually stows it — see OnPrimaryItemStateChanged.
            if (pickable.IsCarried)
            {
                if (pickable.Slot == CarrySlot.Primary) ResolvePrimaryItemController()?.SetState(false);
                else HolsterCarried(pickable); // no draw binding for the other slots yet
                return;
            }

            ReturnToWorld(pickable);
        }

        /// <summary>
        /// Puts an object back into the world according to its Release Target. Shared by
        /// a normal release and by evicting the previous Primary item, so both obey the
        /// same authored rule instead of one of them inventing its own.
        /// </summary>
        private void ReturnToWorld(PickableObject pickable)
        {
            SetPickableVisible(pickable, true); // it may have been holstered (hidden)
            objectDropped?.RaiseEvent(pickable.gameObject);

            var t = pickable.transform;
            switch (pickable.ReleaseMode)
            {
                case ReleaseTarget.OriginalSpot:
                    t.SetParent(pickable.OriginalParent);
                    t.SetPositionAndRotation(pickable.OriginalPosition, pickable.OriginalRotation);
                    break;

                case ReleaseTarget.WorldLocation:
                    // Plain coordinates — nothing to resolve, so additive loading cannot break it.
                    pickable.GetReleaseWorldPose(out var worldPosition, out var worldRotation);
                    t.SetParent(null);
                    t.SetPositionAndRotation(worldPosition, worldRotation);
                    break;

                case ReleaseTarget.EventDriven:
                    // The project places it (a teleport/placement event, an inventory, …).
                    t.SetParent(null);
                    pickable.RequestRelease();
                    break;

                case ReleaseTarget.DropInPlace:
                default:
                    t.SetParent(null);
                    break;
            }

            RestorePhysics(pickable);
        }

        private PlayerItemAnchors ResolveAnchors()
        {
            if (itemAnchors == null && !anchorsSearched)
            {
                anchorsSearched = true;
                itemAnchors = GetComponentInParent<PlayerItemAnchors>()
                              ?? FindFirstObjectByType<PlayerItemAnchors>(FindObjectsInactive.Include);
            }
            return itemAnchors;
        }
        // NOTE: these two are invoked from SelectEnter UnityEvents on the NearFar
        // interactors in Player.prefab (whose serialized target type is still the
        // pre-rename 'jeanf.vrplayer.TakeObject' — they resolve by object+name at
        // runtime, so do NOT rename them or change their signature).
        public void AssignGameObjectInRightHand()
        {
            if (rightInteractor.interactablesSelected.Count <= 0) return;
            var selectedInteractable = rightInteractor.interactablesSelected[0]; // Get the first selected interactable
            objectRightHand = selectedInteractable.transform.gameObject.GetComponent<PickableObject>();
            // XRI owns the attachment in VR; the item's hand pose is still ours to apply.
            if (objectRightHand != null) ResolveAnchors()?.ApplyHandPose(HandType.Right, objectRightHand.HandPose);
            OnGrabDeactivateCollider?.Invoke(true, HandType.Right);
            OnVrGrabSwapPrimaryItem?.Invoke("RightHand");
        }

        public void AssignGameObjectInLeftHand()
        {
            if (leftInteractor.interactablesSelected.Count <= 0) return;
            var selectedInteractable = leftInteractor.interactablesSelected[0]; // Get the first selected interactable
            objectLeftHand = selectedInteractable.transform.gameObject.GetComponent<PickableObject>();
            if (objectLeftHand != null) ResolveAnchors()?.ApplyHandPose(HandType.Left, objectLeftHand.HandPose);
            OnGrabDeactivateCollider?.Invoke(true, HandType.Left);
            OnVrGrabSwapPrimaryItem?.Invoke("LeftHand");

        }
        public void RemoveGameObjectInRightHand()
        {
            // Guard: this fires on SelectExit even when nothing pickable was held
            // (objectRightHand is only set for a PickableObject), and the old code
            // dereferenced it unconditionally.
            if (objectRightHand != null)
            {
                ResolveAnchors()?.ClearHandPose(HandType.Right, objectRightHand.HandPose);
                objectDropped?.RaiseEvent(objectRightHand.gameObject);
            }

            objectRightHand = null;
            OnGrabDeactivateCollider?.Invoke(false, HandType.Right);

        }

        public void RemoveGameObjectInLeftHand()
        {
            if (objectLeftHand != null)
            {
                ResolveAnchors()?.ClearHandPose(HandType.Left, objectLeftHand.HandPose);
                objectDropped?.RaiseEvent(objectLeftHand.gameObject);
            }

            objectLeftHand = null;
            OnGrabDeactivateCollider?.Invoke(false, HandType.Left);

        }

        public bool GetObjectsInHandStatus()
        {

            if (objectInHand == null && objectRightHand == null && objectLeftHand == null) return false;
            else { return true; }
        }
        public List<GameObject> GetObjectsInHand()
        {
            List<GameObject> objectsInHand = new List<GameObject>();

            if (objectLeftHand != null)
            {
                objectsInHand.Add(objectLeftHand.gameObject);
            }

            if (objectRightHand != null)
            {
                objectsInHand.Add(objectRightHand.gameObject);
            }

            if (objectInHand != null)
            {
                objectsInHand.Add(objectInHand.gameObject);
            }

            return objectsInHand;
        }
        #endregion

        #region Object movemement methods
        private void UpdateObjectDistance(float value)
        {
            if (objectIsSnapping || objectInHand == null) return;
            // Scrolling pushes/pulls a CAMERA-docked item. An item held in a hand sits
            // where the hand (or its hand pose) puts it — there is no view distance to
            // adjust, and writing a world position would fight the hand parenting.
            if (objectInHand.Anchor != HeldAnchor.Camera) return;

            value *= scrollStep;
            if (_isDebug) Debug.Log($"scroll reading: {value}");
            objectDistance = Mathf.Clamp(objectDistance + value, minDistance, maxDistance);
            ApplyCameraHoldDistance();
        }

        // Snapping drives the object to a world snap point; letting go of the snap must
        // put it back where its ANCHOR says it should be — which is the hand pose for a
        // hand-held item, not (as before) always a camera-forward world position.
        private void UpdateSnapStatus(bool snapState)
        {
            objectIsSnapping = snapState;
            if (!snapState && objectInHand != null) ReapplyHeldPose();
        }

        /// <summary>Restores the held object to its anchor-local pose (its hand pose, or the camera dock at the current scroll distance).</summary>
        private void ReapplyHeldPose()
        {
            if (objectInHand == null) return;
            objectInHand.GetHeldOffset(_heldHand, out var localPosition, out var localRotation);
            if (objectInHand.Anchor == HeldAnchor.Camera) localPosition.z = objectDistance;
            objectInHand.transform.localPosition = localPosition;
            objectInHand.transform.localRotation = localRotation;
        }

        // The item is parented to the camera, so the hold distance is simply its local
        // z — no world-space math, which keeps it correct while the camera moves.
        private void ApplyCameraHoldDistance()
        {
            if (objectInHand == null) return;
            var local = objectInHand.transform.localPosition;
            local.z = objectDistance;
            objectInHand.transform.localPosition = local;
        }
        private void SetObjectPosition(Transform objectToMove, Vector3 goal)
        {
            DisablePositionHandle();
            if (!objectInHand) return;

            if (objectToMove.position == goal)
            {
                return;
            }
            if (!objectToMove)
            {
                if (_isDebug) Debug.Log($"objectToMove is null");

                DisablePositionHandle();
                return;
            }


            if (objectToMove.transform.position == goal)
            {
                return;
            }
            objectToMove.position = Vector3.Lerp(objectToMove.position, goal, 1f);
            //_positionHandle = LMotion.Create(objectToMove.transform.position, goal, sliderMotionDuration)
            //    .Bind(x => objectToMove.transform.position = x)
            //    .AddTo(objectToMove.gameObject);
            //objectToMove.transform.position = goal;
        }
        private void DisablePositionHandle()
        {
            if (!_positionHandle.IsActive()) return;
            _positionHandle.Complete();
            _positionHandle.Cancel();

        }

        private void SetObjectRotation(Transform objectToMove, Quaternion goal)
        {
            if (!objectToMove)
            {
                DisableRotationHandle();
                return;
            }
            if (objectToMove.transform.rotation == goal) return;
            if (!objectInHand) return;

            objectToMove.transform.rotation = Quaternion.Lerp(objectToMove.transform.rotation, goal, 1f);
            //_rotationHandle = LMotion.Create(objectToMove.transform.rotation, goal, sliderMotionDuration)
            //    .Bind(x => objectToMove.transform.rotation = x)
            //    .AddTo(objectToMove.gameObject);
        }
        private void DisableRotationHandle()
        {
            if (!_rotationHandle.IsActive()) return;
            _rotationHandle.Complete();
            _rotationHandle.Cancel();
        }
        #endregion

        #region room id Method
        private void AssignRoomId(int roomId)
        {
            this.roomId = roomId;
        }
        #endregion
    }
}



