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
        [SerializeField] private bool isDebug;

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
            if(isDebug) Debug.Log($"teleported {chosenTarget.gameObject.name} to {chosenTarget.transform.position} with rotation: {chosenTarget.transform.rotation}");
            
            if (!isRotateCamera) return;
            cameraOffset.localRotation = Quaternion.Euler(chosenTarget.rotation.x, chosenTarget.rotation.y, 0);
            if(isDebug) Debug.Log($"cameraOffset rotation: {chosenTarget.transform.rotation}");
        }
    }
}