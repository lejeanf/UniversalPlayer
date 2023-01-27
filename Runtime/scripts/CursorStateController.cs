using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace jeanf.vrplayer
{
    public class CursorStateController : MonoBehaviour
    {
        private bool _isCursorOn = false;
        private bool _isIpadOn = false;
        [SerializeField] private SVGImage cursorImage;

        public delegate void SetCurrentCursorState(CursorState cursorState);
        public static SetCurrentCursorState CurrentCursorState;

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        private CursorState _cursorState = CursorState.OnLocked;
        void Awake() => Init();
        void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursorState;
            CurrentCursorState += SetCursorState;
        }

        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetCursorState;
            CurrentCursorState -= SetCursorState;
        }

        private void Init()
        {
            _cursorState = CursorState.OnLocked;
            _isIpadOn = false;
            _isCursorOn = true;

            SetCursor(_cursorState);
        }

        private void SetCursorState(bool state)
        {
            //Debug.Log($"SetCursorState : {state}");
            _isCursorOn = !state;
            if (!_isCursorOn)
            {
                _cursorState = CursorState.Off;
                cursorImage.enabled = false;
                //Debug.Log($"cursorImage.enabled : {cursorImage.enabled}");
            }
            else { CheckIpadState(_isIpadOn); }
        }
        private void SetCursorState(CursorState state)
        {
            SetCursor(state);
        }

        private void CheckIpadState(bool state)
        {
            _cursorState = state ? CursorState.OnConstrained : CursorState.OnLocked;
            SetCursor(_cursorState);
        }

        private void SetCursor(CursorState cursorState)
        {
            //Debug.Log($"SetCursor");
            switch (cursorState)
            {
                case CursorState.OnConstrained:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    cursorImage.enabled = false;
                    //Debug.Log($"Cursor state : OnConstrained");
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    //Cursor.visible = true;
                    cursorImage.enabled = true;
                    //Debug.Log($"Cursor state : OnLocked");
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursorImage.enabled = false;
                    //Debug.Log($"Cursor state : Off");
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursorImage.enabled = true;
                    //Debug.Log($"Cursor state : Default");
                    break;
            }
        }
    }
}