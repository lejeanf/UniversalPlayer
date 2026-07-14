using System;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>Where a held object docks while it is carried.</summary>
    public enum HeldAnchor
    {
        /// <summary>In front of the view at an authorable local pose (tablet-style). Desktop only — VR has real hands, so a grab there uses the grabbing hand instead.</summary>
        Camera,
        RightHand,
        LeftHand,
    }

    /// <summary>How a hand anchor is realised on M&amp;K / gamepad. VR always uses the real tracked hand.</summary>
    public enum HandAttachMode
    {
        /// <summary>A camera-relative anchor to the side of the view: rock-steady. Right for tablets and anything whose UI must be clickable.</summary>
        SteadyDock,
        /// <summary>The first-person body's actual hand bone: the item moves with the walk/idle animation. Needs FirstPersonBody enabled with a humanoid rig.</summary>
        AnimatedBone,
    }

    /// <summary>Where an object goes when it is released.</summary>
    public enum ReleaseTarget
    {
        /// <summary>Let go where it is; physics takes over.</summary>
        DropInPlace,
        /// <summary>Back to the parent, position AND rotation it started at.</summary>
        OriginalSpot,
        /// <summary>A fixed world pose. No scene reference, so it is safe under additive loading.</summary>
        WorldLocation,
        /// <summary>The project decides — raises <see cref="PickableObject.ReleaseRequested"/> and does not place the object itself.</summary>
        EventDriven,
    }

    /// <summary>
    /// Carry slots. A slotted item STAYS WITH THE PLAYER once picked: releasing it
    /// holsters it into its slot instead of dropping it back into the world (this is
    /// what makes the tablet "a pickable that stays available", rather than a special
    /// class). <see cref="CarrySlot.None"/> is an ordinary world object.
    /// </summary>
    public enum CarrySlot
    {
        None,
        Primary,
        Secondary,
        Tertiary,
        Quaternary,
    }

    /// <summary>
    /// Marks an object as pickable and declares WHERE it goes — while held (<see cref="HeldAnchor"/>)
    /// and when released (<see cref="ReleaseTarget"/>).
    ///
    /// No cross-scene references: hands and the camera resolve from the player rig at
    /// runtime (see <see cref="PlayerItemAnchors"/>) and a world location is plain
    /// coordinates, so nothing has to point across an additively-loaded scene boundary.
    /// </summary>
    public class PickableObject : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        /// <summary>Raised instead of placing the object when <see cref="ReleaseTarget.EventDriven"/> — the project decides where it lands (a teleport/placement event, an inventory, …).</summary>
        public static event Action<PickableObject> ReleaseRequested;

        [Header("Held anchor — where it sits while carried")]
        [Tooltip("Camera = docked in front of the view (tablet-style). Right/Left hand = held in that hand. In VR the hand that actually grabbed it always wins.")]
        [SerializeField] private HeldAnchor heldAnchor = HeldAnchor.Camera;
        [Tooltip("Only for a hand anchor on M&K/gamepad. Steady Dock is camera-relative and never jitters (use it for anything with UI). Animated Bone rides the first-person body's real hand — needs the body enabled with a humanoid rig.")]
        [SerializeField] private HandAttachMode handAttachMode = HandAttachMode.SteadyDock;
        [Tooltip("Local offset from the resolved anchor. Ignored for a hand anchor when a Hand Pose is set (the pose carries its own attach offset).")]
        [SerializeField] private Vector3 heldLocalPosition = new Vector3(0f, 0f, 0.5f);
        [SerializeField] private Vector3 heldLocalEuler = Vector3.zero;
        [Tooltip("Optional. The hand pose to wrap around this object: supplies the per-hand attach offset AND the finger pose. Same Pose asset type the primary item uses.")]
        [SerializeField] private Pose handPose;

        [Header("Release — where it goes when dropped")]
        [Tooltip("Original Spot (the default) puts it back exactly where it started. Drop In Place lets go of it where it is and lets physics take over.")]
        [SerializeField] private ReleaseTarget releaseTarget = ReleaseTarget.OriginalSpot;
        [Tooltip("Used by Release Target = World Location. Plain coordinates, so additive loading can never break it.")]
        [SerializeField] private Vector3 releaseWorldPosition;
        [SerializeField] private Vector3 releaseWorldEuler;

        [Header("Carry slot")]
        [Tooltip("None = an ordinary world object (released back into the world). Any slot = the item stays with the player once picked; releasing HOLSTERS it into that slot. Can also be assigned at runtime (e.g. an inventory promoting an item to Primary).")]
        [SerializeField] private CarrySlot carrySlot = CarrySlot.None;

        // Legacy: superseded by releaseTarget (OriginalSpot vs DropInPlace). Kept so
        // existing scenes keep their behaviour, and migrated once — see Migrate().
        [SerializeField, HideInInspector] private bool returnToInitialPositionOnRelease;
        [SerializeField, HideInInspector] private int _migrationVersion;
        private const int CurrentMigrationVersion = 1;

        // The spot the object started at — parent AND pose. The old code kept only the
        // parent (in a serialized field that Awake overwrote, so the inspector value
        // was dead), which let a released object keep whatever pose it had drifted to.
        private Transform _originalParent;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        // Scale is captured too, in BOTH frames. Reparenting silently rewrites scale
        // (SetParent(p, true) rescales localScale to preserve world size), so an object
        // that is re-parented on take and again on release compounds the rig's scale and
        // grows a little every cycle. Restoring an explicit value is the only way to make
        // take/release exactly reversible.
        private Vector3 _originalLocalScale = Vector3.one;  // correct under the original parent
        private Vector3 _originalWorldScale = Vector3.one;  // correct when dropped at the scene root

        private Rigidbody rb;
        private float initialDrag;
        private float initialAngularDrag;
        private bool initialUseGravity;

        public HeldAnchor Anchor => heldAnchor;
        public HandAttachMode AttachMode => handAttachMode;
        public Vector3 HeldLocalPosition => heldLocalPosition;
        public Quaternion HeldLocalRotation => Quaternion.Euler(heldLocalEuler);
        public Pose HandPose => handPose;
        public ReleaseTarget ReleaseMode => releaseTarget;
        public CarrySlot Slot => carrySlot;
        /// <summary>True when this item lives in a carry slot — releasing it holsters it instead of dropping it.</summary>
        public bool IsCarried => carrySlot != CarrySlot.None;

        public Rigidbody Rigidbody => rb;
        public Transform OriginalParent => _originalParent;
        public Vector3 OriginalPosition => _originalPosition;
        public Quaternion OriginalRotation => _originalRotation;
        /// <summary>The authored localScale — restore this when the object goes back under its original parent.</summary>
        public Vector3 OriginalLocalScale => _originalLocalScale;
        /// <summary>The authored WORLD scale — restore this as localScale when the object is dropped unparented (scene root).</summary>
        public Vector3 OriginalWorldScale => _originalWorldScale;
        public float InitialDrag => initialDrag;
        public float InitialAngularDrag => initialAngularDrag;
        public bool InitialUseGravity => initialUseGravity;

        // Kept for compatibility with existing project code.
        public Transform Parent => _originalParent;
        public Vector3 InitialPosition => _originalPosition;
        public Quaternion InitialRotation => _originalRotation;
        public bool ReturnToInitialPositionOnRelease => releaseTarget == ReleaseTarget.OriginalSpot;

        public bool canBeRejected;

        // Private Awake calling a protected virtual: a subclass (SnapObject) cannot
        // shadow Awake and silently lose this init — Unity would call only the most
        // derived one. Override Initialize instead.
        private void Awake() => Initialize();

        protected virtual void Initialize()
        {
            Migrate();

            var t = transform;
            _originalParent = t.parent;
            _originalPosition = t.position;
            _originalRotation = t.rotation;
            _originalLocalScale = t.localScale;
            _originalWorldScale = t.lossyScale;

            rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                initialDrag = rb.linearDamping;
                initialAngularDrag = rb.angularDamping;
                initialUseGravity = rb.useGravity;
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} PickableObject on '{name}': no Rigidbody — it can be picked up, but its " +
                    "physics cannot be suspended while held or restored on release. Add a Rigidbody.", this);
            }
        }

        /// <summary>Assign this item to a carry slot at runtime — e.g. an inventory promoting it to Primary.</summary>
        public void SetCarrySlot(CarrySlot slot) => carrySlot = slot;

        /// <summary>Raises <see cref="ReleaseRequested"/>. Called by TakeObject for <see cref="ReleaseTarget.EventDriven"/>.</summary>
        internal void RequestRelease() => ReleaseRequested?.Invoke(this);

        /// <summary>
        /// The local offset to apply at the resolved anchor. A hand pose (when set and
        /// we are actually in a hand) carries its own attach offset, so it wins over the
        /// generic heldLocalPosition/Euler.
        /// </summary>
        public void GetHeldOffset(HandType hand, out Vector3 localPosition, out Quaternion localRotation)
        {
            if (handPose != null && hand != HandType.None)
            {
                var info = handPose.GetHandInfo(hand);
                if (info != null)
                {
                    localPosition = info.attachPosition;
                    localRotation = info.attachRotation;
                    return;
                }
            }
            localPosition = heldLocalPosition;
            localRotation = Quaternion.Euler(heldLocalEuler);
        }

        /// <summary>The world pose to release at, for <see cref="ReleaseTarget.WorldLocation"/>.</summary>
        public void GetReleaseWorldPose(out Vector3 position, out Quaternion rotation)
        {
            position = releaseWorldPosition;
            rotation = Quaternion.Euler(releaseWorldEuler);
        }

        // Unity calls Reset only when the component is freshly ADDED. Stamping the
        // version here is what separates "a new pickable" from "an asset authored
        // before releaseTarget existed" — both otherwise deserialize with version 0,
        // and a new one would be migrated off its own default.
        private void Reset() => _migrationVersion = CurrentMigrationVersion;

        // One-time migration off the legacy bool, for assets authored before
        // releaseTarget existed. Without it, adding the enum would silently change
        // every pickable that had "return to initial position" ticked.
        private void Migrate()
        {
            if (_migrationVersion >= CurrentMigrationVersion) return;
            releaseTarget = returnToInitialPositionOnRelease ? ReleaseTarget.OriginalSpot : ReleaseTarget.DropInPlace;
            _migrationVersion = CurrentMigrationVersion;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Migrate authored assets as they are loaded/inspected, so the change is
            // persisted rather than re-derived at runtime forever.
            if (_migrationVersion < CurrentMigrationVersion)
            {
                Migrate();
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
