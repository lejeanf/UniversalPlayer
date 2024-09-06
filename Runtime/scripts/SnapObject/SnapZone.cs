using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 
namespace jeanf.vrplayer
{
    [DefaultExecutionOrder(2)]
    public class SnapZone : MonoBehaviour
    {
        [SerializeField] private List<GameObject> snapPoints = new List<GameObject>();
        public List<GameObject> SnapPoints { get { return snapPoints;}}

        
    }
}
