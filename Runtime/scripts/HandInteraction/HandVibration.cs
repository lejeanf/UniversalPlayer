using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
public class HandVibration : MonoBehaviour
{
    [SerializeField] HapticImpulsePlayer leftHandHapticImpulse;
    [SerializeField] HapticImpulsePlayer rightHandHapticImpulse;
    [Range(0.01f, 1.0f)][SerializeField] float amplitude;
    [Range(0.01f, 1.0f)][SerializeField] float duration;

    [Header("Listening On")]
    [SerializeField] StringEventChannelSO hapticFeedbackOnSpecificHandSO;
    public delegate void VibrateHandDelegate(string hand, float amplitude, float duration);

    public VibrateHandDelegate VibrateHand;
    private void OnEnable()
    {
        hapticFeedbackOnSpecificHandSO.OnEventRaised += TriggerHapticFeedback;
        VibrateHand += TriggerHapticFeedback;
    }

    private void OnDisable()
    {
        hapticFeedbackOnSpecificHandSO.OnEventRaised += TriggerHapticFeedback;
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
