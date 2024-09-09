using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.vrplayer
{
    public class SnapPoint : MonoBehaviour
    {
        [SerializeField] private Quaternion desiredSnapRotation;
        public Quaternion DesiredSnapRotation { get { return desiredSnapRotation;}}
    }
}
