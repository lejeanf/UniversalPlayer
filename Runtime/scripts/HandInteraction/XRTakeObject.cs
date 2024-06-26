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
        takeObject._objectInHand = rightInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
    }

    public void RemoveGameObjectInRightHand()
    {
        takeObject._objectInHand = null;
    }

    public void AssignGameObjectInLeftHand()
    {
        takeObject._objectInHand = leftInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
    }

    public void RemoveGameObjectInLeftHand()
    {
        takeObject._objectInHand = null;
    }
}
