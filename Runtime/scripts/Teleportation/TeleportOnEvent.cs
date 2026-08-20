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

        [Tooltip("On: this listener teleports the player (player root required). Off: it only teleports the objects carried by the events (object-only listener).")]
        [SerializeField] private bool teleportsPlayer = true;
        [Validation("The player root is required — player teleports have nothing to move without it.", RequiredIf = nameof(teleportsPlayer))]
        [SerializeField] private GameObject player;
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
            if (teleportInformation.objectIsPlayer && !teleportsPlayer)
            {
                if (_isDebug) Debug.Log($"[{gameObject.name}] ignoring player teleport — this listener is object-only (Teleports Player is off).");
                return;
            }

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

            var teleportSubject = teleportInformation.objectIsPlayer
                ? player
                : teleportInformation.objectToTeleport != null ? teleportInformation.objectToTeleport.gameObject : null;
            if (teleportSubject == null)
            {
                Debug.LogError(teleportInformation.objectIsPlayer
                    ? $"[{gameObject.name}] player teleport received but the Player field is not assigned — nothing to move."
                    : $"[{gameObject.name}] object teleport received but the event carries no objectToTeleport — nothing to move.", this);
                return;
            }

            CleanupCoroutine();

            LastHandledFrame = Time.frameCount;
            _teleportCoroutine = StartCoroutine(TeleportWithFade(teleportInformation, teleportSubject));
        }

        private IEnumerator TeleportWithFade(TeleportInformation teleportInformation, GameObject teleportSubject)
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

            // Perform the actual teleport — a CharacterController (player) must be disabled
            // while its transform is moved, or it snaps the transform back.
            var characterController = teleportSubject.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            teleportSubject.transform.position = teleportInformation.targetDestination.position;
            teleportSubject.transform.rotation = teleportInformation.targetDestination.rotation;

            if (_isDebug) Debug.Log($"TELEPORT - subject position = {teleportSubject.transform.position} && targetDestination.position = {teleportInformation.targetDestination.position}");

            if (characterController != null) characterController.enabled = true;

            if (teleportInformation.objectIsPlayer)
            {
                // Authoritative "the player DID move" signal (the bridge also forwards the
                // project channel, but only when one is wired). A seated player who gets
                // teleported is standing by definition — SitController releases the seat on this.
                PlayerEvents.RaisePlayerTeleported(teleportInformation);
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