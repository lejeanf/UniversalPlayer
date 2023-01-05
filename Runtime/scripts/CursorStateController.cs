using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
namespace jeanf.vrplayer
{
    [RequireComponent(typeof(SVGImage))]
    public class CursorStateController : MonoBehaviour
    {
        private bool _isCursorOn = false;
        private bool _isIpadOn = false;
        [SerializeField] private SVGImage cursorOuter;
        [SerializeField] private SVGImage cursorCenter;

        public delegate void SetCurrentCursorState(CursorState cursorState);
        public static SetCurrentCursorState CurrentCursorState;

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        private CursorState _cursorState = CursorState.OnLocked;
        private void Awake() => Init();
        private void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursorState;
            CurrentCursorState += SetCursorState;
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

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
            _isCursorOn = state;
            if (!_isCursorOn) _cursorState = CursorState.Off;
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
            switch (cursorState)
            {
                case CursorState.OnConstrained:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    cursorOuter.enabled = false;
                    cursorCenter.enabled = false;
                    break;
                case CursorState.OnLocked:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursorOuter.enabled = true;
                    cursorCenter.enabled = true;
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursorOuter.enabled = false;
                    cursorCenter.enabled = false;
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursorOuter.enabled = true;
                    cursorCenter.enabled = true;
                    break;
            }
        }
    }
}