using System.Collections.Generic;
using System.Linq;
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
        
        [SerializeField] private GameObject player;
        [SerializeField] private Transform cameraOffset;
        [SerializeField] private List<FilterSO> listOfFilters;

        public void Teleport(TeleportInformation teleportInformation)
        {
            if(_isDebug) Debug.Log($"message received: request to teleport smth to {teleportInformation.targetDestination.gameObject.name}, filter: {teleportInformation.filter.filters[0]}");
            if (!listOfFilters.Contains(teleportInformation.filter)) return;
            if(_isDebug) Debug.Log($"{teleportInformation.filter.filters[0]} is within the list of {this.gameObject.name}, proceeding...");
            
            var teleportSubject = teleportInformation.objectIsPlayer ? player.transform : teleportInformation.objectToTeleport.transform;
            teleportSubject.position = teleportInformation.targetDestination.position;
            teleportSubject.rotation = teleportInformation.targetDestination.rotation;

            if(_isDebug) Debug.Log($"teleported {teleportSubject.gameObject.name} to {teleportInformation.targetDestination.transform.position} with rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
            
            if ( !teleportInformation.alignToTarget ) return;
            if ( cameraOffset && teleportInformation.targetDestination) cameraOffset.localRotation = Quaternion.Euler(teleportInformation.targetDestination.rotation.x, teleportInformation.targetDestination.rotation.y, 0);
            if ( isDebug ) Debug.Log($"cameraOffset rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
        }
    }
}