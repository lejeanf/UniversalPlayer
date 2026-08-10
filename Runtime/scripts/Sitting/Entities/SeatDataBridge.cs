using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Surfaces ECS-baked seats to the GameObject world. It queries seat entities and exposes
    /// each as a plain <see cref="SeatData"/> (plus its world position), so the seat collider-proxy
    /// pool and <see cref="SitController"/> can treat a baked seat exactly like a classic one.
    ///
    /// Seats never move, so the cache only needs re-scanning as SubScenes stream in and out —
    /// hence a slow interval rather than a per-frame refresh.
    /// </summary>
    public class SeatDataBridge : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public static SeatDataBridge Instance { get; private set; }

        [SerializeField] private bool isDebug = false;
        [Tooltip("Seconds between re-scans of baked seats (handles SubScene streaming). Seats never move, so this can be slow.")]
        [SerializeField] private float refreshInterval = 0.5f;

        public readonly struct SeatEntry
        {
            public readonly Entity Entity;
            public readonly SeatData Data;
            public SeatEntry(Entity entity, SeatData data) { Entity = entity; Data = data; }
        }

        private EntityManager _em;
        private EntityQuery _seatQuery;
        private bool _worldReady;
        private float _timer;
        private int _lastLoggedCount = -1;
        private readonly List<SeatEntry> _seats = new List<SeatEntry>(64);
        private readonly Dictionary<Entity, int> _index = new Dictionary<Entity, int>(64);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            _timer = float.MaxValue; // scan on the first Update
            TryInitWorld();
        }

        private void TryInitWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { _worldReady = false; return; }
            _em = world.EntityManager;
            _seatQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<SeatComponent>());
            _worldReady = true;
        }

        private void Update()
        {
            if (!_worldReady)
            {
                TryInitWorld();
                if (!_worldReady) return;
            }

            _timer += Time.deltaTime;
            if (_timer < refreshInterval) return;
            _timer = 0f;
            Refresh();
        }

        private void Refresh()
        {
            _seats.Clear();
            _index.Clear();

            var entities = _seatQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (!TryBuildSeatData(e, out var data)) continue;
                _index[e] = _seats.Count;
                _seats.Add(new SeatEntry(e, data));
            }
            entities.Dispose();

            if (isDebug && _seats.Count != _lastLoggedCount)
            {
                _lastLoggedCount = _seats.Count;
                Debug.Log($"{LogPrefix} SeatDataBridge: {_seats.Count} baked seat(s) available.", this);
            }
        }

        private bool TryBuildSeatData(Entity seat, out SeatData data)
        {
            data = default;
            if (!_em.HasComponent<SeatComponent>(seat)) return false;

            var sc = _em.GetComponentData<SeatComponent>(seat);
            if (sc.SitAnchor == Entity.Null || !_em.HasComponent<LocalToWorld>(sc.SitAnchor)) return false;

            var sitL2W = _em.GetComponentData<LocalToWorld>(sc.SitAnchor);
            Vector3 sitPos = sitL2W.Position;
            var sitYaw = ((Quaternion)sitL2W.Rotation).eulerAngles.y;

            var hasExit = sc.ExitAnchor != Entity.Null && _em.HasComponent<LocalToWorld>(sc.ExitAnchor);
            Vector3 exitPos = Vector3.zero;
            var exitYaw = 0f;
            if (hasExit)
            {
                var l2w = _em.GetComponentData<LocalToWorld>(sc.ExitAnchor);
                exitPos = l2w.Position;
                exitYaw = ((Quaternion)l2w.Rotation).eulerAngles.y;
            }

            var hasHand = sc.HandSupportAnchor != Entity.Null && _em.HasComponent<LocalToWorld>(sc.HandSupportAnchor);
            Vector3 handPos = Vector3.zero;
            var handRot = Quaternion.identity;
            if (hasHand)
            {
                var l2w = _em.GetComponentData<LocalToWorld>(sc.HandSupportAnchor);
                handPos = l2w.Position;
                handRot = l2w.Rotation;
            }

            data = new SeatData(
                seatId: seat.Index,
                name: $"Seat(e{seat.Index})",
                sitPosition: sitPos,
                sitFacingYaw: sitYaw,
                eyeHeightAboveSeat: sc.EyeHeight,
                hasExit: hasExit,
                exitPosition: exitPos,
                exitFacingYaw: exitYaw,
                hasHandSupport: hasHand,
                handSupportWorldPos: handPos,
                handSupportWorldRot: handRot);
            return true;
        }

        /// <summary>All baked seats currently available (refreshed on the streaming interval).</summary>
        public IReadOnlyList<SeatEntry> GetAllSeats() => _seats;

        /// <summary>Fresh seat data for a specific seat entity, or false if it is gone (streamed out).</summary>
        public bool TryGetSeat(Entity seat, out SeatData data)
        {
            if (_worldReady && _em.Exists(seat) && TryBuildSeatData(seat, out data)) return true;
            if (_index.TryGetValue(seat, out var i)) { data = _seats[i].Data; return true; }
            data = default;
            return false;
        }
    }
}
