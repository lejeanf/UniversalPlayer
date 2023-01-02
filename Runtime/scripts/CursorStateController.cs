using System.Collections;
using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.UI;
namespace jeanf.vrplayer
{
    public class CursorStateController : MonoBehaviour
    {
        bool isCursorOn = false;
        bool isIpadOn = false;
        [SerializeField] SVGImage cursor_outter;
        [SerializeField] SVGImage cursor_center;
        enum CursorState
        {
            on_locked,
            on_free,
            off,
        }
        CursorState cursorState = CursorState.on_locked;

        private void Awake() => Init();

        void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursorState;
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetCursorState;
        }

        void Init()
        {
            cursorState = CursorState.on_locked;
            isIpadOn = false;
            isCursorOn = true;

            SetCursor(cursorState);
        }

        void SetCursorState(bool state)
        {
            isCursorOn = state;
            if (!isCursorOn) cursorState = CursorState.off;
            else { CheckIpadState(isIpadOn); }
        }

        void CheckIpadState(bool state)
        {
            if (state) cursorState = CursorState.on_free;
            else { cursorState = CursorState.on_locked; }
            SetCursor(cursorState);
        }

        void SetCursor(CursorState cursorState)
        {
            switch (cursorState)
            {
                case CursorState.on_free:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    cursor_outter.enabled = false;
                    cursor_center.enabled = false;
                    break;
                case CursorState.on_locked:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursor_outter.enabled = true;
                    cursor_center.enabled = true;
                    break;
                case CursorState.off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    cursor_outter.enabled = false;
                    cursor_center.enabled = false;
                    break;
            }
        }
    }
}