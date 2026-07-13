using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer 
{
    public class TeleportManager : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        
        [SerializeField] List<Transform> teleportPositions;
        readonly List<InputAction> inputActions = new List<InputAction>();

        // actions are built once and enabled/disabled with the component; the old code
        // rebuilt (and re-subscribed) fresh actions on every OnEnable and never disposed them
        private void BuildActionsOnce()
        {
            if (inputActions.Count > 0) return;
            for (var i = 0; i < Mathf.Min(teleportPositions.Count, 10); i++)
            {
                var spawnId = i;
                var inputAction = new InputAction($"TeleportToSpawn{spawnId}");
                inputAction.AddBinding($"<Keyboard>/{spawnId}");
                inputAction.AddBinding($"<Keyboard>/numpad{spawnId}");
                inputAction.performed += ctx => TeleportTo(spawnId);
                inputActions.Add(inputAction);
            }
        }

        private void OnEnable()
        {
            BuildActionsOnce();
            foreach (var action in inputActions) action.Enable();
        }

        private void OnDisable()
        {
            foreach (var action in inputActions) action.Disable();
        }

        private void OnDestroy()
        {
            foreach (var action in inputActions) action.Dispose();
            inputActions.Clear();
        }

        private void TeleportTo(int spawnId)
        {
            if(_isDebug) Debug.Log($"teleporting to spawn nr: {spawnId}");
            var target = teleportPositions[spawnId] != null ? teleportPositions[spawnId].GetComponent<SendTeleportTarget>() : null;
            if (target == null)
            {
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} TeleportManager: spawn {spawnId} has no SendTeleportTarget — nothing to teleport to.", this);
                return;
            }
            target.Teleport();
        }

    }
}