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

        [SerializeField] private List<Collider> snapColliders = new List<Collider>();
        public List<Collider> SnapColliders { get { return snapColliders; } }
    }
}
