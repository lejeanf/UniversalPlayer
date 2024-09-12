using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 
namespace jeanf.vrplayer
{
    public class SnapZone : MonoBehaviour
    {
        [SerializeField] private List<GameObject> snapPoints = new List<GameObject>();
        public List<GameObject> SnapPoints { get { return snapPoints;}}

        [SerializeField] private GameObject lookTarget;

        public GameObject LookTarget { get { return lookTarget; } }
    }
}
