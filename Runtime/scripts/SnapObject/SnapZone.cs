using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 
namespace jeanf.vrplayer
{
    [DefaultExecutionOrder(2)]
    public class SnapZone : MonoBehaviour
    {
        delegate void SendZone();
        public static event Action<SnapZone> OnEnableSnapZone;
        public GameObject body;
        [SerializeField] private Quaternion snapObjectRotationValue;
        public Quaternion SnapObjectRotationValue { get { return snapObjectRotationValue; } private set { }}
        [SerializeField] private Vector3 snapObjectPositionValue;
        public Vector3 SnapObjectPositionValue { get { return snapObjectPositionValue; } private set { } }

        private void OnEnable()
        {
            OnEnableSnapZone.Invoke(this);
        }
    }
}
