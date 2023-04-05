using UnityEngine;
using UnityEngine.XR;

public class DetectUserPresence : MonoBehaviour
{
    private static InputDevice headDevice;
    public DetectUserPresence()
    {
        if (headDevice == null)
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }
    }

    public static bool IsHMDMounted()
    {
    
        if (headDevice == null || headDevice.isValid == false)
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }
        if (headDevice != null)
        {
            var presenceFeatureSupported = headDevice.TryGetFeatureValue(CommonUsages.userPresence, out var userPresent);
            if (headDevice.isValid && presenceFeatureSupported)
            {
                return userPresent;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }
}