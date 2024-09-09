using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 
namespace jeanf.vrplayer
{
    [DefaultExecutionOrder(2)]
    public class SnapZone : MonoBehaviour
    {
        [SerializeField] private List<SnapPoint> snapPoints = new List<SnapPoint>();
        public List<SnapPoint> SnapPoints { get { return snapPoints;}}

        
    }
}
