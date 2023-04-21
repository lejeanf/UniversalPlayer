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

        [Header("Broadcasting on:")]
        [SerializeField] private VoidEventChannelSO cameraResetChannel;

        public void Teleport(TeleportInformation teleportInformation)
        {
            if (teleportInformation.isUsingFilter)
            {
                if (!listOfFilters.Contains(teleportInformation.filter))
                    return;
                if (_isDebug)
                    Debug.Log(
                        $"{teleportInformation.filter.filters[0]} is within the list of {this.gameObject.name}, proceeding...");
            }

            var teleportSubject = teleportInformation.objectIsPlayer
                ? player.transform
                : teleportInformation.objectToTeleport.transform;
            teleportSubject.position = teleportInformation.targetDestination.position;
            teleportSubject.rotation = teleportInformation.targetDestination.rotation;

            if ( teleportInformation.objectIsPlayer ) cameraResetChannel.RaiseEvent();
            if (_isDebug) Debug.Log( $"[{teleportInformation.targetDestination.gameObject.name}] teleported {teleportSubject.gameObject.name} to {teleportInformation.targetDestination.transform.position} with rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");


        }
    }
}