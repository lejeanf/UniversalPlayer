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
}
