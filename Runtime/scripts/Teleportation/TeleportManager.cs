using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer 
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
        List<InputAction> inputActions = new List<InputAction>();
        private void OnEnable()
        {
            for (var i = 0; i < teleportPositions.Count; i++)
            {
                if (i >= 10) return;
                var spawnId = i;
                
                var inputAction = new InputAction();
                inputAction.AddBinding($"<Keyboard>/{spawnId}");
                inputAction.AddBinding($"<Keyboard>/numpad{spawnId}");
                if (!inputActions.Contains(inputAction)) inputActions.Add(inputAction);

                inputAction.Enable();
                inputAction.performed += ctx => TeleportTo(spawnId);
            }
        }
        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            foreach (var t in inputActions)
            {
                t.performed -= null;
                t.Disable();
            }
        }

        private void TeleportTo(int spawnId)
        {
            if(_isDebug) Debug.Log($"teleporting to spawn nr: {spawnId}");
            teleportPositions[spawnId].GetComponent<SendTeleportTarget>().Teleport();
        }

    }
}