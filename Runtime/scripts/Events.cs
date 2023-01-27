using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Events : MonoBehaviour
{
    public void EventLog()
    {
        Debug.Log($"{gameObject.name}, Event envoyé.");
    }

    public void OnSelectInteractable()
    {
        Debug.Log($"Interaction Select");
    }

    public void OnSelectGrabbable()
    {
        Debug.Log($"Grabbable Select");
    }
}
