using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using jeanf.validationTools;
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

        [Validation("The player root is required — teleports have nothing to move without it.")]
        [SerializeField] private GameObject player;
        [Validation("The camera offset transform is required to keep the view height after a teleport.")]
        [SerializeField] private Transform cameraOffset;
        [SerializeField] private List<FilterSO> listOfFilters;

        [Header("Fade Settings")]
        [SerializeField] private float fadeInDuration = 0.2f;

        [Header("Broadcasting on:")]
        // Camera reset after a player teleport goes through PlayerEvents (bridge forwards it).

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

        /// <summary>Frame index of the last teleport any TeleportOnEvent accepted — lets SendTeleportTarget warn when a teleport event found no taker.</summary>
        public static int LastHandledFrame { get; private set; } = -1;

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

            LastHandledFrame = Time.frameCount;
            _teleportCoroutine = StartCoroutine(TeleportWithFade(teleportInformation));
        }

        private IEnumerator TeleportWithFade(TeleportInformation teleportInformation)
        {
            if (teleportInformation.shouldFade)
            {
                FadeMask.SetStateLoading();
                if (_isDebug) Debug.Log("TeleportOnEvent: Fading to black...");
                yield return new WaitForSeconds(fadeInDuration);
            }
            else
            {
                if (_isDebug) Debug.Log("TeleportOnEvent: Skipping fade (external system handling it)");
            }

            // Perform the actual teleport
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
            {
                PlayerEvents.RaiseCameraReset();
            }

            if (teleportInformation.shouldFade)
            {
                yield return new WaitForSeconds(0.1f);
                FadeMask.SetStateClear();
                if (_isDebug) Debug.Log("TeleportOnEvent: Fading to clear...");
            }
            
            if (_isDebug) Debug.Log($"[{teleportInformation.targetDestination.gameObject.name}] teleported {teleportSubject.gameObject.name} to {teleportInformation.targetDestination.transform.position} with rotation: {teleportInformation.targetDestination.transform.rotation.eulerAngles}");
            
            _teleportCoroutine = null;
        }
    }
}