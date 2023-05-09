using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Transformers;

public class PoseContainer : MonoBehaviour, IDebugBehaviour
{
    
    public bool isDebug
    {
        get => _isDebug;
        set => _isDebug = value;
    }
    [SerializeField] private bool _isDebug = false;
    
    // The pose is when this object is grabbed
    public Pose pose = null;
    
    // The interactor we react to
    private XRBaseInteractor rightInteractor = null;
    
    
    /*
    public override void Process(XRGrabInteractable grabInteractable, XRInteractionUpdateOrder.UpdatePhase updatePhase, ref UnityEngine.Pose targetPose, ref Vector3 localScale)
    
    {
        ApplyOffset(grabInteractable);
    }
    */

    public void SetXRDirectInteractor(XRBaseInteractor xrBaseInteractor)
    {
        rightInteractor = xrBaseInteractor;
        if(_isDebug) Debug.Log($"targetInteractor: {xrBaseInteractor.gameObject.name}");
    }

    public void ApplyOffset(XRGrabInteractable grabInteractable)
    {
        if(_isDebug) Debug.Log($"apply offset start.");
        
        HandInfo handInfo = pose.GetHandInfo(HandType.Right);
        var objectRotation = handInfo.attachRotation;
        var objectPosition = handInfo.attachPosition;

        // Set the position and rotach of attach
        grabInteractable.transform.localPosition = objectPosition;
        grabInteractable.transform.localRotation = objectRotation;
        
        if(_isDebug) Debug.Log($"overriding: pos[{objectPosition}], rot[{objectRotation}]");
    }
}
