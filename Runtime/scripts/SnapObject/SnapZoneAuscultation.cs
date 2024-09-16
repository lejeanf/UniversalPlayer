using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.HID;

namespace jeanf.vrplayer
{
    public class SnapZoneAuscultation : SnapZone
    {
        [SerializeField] private List<GameObject> organs = new List<GameObject>();
        public List<GameObject> Organs { get { return organs; }}


        public GameObject GetNearestOrgan(GameObject gameObjectToCompare)
        {
            float minDistance = Mathf.Infinity;

            GameObject nearestOrgan = null;
            foreach (GameObject organ in organs)
            {
                float distance = Vector3.Distance(gameObjectToCompare.transform.position, organ.transform.position);

                if (distance < minDistance)
                {
                    minDistance = distance;

                    nearestOrgan = organ;
                }
            }

            return nearestOrgan;
        }

    }
}
