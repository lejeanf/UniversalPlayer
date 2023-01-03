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

                InputAction inputAction = new InputAction();
                int floorNb = i + 1;
                inputAction.AddBinding($"<Keyboard>/{floorNb}");
                inputAction.AddBinding($"<Keyboard>/numpad{floorNb}");
                if (!inputActions.Contains(inputAction)) inputActions.Add(inputAction);

                inputAction.Enable();
                inputAction.performed += ctx => Load(floorNb);
            }
        }
        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            for (int i = 0; i < inputActions.Count; i++)
            {
                inputActions[i].performed -= ctx => Load(i + 1);
                inputActions[i].Disable();
            }
        }

        void Load(int floorNb)
        {
            ResetCameraOffset.resetCameraOffset?.Invoke();
            loadFloor?.Invoke(floorNb);
            teleportPositions[floorNb - 1].GetComponent<SendTeleportTarget>().Teleport();
        }
    }
}