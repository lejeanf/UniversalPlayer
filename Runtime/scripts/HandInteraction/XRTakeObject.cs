using jeanf.vrplayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
public class XRTakeObject : MonoBehaviour
{
    [SerializeField] XRDirectInteractor rightInteractor;
    [SerializeField] XRDirectInteractor leftInteractor;
    TakeObject takeObject;

    private void Awake()
    {
        takeObject = GetComponent<TakeObject>();
    }



    public void AssignGameObjectInRightHand()
    {
        takeObject._objectInXrRightHand = rightInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
    }

    public void RemoveGameObjectInRightHand()
    {
        takeObject._objectInXrRightHand = null;
    }

    public void AssignGameObjectInLeftHand()
    {
        takeObject._objectInXrLeftHand = rightInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
    }

    public void RemoveGameObjectInLeftHand()
    {
        takeObject._objectInXrLeftHand = null;
    }
}
