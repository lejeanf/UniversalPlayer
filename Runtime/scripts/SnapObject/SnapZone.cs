using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
 
namespace jeanf.universalplayer
{
    public class SnapZone : MonoBehaviour
    {     
        [SerializeField] private List<GameObject> snapPoints = new List<GameObject>();
        public List<GameObject> SnapPoints { get { return snapPoints; } }
    }

}
