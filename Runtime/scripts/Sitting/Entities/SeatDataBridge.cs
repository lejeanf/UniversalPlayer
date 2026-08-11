using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Bridges ECS-baked seats back into the GameObject world. It queries seat entities, reads
    /// their baked anchors/collider, and — because this project has no ECS physics — spawns plain
    /// PhysX <see cref="BoxCollider"/> proxies (<see cref="SeatProxy"/>) over the seats near the
    /// player. Those proxies are what the raycast / hover / VR interactor actually hit, so a baked
    /// seat behaves exactly like a classic <see cref="Seat"/>. Mirrors the door system's data
    /// bridge + collider pool, merged into one component.
    ///
    /// Seats never move and only exist while their SubScene is loaded, so this is a light reconcile
    /// (add proxies for newly in-range seats, drop ones that left range or streamed out) rather than
    /// a per-frame moving-collider pool. Drop one on a persistent GameObject; it's a singleton.
    /// </summary>
    public class SeatDataBridge : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public static SeatDataBridge Instance { get; private set; }

        [SerializeField] private bool isDebug = false;
        [Tooltip("Seconds between re-scans of baked seats (handles SubScene streaming) and proxy reconciles. Seats never move, so this can be slow.")]
        [SerializeField] private float refreshInterval = 0.25f;
        [Tooltip("Only seats within this distance of the camera get a live proxy collider. <= 0 means no limit.")]
        [SerializeField] private float cullingDistance = 30f;

        private struct SeatInfo
        {
            public SeatData Data;
            public Vector3 Position;   // chair root world position
            public Quaternion Rotation;
            public Vector3 LossyScale;
            public Vector3 ColliderSize;
            public Vector3 ColliderCenter;
            public bool HasCollider;
            public int Layer;
        }

        private EntityManager _em;
        private EntityQuery _seatQuery;
        private EntityQuery _forceSitQuery;
        private bool _worldReady;
        private float _timer;
        private int _lastLoggedCount = -1;

        private Transform _container;
        private Transform _camera;

        private readonly List<Entity> _entities = new List<Entity>(64);
        private readonly Dictionary<Entity, SeatInfo> _seats = new Dictionary<Entity, SeatInfo>(64);
        private readonly Dictionary<Entity, SeatProxy> _proxies = new Dictionary<Entity, SeatProxy>(64);
        private readonly List<Entity> _toRemove = new List<Entity>(16);
        private readonly HashSet<Entity> _seen = new HashSet<Entity>();

        // Baked SitPlayerOnEnable (ForceSitOnLoad) entities → the proxy GameObjects re-hosting
        // the real component. Alive proxy = already fired for this load; streaming the section
        // out destroys it, so a reload re-triggers — the entity-world "OnEnable".
        private readonly Dictionary<Entity, GameObject> _forceSitProxies = new Dictionary<Entity, GameObject>(8);
        private readonly HashSet<Entity> _forceSitSeen = new HashSet<Entity>();
        private readonly HashSet<Entity> _forceSitInvalid = new HashSet<Entity>();
        private readonly List<Entity> _forceSitToRemove = new List<Entity>(8);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            // A root container at identity scale, so a proxy's localScale == the chair's lossy scale.
            _container = new GameObject("SeatProxy_Pool").transform;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_container != null) Destroy(_container.gameObject);
        }

        private void OnEnable()
        {
            _timer = float.MaxValue; // scan on the first Update
            TryInitWorld();
            // Baked seats join the cross-world seat lookup (SitPlayerOnEnable & co. resolve by id).
            SeatRegistry.RegisterResolver(ResolveSeatById);
        }

        private void OnDisable() => SeatRegistry.UnregisterResolver(ResolveSeatById);

        // Authored-id lookup over the baked seats. On a cache miss with a live world, re-scan
        // once immediately — scenario code often asks right as a SubScene finishes streaming in,
        // up to refreshInterval before the next scheduled scan would see it.
        private bool ResolveSeatById(int seatId, out SeatData data)
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                foreach (var kv in _seats)
                {
                    if (kv.Value.Data.SeatId != seatId) continue;
                    data = kv.Value.Data;
                    return true;
                }
                if (!_worldReady) TryInitWorld();
                if (!_worldReady) break;
                Refresh();
            }
            data = default;
            return false;
        }

        private void TryInitWorld()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) { _worldReady = false; return; }
            _em = world.EntityManager;
            _seatQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<SeatComponent>());
            _forceSitQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ForceSitOnLoad>());
            _worldReady = true;
        }

        private void Update()
        {
            if (!_worldReady)
            {
                TryInitWorld();
                if (!_worldReady) return;
            }
            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;

            _timer += Time.deltaTime;
            if (_timer < refreshInterval) return;
            _timer = 0f;

            Refresh();
            Reconcile();
            ReconcileForceSits();
        }

        // --- Query baked seats -------------------------------------------------

        private void Refresh()
        {
            _seats.Clear();

            var entities = _seatQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                if (TryBuildSeat(e, out var info)) _seats[e] = info;
            }
            entities.Dispose();

            if (isDebug && _seats.Count != _lastLoggedCount)
            {
                _lastLoggedCount = _seats.Count;
                Debug.Log($"{LogPrefix} SeatDataBridge: {_seats.Count} baked seat(s) available.", this);
            }
        }

        private bool TryBuildSeat(Entity seat, out SeatInfo info)
        {
            info = default;
            if (!_em.HasComponent<SeatComponent>(seat) || !_em.HasComponent<LocalToWorld>(seat)) return false;

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

            var rootL2W = _em.GetComponentData<LocalToWorld>(seat);
            var m = rootL2W.Value;
            var lossyScale = new Vector3(math.length(m.c0.xyz), math.length(m.c1.xyz), math.length(m.c2.xyz));

            info = new SeatInfo
            {
                Data = new SeatData(
                    seatId: sc.SeatId != 0 ? sc.SeatId : seat.Index, // authored id wins; entity index is a fallback
                    name: $"Seat(e{seat.Index})",
                    sitPosition: sitPos,
                    sitFacingYaw: sitYaw,
                    eyeHeightAboveSeat: sc.EyeHeight,
                    hasExit: hasExit,
                    exitPosition: exitPos,
                    exitFacingYaw: exitYaw,
                    hasHandSupport: hasHand,
                    handSupportWorldPos: handPos,
                    handSupportWorldRot: handRot),
                Position = rootL2W.Position,
                Rotation = rootL2W.Rotation,
                LossyScale = lossyScale,
                ColliderSize = sc.ColliderSize,
                ColliderCenter = sc.ColliderCenter,
                HasCollider = sc.HasCollider == 1,
                Layer = sc.Layer,
            };
            return true;
        }

        // --- Spawn / recycle GameObject collider proxies -----------------------

        private void Reconcile()
        {
            var haveCam = _camera != null;
            var camPos = haveCam ? _camera.position : Vector3.zero;
            var cull = cullingDistance > 0f;
            var cullSqr = cullingDistance * cullingDistance;

            _seen.Clear();
            foreach (var kv in _seats)
            {
                var info = kv.Value;
                if (cull && haveCam && (info.Position - camPos).sqrMagnitude > cullSqr) continue;
                _seen.Add(kv.Key);

                if (_proxies.TryGetValue(kv.Key, out var proxy) && proxy != null) continue; // static: place once
                _proxies[kv.Key] = CreateAndPlace(kv.Key, info);
            }

            _toRemove.Clear();
            foreach (var kv in _proxies)
                if (kv.Value == null || !_seen.Contains(kv.Key)) _toRemove.Add(kv.Key);

            for (var i = 0; i < _toRemove.Count; i++)
            {
                if (_proxies.TryGetValue(_toRemove[i], out var p) && p != null) Destroy(p.gameObject);
                _proxies.Remove(_toRemove[i]);
            }
        }

        private SeatProxy CreateAndPlace(Entity entity, in SeatInfo info)
        {
            var go = new GameObject("SeatProxy");
            go.transform.SetParent(_container, false);
            go.transform.SetPositionAndRotation(info.Position, info.Rotation);
            go.transform.localScale = info.LossyScale;
            go.layer = info.Layer;

            var box = go.AddComponent<BoxCollider>();
            if (info.HasCollider) { box.size = info.ColliderSize; box.center = info.ColliderCenter; }
            else { box.size = new Vector3(0.6f, 1f, 0.6f); box.center = new Vector3(0f, 0.5f, 0f); }

            var proxy = go.AddComponent<SeatProxy>();
            proxy.Bind(entity, info.Data);
            return proxy;
        }

        // --- Baked SitPlayerOnEnable execution ---------------------------------

        // A SitPlayerOnEnable authored in a SubScene is stripped at runtime (entities only),
        // so its baked ForceSitOnLoad is executed here: when the entity appears (its section
        // loaded), spawn a proxy GameObject re-hosting the REAL component pre-resolved — all
        // the fade/wait/warn behavior stays in one place. One firing per load; streaming out
        // removes the proxy so the next load fires again.
        private void ReconcileForceSits()
        {
            _forceSitSeen.Clear();
            var entities = _forceSitQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var e = entities[i];
                _forceSitSeen.Add(e);
                if (_forceSitInvalid.Contains(e)) continue;
                if (_forceSitProxies.TryGetValue(e, out var existing) && existing != null) continue;

                var fs = _em.GetComponentData<ForceSitOnLoad>(e);
                if (fs.Seat != Entity.Null)
                {
                    // Directly-linked seat: resolve through the (just-refreshed) seat cache.
                    // Not there yet = the seat's section is still streaming in — retry next tick.
                    if (!TryGetSeat(fs.Seat, out var data)) continue;
                    _forceSitProxies[e] = SpawnForceSitProxy(proxy => proxy.ConfigureResolved(data, fs.FadeToBlack == 1, fs.HoldBlackSeconds));
                }
                else if (fs.SeatId != 0)
                {
                    // Id-targeted: the component's own SeatRegistry retry loop handles timing.
                    _forceSitProxies[e] = SpawnForceSitProxy(proxy => proxy.ConfigureById(fs.SeatId, fs.FadeToBlack == 1, fs.HoldBlackSeconds));
                }
                else
                {
                    _forceSitInvalid.Add(e);
                    Debug.LogWarning($"{LogPrefix} SeatDataBridge: a baked SitPlayerOnEnable has neither a linked Seat " +
                        "nor a Seat Id — the player cannot be seated. Fix the SitPlayerOnEnable in its SubScene and re-bake.", this);
                }
            }
            entities.Dispose();

            _forceSitToRemove.Clear();
            foreach (var kv in _forceSitProxies)
                if (kv.Value == null || !_forceSitSeen.Contains(kv.Key)) _forceSitToRemove.Add(kv.Key);
            for (var i = 0; i < _forceSitToRemove.Count; i++)
            {
                if (_forceSitProxies.TryGetValue(_forceSitToRemove[i], out var go) && go != null) Destroy(go);
                _forceSitProxies.Remove(_forceSitToRemove[i]);
            }
            _forceSitInvalid.RemoveWhere(e => !_forceSitSeen.Contains(e)); // re-warn if a fixed bake reloads
        }

        private GameObject SpawnForceSitProxy(System.Action<SitPlayerOnEnable> configure)
        {
            // Configure while inactive so OnEnable runs with the values already in place.
            var go = new GameObject("ForceSitProxy");
            go.transform.SetParent(_container, false);
            go.SetActive(false);
            configure(go.AddComponent<SitPlayerOnEnable>());
            go.SetActive(true);
            return go;
        }

        /// <summary>Seat pose for a specific baked seat entity, or false if it is gone (streamed out).</summary>
        public bool TryGetSeat(Entity seat, out SeatData data)
        {
            if (_seats.TryGetValue(seat, out var info)) { data = info.Data; return true; }
            data = default;
            return false;
        }
    }
}
