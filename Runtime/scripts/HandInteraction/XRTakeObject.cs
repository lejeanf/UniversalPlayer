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



    public void AssignGameObjectInHand()
    {
        takeObject._objectInHand = rightInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
    }

    public void RemoveGameObjectInHand()
    {
        takeObject._objectInHand = null;
    }
}
