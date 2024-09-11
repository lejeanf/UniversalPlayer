using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 
namespace jeanf.vrplayer
{
    public class SnapZone : MonoBehaviour
    {
        [SerializeField] private List<SnapPoint> snapPoints = new List<SnapPoint>();
        public List<SnapPoint> SnapPoints { get { return snapPoints;}}
    }
}
