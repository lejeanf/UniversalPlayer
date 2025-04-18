using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
namespace jeanf.universalplayer
{
    [System.Obsolete("This is an obsolete method")]
    public class KeyboardGrab : MonoBehaviour
    {
        /*
        [SerializeField] Camera camera;
        [SerializeField] private InputActionReference keyboardGrabAction;
        private void OnEnable()
        {
            keyboardGrabAction.action.performed += ctx => GrabObject();
        }
        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();
        private void Unsubscribe()
        {
            keyboardGrabAction.action.performed -= null;
        }

        private void GrabObject()
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
        */
    }
}