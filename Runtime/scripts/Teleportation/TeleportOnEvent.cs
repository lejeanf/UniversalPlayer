using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace jeanf.vrplayer
{
    public class TeleportOnEvent : TeleportEventListener, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        
        [SerializeField] private GameObject objectToTeleport;
        [SerializeField] private Transform cameraOffset;

        public void Teleport(TeleportInformation teleportInformation)
        {
            var teleportSubject = teleportInformation.objectIsPlayer ? objectToTeleport.transform : teleportInformation.objectToTeleport.transform;
            teleportSubject.position = teleportInformation.targetDestination.position;
            teleportSubject.rotation = teleportInformation.targetDestination.rotation;

            if(_isDebug) Debug.Log($"teleported {teleportSubject.gameObject.name} to {teleportInformation.targetDestination.transform.position} with rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
            
            if (!teleportInformation.alignToTarget) return;
            cameraOffset.localRotation = Quaternion.Euler(teleportInformation.targetDestination.rotation.x, teleportInformation.targetDestination.rotation.y, 0);
            if(isDebug) Debug.Log($"cameraOffset rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
        }
    }
}