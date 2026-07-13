#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Procedural gesture presets for the Pose Editor. There is no rig-portable
    /// gesture asset library (public hand datasets are ML mocap retargeted to their
    /// own rigs), so gestures are generated from per-finger curl amounts + the
    /// flexibility/coupling model in PoseSceneTools. Each preset is a one-click
    /// STARTING POINT the author then refines with the joint handles — contact
    /// poses (finger heart) in particular need a manual nudge to touch cleanly.
    /// Finger identity comes from the CC rig bone names (Thumb/Index/Middle/Ring/Little).
    /// </summary>
    public static class HandGesturePresets
    {
        public sealed class Gesture
        {
            public string Name;
            public string Tooltip;
            // Curl per finger, 0 = straight, 1 = full fist.
            public float Thumb, Index, Middle, Ring, Little;
        }

        // 1 = thumb, 2 = index, 3 = middle, 4 = ring, 5 = little (user's numbering).
        public static readonly Gesture[] All =
        {
            new Gesture { Name = "Open hand",  Tooltip = "All fingers extended.",                          Thumb = 0f,   Index = 0f,   Middle = 0f,   Ring = 0f,   Little = 0f },
            new Gesture { Name = "Fist",       Tooltip = "All fingers curled.",                            Thumb = 0.9f, Index = 1f,   Middle = 1f,   Ring = 1f,   Little = 1f },
            new Gesture { Name = "Point",      Tooltip = "Index and thumb extended, the other three curled into a fist.", Thumb = 0f, Index = 0f, Middle = 1f, Ring = 1f, Little = 1f },
            new Gesture { Name = "Thumbs up",  Tooltip = "Fist with the thumb standing straight up.",      Thumb = 0f,   Index = 1f,   Middle = 1f,   Ring = 1f,   Little = 1f },
            new Gesture { Name = "Rock horns", Tooltip = "Punk: thumb, middle, ring folded; index + little extended (\U0001F918).", Thumb = 0.9f, Index = 0f, Middle = 0.85f, Ring = 0.85f, Little = 0f },
            new Gesture { Name = "I love you", Tooltip = "ASL sign: thumb, index and little extended; middle + ring folded (\U0001F91F).", Thumb = 0f, Index = 0f, Middle = 0.85f, Ring = 0.85f, Little = 0f },
            new Gesture { Name = "Finger heart", Tooltip = "K-pop heart: thumb and index tips cross; the rest folded (refine the crossing by hand).", Thumb = 0.55f, Index = 0.55f, Middle = 1f, Ring = 1f, Little = 1f, },
        };

        /// <summary>
        /// Applies a gesture to the hand from its DEFAULT pose. Flexibility scales
        /// each finger's flexion range AND the adjacent-finger synergy (a curled
        /// finger drags its neighbours). The THUMB is excluded from that synergy —
        /// it flexes on its own musculature, independent of the fingers.
        /// </summary>
        public static void Apply(BaseHand hand, Gesture gesture, float flexibility)
        {
            if (hand == null || gesture == null) return;

            // Gestures are defined relative to the flat default hand — but reset the
            // FINGERS only (instantly: live save reads the joints right after).
            // ApplyDefaultPoseForeSetup would also yank the hand root back to the
            // default attach offset, destroying the author's placement.
            var defaultInfo = hand.DefaultPose != null ? hand.DefaultPose.GetHandInfo(hand.HandType) : null;
            if (defaultInfo != null) hand.ApplyFingerRotations(defaultInfo.fingerRotations, true);

            var palmFrame = PoseSceneTools.ComputePalmFrame(hand);
            var curls = new Dictionary<PoseSceneTools.FingerKind, float>
            {
                { PoseSceneTools.FingerKind.Thumb,  gesture.Thumb },
                { PoseSceneTools.FingerKind.Index,  gesture.Index },
                { PoseSceneTools.FingerKind.Middle, gesture.Middle },
                { PoseSceneTools.FingerKind.Ring,   gesture.Ring },
                { PoseSceneTools.FingerKind.Little, gesture.Little },
            };

            // Neighbour synergy: blend each finger's curl toward the average of its
            // anatomical neighbours by flexibility. A stiff hand (0) keeps the crisp
            // preset; a loose hand (1) rounds the transitions between fingers. The
            // THUMB is not in this chain — it neither drags nor is dragged.
            var order = new[]
            {
                PoseSceneTools.FingerKind.Index, PoseSceneTools.FingerKind.Middle,
                PoseSceneTools.FingerKind.Ring, PoseSceneTools.FingerKind.Little
            };
            var blended = new Dictionary<PoseSceneTools.FingerKind, float>(curls);
            for (var i = 0; i < order.Length; i++)
            {
                var neighbourSum = 0f;
                var neighbourCount = 0;
                if (i > 0) { neighbourSum += curls[order[i - 1]]; neighbourCount++; }
                if (i < order.Length - 1) { neighbourSum += curls[order[i + 1]]; neighbourCount++; }
                if (neighbourCount == 0) continue;
                var neighbourAvg = neighbourSum / neighbourCount;
                blended[order[i]] = Mathf.Lerp(curls[order[i]], neighbourAvg, flexibility * 0.3f);
            }

            var chainsByKind = new Dictionary<PoseSceneTools.FingerKind, PoseSceneTools.FingerChain>();
            foreach (var chain in PoseSceneTools.Chains(hand))
            {
                chainsByKind[chain.Kind] = chain;
                if (blended.TryGetValue(chain.Kind, out var amount) && amount > 0f)
                    PoseSceneTools.CurlFinger(chain, amount, flexibility, palmFrame);
            }

            // Convergence: curled fingers pack TOWARD the palm centre as a fist
            // closes (real fingers squeeze together — they don't curl in parallel and
            // stay splayed). Scaled by how curled the finger is.
            for (var i = 0; i < order.Length; i++)
            {
                var curl = curls[order[i]];
                if (curl < 0.5f) continue;
                if (!chainsByKind.TryGetValue(order[i], out var curled)) continue;
                PoseSceneTools.AdductFingerToward(curled, palmFrame.Center, curl * Mathf.Lerp(4f, 8f, flexibility), palmFrame);
            }

            // Adduction: an EXTENDED finger next to a FOLDED one leans inward toward
            // it (a folded neighbour pulls the straight finger sideways). This is the
            // sideways lean the flexion curl can't do — e.g. the index and little in
            // rock horns come in toward the tucked middle/ring.
            var adductDegrees = Mathf.Lerp(5f, 11f, flexibility);
            for (var i = 0; i < order.Length; i++)
            {
                if (curls[order[i]] > 0.2f) continue; // only fingers left extended
                if (!chainsByKind.TryGetValue(order[i], out var extended)) continue;

                PoseSceneTools.FingerKind? foldedNeighbour = null;
                if (i > 0 && curls[order[i - 1]] > 0.5f) foldedNeighbour = order[i - 1];
                else if (i < order.Length - 1 && curls[order[i + 1]] > 0.5f) foldedNeighbour = order[i + 1];
                if (foldedNeighbour == null || !chainsByKind.TryGetValue(foldedNeighbour.Value, out var folded)) continue;

                PoseSceneTools.AdductFingerToward(extended, folded.Joints[0].position, adductDegrees, palmFrame);
            }
        }
    }
}
#endif
