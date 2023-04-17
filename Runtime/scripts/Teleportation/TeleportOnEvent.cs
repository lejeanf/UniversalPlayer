using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace jeanf.vrplayer
{
    public class TeleportOnEvent : MonoBehaviour
    {
        [SerializeField] GameObject objectToTeleport;

        private void OnEnable()
        {
            SendTeleportTarget.teleportPlayer += Teleport;
        }
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();
        private void Unsubscribe()
        {
            SendTeleportTarget.teleportPlayer -= null;
        }

        public void Teleport(Transform teleportTarget, bool isRotateCamera)
        {
            if (!teleportTarget) return;
            var chosenTarget = teleportTarget;
            if (!objectToTeleport) return;

            objectToTeleport.transform.position = chosenTarget.position;
            objectToTeleport.transform.rotation = chosenTarget.rotation;
            if(isRotateCamera && Camera.main != null) Camera.main.transform.localRotation = Quaternion.Euler(chosenTarget.rotation.x, chosenTarget.rotation.y, 0);
        }
    }
}