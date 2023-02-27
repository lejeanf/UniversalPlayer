using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEditor.Build;
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
        [SerializeField] private static SVGImage _cursorImage;

        public delegate void SetCurrentCursorState(CursorState cursorState);
        public static SetCurrentCursorState CurrentCursorState;

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        private CursorState _cursorState = CursorState.OnLocked;
        private  void Awake() => Init();
        private  void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursorAccordingToHmdState;
            CurrentCursorState += SetCursorState;
        }

        private  void OnDestroy() => Unsubscribe();
        private  void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetCursorAccordingToHmdState;
            CurrentCursorState -= SetCursorState;
        }

        private void Init()
        {
            _cursorImage = cursorImage;
            _cursorState = CursorState.OnLocked;
            _isIpadOn = false;
            _isCursorOn = true;

            SetCursorState(_cursorState);
        }

        private static void SetCursorAccordingToHmdState(bool state)
        {
            SetCursorState(state ? CursorState.Off : CursorState.OnLocked);
        }
        /*
        public void SetCursorState(bool state)
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
        
        private void CheckIpadState()
        {
            _cursorState = state ? CursorState.OnConstrained : CursorState.OnLocked;
            SetCursor(_cursorState);
        }
        */
        public static void SetCursorState(CursorState state)
        {
            //Debug.Log($"SetCursor");
            switch (state)
            {
                case CursorState.OnConstrained:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    _cursorImage.enabled = false;
                    //Debug.Log($"Cursor state : OnConstrained");
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    //Cursor.visible = true;
                    _cursorImage.enabled = true;
                    //Debug.Log($"Cursor state : OnLocked");
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = false;
                    //Debug.Log($"Cursor state : Off");
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    //Debug.Log($"Cursor state : Default");
                    break;
            }
        }
    }
}