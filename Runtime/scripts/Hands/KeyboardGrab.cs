using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
namespace jeanf.vrplayer
{
    public class KeyboardGrab : MonoBehaviour
    {
        [SerializeField] Camera camera;
        [SerializeField] private InputActionReference keyboardGrabAction;
        void OnEnable()
        {
            keyboardGrabAction.action.performed += ctx => GrabObject();
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            keyboardGrabAction.action.performed -= ctx => GrabObject();
        }

        void GrabObject()
        {
            GameObject grabbedObject = LookForObjectToGrab();
            if (grabbedObject == null) return;
            grabbedObject.transform.parent = camera.transform;
            grabbedObject.transform.localPosition = new Vector3(0, 0, .3f);
        }

        GameObject LookForObjectToGrab()
        {
            GameObject objectToGrab = null;
            RaycastHit hit;
            Ray ray = camera.ScreenPointToRay(new Vector2(.5f, .5f));

            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log(hit.transform.gameObject.name);
                if (hit.transform.gameObject.GetComponent<XRGrabInteractable>()) objectToGrab = hit.transform.gameObject;
            }

            return objectToGrab;
        }
    }
}