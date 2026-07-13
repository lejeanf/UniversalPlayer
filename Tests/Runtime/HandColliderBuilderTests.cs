using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// HandColliderBuilder: the bind-pose MeshCollider is removed and each finger
    /// PHALANX bone (Proximal/Intermediate/Distal) gets a BoxCollider spanning to the
    /// next joint. Anatomy contract from the user: metacarpals/carpals (bones inside
    /// the palm) get NO collider — fingers only.
    /// </summary>
    public class HandColliderBuilderTests
    {
        private GameObject _hand;

        [TearDown]
        public void TearDown()
        {
            if (_hand != null) Object.DestroyImmediate(_hand);
        }

        private Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = localPosition;
            return bone;
        }

        private Transform BuildRig()
        {
            _hand = new GameObject("Hand");
            _hand.AddComponent<MeshCollider>();
            var wrist = Bone("L_Wrist", _hand.transform, Vector3.zero);

            // Index finger: metacarpal (palm) -> proximal -> intermediate -> distal -> tip
            var metacarpal = Bone("L_IndexMetacarpal", wrist, new Vector3(0f, 0f, 0.02f));
            var proximal = Bone("L_IndexProximal", metacarpal, new Vector3(0f, 0f, 0.05f));
            var intermediate = Bone("L_IndexIntermediate", proximal, new Vector3(0f, 0f, 0.03f));
            var distal = Bone("L_IndexDistal", intermediate, new Vector3(0f, 0f, 0.02f));
            Bone("L_IndexTip", distal, new Vector3(0f, 0f, 0.015f));
            return wrist;
        }

        [UnityTest]
        public IEnumerator PhalangesGetBoxes_PalmBonesAndMeshColliderDoNot()
        {
            var wrist = BuildRig();

            var built = HandColliderBuilder.ReplaceWithFingerBoxes(_hand.transform, wrist);
            yield return null;

            Assert.That(_hand.GetComponentsInChildren<MeshCollider>(true), Is.Empty,
                "The bind-pose MeshCollider must be removed — it never follows the fingers.");
            Assert.That(built, Is.EqualTo(3), "Expected one box per phalanx (proximal, intermediate, distal).");

            Assert.That(wrist.Find("L_IndexMetacarpal").GetComponent<BoxCollider>(), Is.Null,
                "Metacarpals live inside the palm — anatomy says no collider there.");
            Assert.That(wrist.GetComponentInChildren<Transform>(true), Is.Not.Null); // rig sanity

            var proximalBox = _hand.transform.Find("L_Wrist/L_IndexMetacarpal/L_IndexProximal").GetComponent<BoxCollider>();
            Assert.That(proximalBox, Is.Not.Null, "The proximal phalanx did not receive a BoxCollider.");
            Assert.That(proximalBox.center.z, Is.EqualTo(0.015f).Within(1e-5f),
                "The box must span from the bone toward the NEXT joint (center at half the segment).");
            Assert.That(proximalBox.size.z, Is.EqualTo(0.03f).Within(1e-5f),
                "The box length must equal the distance to the next joint.");

            var tip = _hand.transform.Find("L_Wrist/L_IndexMetacarpal/L_IndexProximal/L_IndexIntermediate/L_IndexDistal/L_IndexTip");
            Assert.That(tip.GetComponent<BoxCollider>(), Is.Null, "Tip bones have no segment to span — no box.");
        }

        [UnityTest]
        public IEnumerator UnknownRig_BuildsNothing_AndWarnsInsteadOfThrowing()
        {
            _hand = new GameObject("WeirdHand");
            _hand.AddComponent<MeshCollider>();
            var root = Bone("Bone01", _hand.transform, Vector3.zero);
            Bone("Bone02", root, new Vector3(0f, 0f, 0.05f));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no phalanx bones found"));
            var built = HandColliderBuilder.ReplaceWithFingerBoxes(_hand.transform, root);
            yield return null;

            Assert.That(built, Is.Zero);
            Assert.That(_hand.GetComponentsInChildren<MeshCollider>(true), Is.Empty);
        }
    }
}
