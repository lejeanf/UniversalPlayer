using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;

public class GrabHandPose : MonoBehaviour
{
    public HandData rightHandPose;

    private Vector3 currentHandPosition;
    private Vector3 targetHandPosition;
    private Quaternion currentHandRotation;
    private Quaternion targetHandRotation;

    private Quaternion[] currentFingerBoneRotations;
    private Quaternion[] targetFingerBoneRotations;

    private HandData GrabbingHandData;
    
    // Start is called before the first frame update
    void Start()
    {
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        grabInteractable.selectEntered.AddListener(SetupPose);
        grabInteractable.selectExited.AddListener(UnSetPose);
        
        rightHandPose.gameObject.SetActive(false);
    }

    private void SetupPose(BaseInteractionEventArgs arg)
    {
        if (arg.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor)
        {
            //HandData handData = arg.interactorObject.transform.GetComponentInChildren<HandData>();
            HandData handData = GrabbingHandData;
            //HandData handData = arg.interactorObject.transform.GetComponentInParent<HandData>();
            //Debug.Log($"arg.interactorObject.transform.gameObject.name : {arg.interactorObject.transform.gameObject.name}");
            //Debug.Log($"handData : {handData}");
            //Debug.Log($"handData gameObject : {handData.gameObject.name}");
            handData.animator.enabled = false;
            
            StoreHandData(handData, rightHandPose);
            SetHandData(handData, targetHandPosition, targetHandRotation, targetFingerBoneRotations);
        }
    }
    
    private void UnSetPose(BaseInteractionEventArgs arg)
    {
        if (arg.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor)
        {
            HandData handData = GrabbingHandData;
            handData.animator.enabled = true;
            
            SetHandData(handData, currentHandPosition, currentHandRotation, currentFingerBoneRotations);
        }
    }

    public void StoreHandData(HandData currentHand, HandData targetHand)
    {
        //currentHandPosition = new Vector3(currentHand.root.localPosition.x / currentHand.root.localScale.x, currentHand.root.localPosition.y / currentHand.root.localScale.y, currentHand.root.localPosition.z / currentHand.root.localScale.z);
        //targetHandPosition = new Vector3(targetHand.root.localPosition.x / targetHand.root.localScale.x, targetHand.root.localPosition.y / targetHand.root.localScale.y, targetHand.root.localPosition.z / targetHand.root.localScale.z);

        currentHandPosition = currentHand.root.localPosition;
        targetHandPosition = targetHand.root.localPosition;
        
        currentHandRotation = currentHand.root.localRotation;
        targetHandRotation = targetHand.root.localRotation;

        currentFingerBoneRotations = new Quaternion[currentHand.fingerBones.Length];
        targetFingerBoneRotations = new Quaternion[targetHand.fingerBones.Length];

        for (int i = 0; i < currentHand.fingerBones.Length; i++)
        {
            currentFingerBoneRotations[i] = currentHand.fingerBones[i].localRotation;
            targetFingerBoneRotations[i] = targetHand.fingerBones[i].localRotation;
        }
    }

    public void SetHandData(HandData h, Vector3 newPosition, Quaternion newRotation, Quaternion[] newBonesRotation)
    {
        SetObjectPosition(transform, h.root);

        for (int i = 0; i < newBonesRotation.Length; i++)
        {
            h.fingerBones[i].localRotation = newBonesRotation[i];
        }
    }

    public void SetHandDataComponent(HandData handData)
    {
        GrabbingHandData = handData;
    }

    public void SetObjectPosition(Transform obj, Transform offset)
    {
        obj.localPosition = offset.localPosition * -1;
        obj.rotation = offset.rotation;
    }
}
