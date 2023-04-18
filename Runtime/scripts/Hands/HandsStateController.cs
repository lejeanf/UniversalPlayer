using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.vrplayer 
{
    public class HandsStateController : MonoBehaviour
    {
        [SerializeField] GameObject hand_left;
        [SerializeField] GameObject hand_right;
        private void Awake()
        {
            SetHandsState(false);
        }
        void OnEnable()
        {
            //BroadcastHmdStatus.hmdStatus += SetHandsState;
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            //BroadcastHmdStatus.hmdStatus -= SetHandsState;
        }

        public void SetHandsState(bool state)
        {
            hand_left.SetActive(state);
            hand_right.SetActive(state);
        }
    }
}