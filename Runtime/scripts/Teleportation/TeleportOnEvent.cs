using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;

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

        [Header("Fade Settings")]
        [SerializeField] private float fadeInDuration = 0.2f;
        [SerializeField] private float fadeOutDuration = 0.2f;

        [Header("Broadcasting on:")]
        [SerializeField] private VoidEventChannelSO cameraResetChannel;

        private Coroutine _teleportCoroutine;

        private void OnDestroy()
        {
            CleanupCoroutine();
        }

        private void OnDisable()
        {
            CleanupCoroutine();
        }

        private void CleanupCoroutine()
        {
            if (_teleportCoroutine != null)
            {
                StopCoroutine(_teleportCoroutine);
                _teleportCoroutine = null;
            }
        }

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

            if (_isDebug)
            {
                Debug.Log($"[{gameObject.name}] destination : {teleportInformation.targetDestination.gameObject.name}, objectIsPlayer : {teleportInformation.objectIsPlayer}");
                Debug.Log($"ObjectToTeleport : {teleportInformation.objectToTeleport.name}");
            }

            CleanupCoroutine();
            
            _teleportCoroutine = StartCoroutine(TeleportWithFade(teleportInformation));
        }

        private IEnumerator TeleportWithFade(TeleportInformation teleportInformation)
        {
            FadeMask.SetStateLoading();
            if (_isDebug) Debug.Log("TeleportOnEvent: Fading to black...");
            
            yield return new WaitForSeconds(fadeInDuration);

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
            teleportSubject.transform.rotation = teleportInformation.targetDestination.rotation;
            
            if (_isDebug) Debug.Log($"TELEPORT - player position = {teleportSubject.transform.position} && targetDestination.position = {teleportInformation.targetDestination.position}");
            
            try
            {
                teleportSubject.GetComponent<CharacterController>().enabled = true;
            }
            catch
            {
                if (isDebug) Debug.Log("teleportation subject is not player - cannot enable player locomotion after teleportation");
            }
            
            if (teleportInformation.objectIsPlayer) 
                cameraResetChannel.RaiseEvent();

            yield return new WaitForSeconds(0.1f);

            FadeMask.SetStateClear();
            if (_isDebug) Debug.Log("TeleportOnEvent: Fading to clear...");
            
            if (_isDebug) Debug.Log($"[{teleportInformation.targetDestination.gameObject.name}] teleported {teleportSubject.gameObject.name} to {teleportInformation.targetDestination.transform.position} with rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
            
            _teleportCoroutine = null;
        }
    }
}