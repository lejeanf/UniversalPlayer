using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using jeanf.validationTools;
 
namespace jeanf.universalplayer
{
    public class SnapZone : MonoBehaviour
    {     
        [Validation("Snap points are required — a SnapObject entering this zone finds no nearest point and throws a null reference on snap.")]
        [SerializeField] private List<GameObject> snapPoints = new List<GameObject>();
        public List<GameObject> SnapPoints { get { return snapPoints; } }
    }

}
