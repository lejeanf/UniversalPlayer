using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.vrplayer
{
    public class SnapZoneAuscultation : SnapZone
    {
        [SerializeField] GameObject coeur;
        [SerializeField] GameObject poumonDroit;
        [SerializeField] GameObject poumonGauche;
        public GameObject Coeur { get { return coeur; } }
        public GameObject PoumonDroit { get {  return poumonDroit; } }
        public GameObject PoumonGauche { get { return poumonGauche; } }


    }
}
