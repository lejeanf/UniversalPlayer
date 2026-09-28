using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Reports a hand entering / leaving a detection zone (the HandDetector prefab's
    /// trigger) over <see cref="PlayerEvents.HandEnteredDetectionZone"/> /
    /// <see cref="PlayerEvents.HandLeftDetectionZone"/>. Wire FireEventOnTrigger's
    /// enter/exit UnityEvents to the two methods — no channel asset needed.
    /// </summary>
    public class HandDetectionZoneReporter : MonoBehaviour
    {
        public void ReportHandEntered() => PlayerEvents.RaiseHandEnteredDetectionZone();
        public void ReportHandLeft() => PlayerEvents.RaiseHandLeftDetectionZone();
    }
}
