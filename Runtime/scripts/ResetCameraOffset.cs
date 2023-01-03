using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.vrplayer 
{
    public class ResetCameraOffset : MonoBehaviour
    {
        public delegate void ResetOffset();
        public static ResetOffset resetCameraOffset;

        [SerializeField] Camera camera;
        [SerializeField] Transform cameraOffset;

        Transform originalCameraOffset;

        private void Awake()
        {
            originalCameraOffset = cameraOffset;
        }

        void OnEnable()
        {
            resetCameraOffset += Reset;
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            resetCameraOffset -= Reset;
        }

        void Reset()
        {
            cameraOffset.transform.localPosition = originalCameraOffset.localPosition;
            cameraOffset.transform.localRotation = Quaternion.Euler(
                new Vector3(
                    cameraOffset.transform.localRotation.x *0, 
                    cameraOffset.transform.localRotation.y *0, 
                    cameraOffset.transform.localRotation.z *0)
                );
            camera.fieldOfView = 60f;
        }
    }
}