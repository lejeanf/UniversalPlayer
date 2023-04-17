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
        [SerializeField] private GameObject objectToTeleport;
        [SerializeField] private Transform cameraOffset;

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

        public void Teleport(Transform chosenTarget, bool isRotateCamera)
        {
            if (!objectToTeleport) return;

            objectToTeleport.transform.position = chosenTarget.position;
            objectToTeleport.transform.rotation = chosenTarget.rotation;
            if(isRotateCamera) cameraOffset.localRotation = Quaternion.Euler(chosenTarget.rotation.x, chosenTarget.rotation.y, 0);
        }
    }
}