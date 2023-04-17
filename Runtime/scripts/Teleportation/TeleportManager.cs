using jeanf.vrplayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer 
{
    public class TeleportManager : MonoBehaviour
    {
        [SerializeField] List<Transform> teleportPositions;
        List<InputAction> inputActions = new List<InputAction>();

        public delegate void LoadFloor(int floorToLoad);
        public static LoadFloor loadFloor;
        private void OnEnable()
        {
            for (int i = 0; i < teleportPositions.Count; i++)
            {
                if (i >= 10) return;
                int floorNb = i;

                InputAction inputAction = new InputAction();
                inputAction.AddBinding($"<Keyboard>/{floorNb}");
                inputAction.AddBinding($"<Keyboard>/numpad{floorNb}");
                if (!inputActions.Contains(inputAction)) inputActions.Add(inputAction);

                inputAction.Enable();
                inputAction.performed += ctx => Load(floorNb);
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

        private void Load(int floorNb)
        {
            Debug.Log($"loading: {floorNb}");
            loadFloor?.Invoke(floorNb);
            teleportPositions[floorNb].GetComponent<SendTeleportTarget>().Teleport();
        }
    }
}