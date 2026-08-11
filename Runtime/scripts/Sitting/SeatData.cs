using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// A seat expressed as plain values — everything <see cref="SitController"/> needs to
    /// seat the player, with no dependency on a live GameObject/Transform. Both the classic
    /// <see cref="Seat"/> MonoBehaviour and the ECS-baked seat proxy produce one of these,
    /// so a single sit/stand path serves the additive-scene flow and the entity world alike.
    /// </summary>
    public readonly struct SeatData
    {
        /// <summary>Stable id for entity-world lookups; the classic path uses the seat's instance id.</summary>
        public readonly int SeatId;
        /// <summary>Human-readable name for logs/warnings.</summary>
        public readonly string Name;

        /// <summary>Where the hips go (world space).</summary>
        public readonly Vector3 SitPosition;
        /// <summary>Which way the player faces while seated (world yaw, degrees).</summary>
        public readonly float SitFacingYaw;
        /// <summary>Eye height above the sit position while seated.</summary>
        public readonly float EyeHeightAboveSeat;

        /// <summary>When false the player returns to where they sat down from on exit.</summary>
        public readonly bool HasExit;
        public readonly Vector3 ExitPosition;
        public readonly float ExitFacingYaw;

        /// <summary>Optional hand rest (chair back / armrest) reached with IK during the glide.</summary>
        public readonly bool HasHandSupport;
        public readonly Vector3 HandSupportWorldPos;
        public readonly Quaternion HandSupportWorldRot;

        public SeatData(
            int seatId,
            string name,
            Vector3 sitPosition,
            float sitFacingYaw,
            float eyeHeightAboveSeat,
            bool hasExit,
            Vector3 exitPosition,
            float exitFacingYaw,
            bool hasHandSupport,
            Vector3 handSupportWorldPos,
            Quaternion handSupportWorldRot)
        {
            SeatId = seatId;
            Name = name;
            SitPosition = sitPosition;
            SitFacingYaw = sitFacingYaw;
            EyeHeightAboveSeat = eyeHeightAboveSeat;
            HasExit = hasExit;
            ExitPosition = exitPosition;
            ExitFacingYaw = exitFacingYaw;
            HasHandSupport = hasHandSupport;
            HandSupportWorldPos = handSupportWorldPos;
            HandSupportWorldRot = handSupportWorldRot;
        }
    }

    /// <summary>
    /// Anything the player can sit on. Implemented by the classic <see cref="Seat"/> and by the
    /// ECS seat proxy, so <see cref="SitController"/> resolves both from one raycast / XR select.
    /// </summary>
    public interface ISeatSource
    {
        SeatData GetSeatData();
    }

    /// <summary>
    /// Finds a seat by its authored Seat Id across BOTH worlds — for scenario code (e.g.
    /// <see cref="SitPlayerOnEnable"/>) that must target a seat it cannot hold a direct
    /// reference to: a seat in another additive scene, or one baked into a SubScene.
    /// Live <see cref="Seat"/>s with a non-zero Seat Id register themselves; the entity
    /// world's SeatDataBridge registers a resolver covering every currently-baked seat.
    /// </summary>
    public static class SeatRegistry
    {
        /// <summary>Resolver signature: true + data when the id is known to this resolver.</summary>
        public delegate bool SeatResolver(int seatId, out SeatData data);

        private static readonly System.Collections.Generic.Dictionary<int, ISeatSource> Sources =
            new System.Collections.Generic.Dictionary<int, ISeatSource>();
        private static readonly System.Collections.Generic.List<SeatResolver> Resolvers =
            new System.Collections.Generic.List<SeatResolver>();

        public static void Register(int seatId, ISeatSource source)
        {
            if (seatId == 0 || source == null) return;
            if (Sources.TryGetValue(seatId, out var existing) && !ReferenceEquals(existing, source))
                Debug.LogWarning($"[UniversalPlayer] SeatRegistry: two seats share Seat Id {seatId} — " +
                    "ids must be unique for scenario targeting to be deterministic. The newest wins.");
            Sources[seatId] = source;
        }

        public static void Unregister(int seatId, ISeatSource source)
        {
            if (seatId == 0) return;
            if (Sources.TryGetValue(seatId, out var existing) && ReferenceEquals(existing, source))
                Sources.Remove(seatId);
        }

        public static void RegisterResolver(SeatResolver resolver)
        {
            if (resolver != null && !Resolvers.Contains(resolver)) Resolvers.Add(resolver);
        }

        public static void UnregisterResolver(SeatResolver resolver) => Resolvers.Remove(resolver);

        /// <summary>Seat values for an authored id — live Seats first, then baked-world resolvers.</summary>
        public static bool TryGetSeatData(int seatId, out SeatData data)
        {
            if (seatId != 0)
            {
                if (Sources.TryGetValue(seatId, out var source))
                {
                    data = source.GetSeatData();
                    return true;
                }
                for (var i = 0; i < Resolvers.Count; i++)
                    if (Resolvers[i](seatId, out data)) return true;
            }
            data = default;
            return false;
        }
    }
}
