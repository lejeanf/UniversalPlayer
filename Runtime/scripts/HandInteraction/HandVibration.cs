using jeanf.universalplayer;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

/// <summary>
/// Plays a haptic burst on a hand. Package code calls <see cref="VibrateHand"/>;
/// projects raise the hub's hapticFeedback channel ("Left" / "Right"), forwarded as
/// <see cref="PlayerEvents.HapticRequested"/>.
/// </summary>
public class HandVibration : MonoBehaviour
{
    [Validation("The left-hand HapticImpulsePlayer is required — every 'Left' haptic request dereferences it unguarded (a null reference throws).")]
    [SerializeField] HapticImpulsePlayer leftHandHapticImpulse;
    [Validation("The right-hand HapticImpulsePlayer is required — every 'Right' haptic request dereferences it unguarded (a null reference throws).")]
    [SerializeField] HapticImpulsePlayer rightHandHapticImpulse;
    [Range(0.01f, 1.0f)][SerializeField] float amplitude;
    [Range(0.01f, 1.0f)][SerializeField] float duration;

    public delegate void VibrateHandDelegate(string hand, float amplitude, float duration);

    public static VibrateHandDelegate VibrateHand;
    private void OnEnable()
    {
        PlayerEvents.HapticRequested += TriggerHapticFeedback;
        VibrateHand += TriggerHapticFeedback;
    }

    private void OnDisable()
    {
        PlayerEvents.HapticRequested -= TriggerHapticFeedback;
        VibrateHand -= TriggerHapticFeedback;
    }
    private void TriggerHapticFeedback(string hand)
    {
        switch (hand)
        {
            case "Right":
                rightHandHapticImpulse.SendHapticImpulse(amplitude, duration);
                break;
            case "Left":
                leftHandHapticImpulse.SendHapticImpulse(amplitude, duration);
                break;
        }
    }
    private void TriggerHapticFeedback(string hand, float duration)
    {
        switch (hand)
        {
            case "Right":
                rightHandHapticImpulse.SendHapticImpulse(amplitude, duration);
                break;
            case "Left":
                leftHandHapticImpulse.SendHapticImpulse(amplitude, duration);
                break;
        }
    }
    private void TriggerHapticFeedback(string hand, float amplitude, float duration)
    {
        switch (hand)
        {
            case "Right":
                rightHandHapticImpulse.SendHapticImpulse(amplitude, duration);
                break;
            case "Left":
                leftHandHapticImpulse.SendHapticImpulse(amplitude, duration);
                break;
        }
    }
}
