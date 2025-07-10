using System.Collections.Generic;
using System.Linq;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace jeanf.universalplayer
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
        [SerializeField] private BoolFloatEventChannelSO FadeEventChannel;

        public void Teleport(TeleportInformation teleportInformation)
        {
            FadeMask.TogglePPE.Invoke(false);
            FadeEventChannel?.RaiseEvent(false, 1.0f);
            if (teleportInformation.isUsingFilter)
            {
                if (!listOfFilters.Contains(teleportInformation.filter))
                    return;
                if (_isDebug)
                    Debug.Log(
                        $"{teleportInformation.filter.filters[0]} is within the list of {this.gameObject.name}, proceeding...");
            }

            if (_isDebug)
            {
                Debug.Log($"[{gameObject.name}] destination : {teleportInformation.targetDestination.gameObject.name}, objectIsPlayer : {teleportInformation.objectIsPlayer}");
                Debug.Log($"ObjectToTeleport : {teleportInformation.objectToTeleport.name}");
            }             
            
            GameObject teleportSubject = teleportInformation.objectIsPlayer
                ? player
                : teleportInformation.objectToTeleport.gameObject;
            try
            {
                teleportSubject.GetComponent<CharacterController>().enabled = false;
            }
            catch
            {
                if (isDebug) Debug.Log("teleportation subject is not player - cannot disable player locomotion for teleportation");
            }
            teleportSubject.transform.position = teleportInformation.targetDestination.position;
            Debug.Log($"TELEPORT - player position = {teleportSubject.transform.position} && targetDestination.position = {teleportInformation.targetDestination.position}");
            teleportSubject.transform.rotation = teleportInformation.targetDestination.rotation;
            try
            {
                teleportSubject.GetComponent<CharacterController>().enabled = true;
            }
            catch
            {
                if (isDebug) Debug.Log("teleportation subject is not player - cannot disable player locomotion for teleportation");
            }

            if ( teleportInformation.objectIsPlayer ) cameraResetChannel.RaiseEvent();
            if (_isDebug) Debug.Log( $"[{teleportInformation.targetDestination.gameObject.name}] teleported {teleportSubject.gameObject.name} to {teleportInformation.targetDestination.transform.position} with rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
            //FadeEventChannel?.RaiseEvent(true, 4.0f);

        }
    }
}