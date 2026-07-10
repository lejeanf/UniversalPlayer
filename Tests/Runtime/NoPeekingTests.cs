using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Behavior tests for NoPeeking: when the sensor (camera) enters a collider on
    /// the configured layer, the view must desaturate via FadeMask; leaving restores it.
    /// </summary>
    public class NoPeekingTests
    {
        private const int WallLayer = 15;

        private FadeTestRig _rig;
        private GameObject _wall;
        private GameObject _sensor;
        private NoPeeking _noPeeking;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new FadeTestRig();

            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "TestWall";
            _wall.layer = WallLayer;
            _wall.transform.position = Vector3.zero;
            _wall.transform.localScale = Vector3.one * 2f;

            _sensor = new GameObject("NoPeekingSensor");
            _sensor.transform.position = new Vector3(100f, 0f, 0f); // far outside the wall
            _noPeeking = _sensor.AddComponent<NoPeeking>();
            SetCollisionLayer(_noPeeking, 1 << WallLayer);

            NoPeeking.SetIsLoadingState(false);
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            NoPeeking.SetIsLoadingState(true); // restore default so other tests start from a known state
            if (_wall != null) Object.Destroy(_wall);
            if (_sensor != null) Object.Destroy(_sensor);
            _rig?.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SensorOutsideWall_ViewIsClear()
        {
            yield return Settle();

            Assert.That(_noPeeking.GetFadeState(), Is.False,
                "NoPeeking reports head-in-wall while the sensor is far from any collider. " +
                "Check sphereCheckSize and that no unexpected collider sits on the collision layer.");
            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Clear"));
        }

        [UnityTest]
        public IEnumerator SensorInsideWall_ViewDesaturates()
        {
            _sensor.transform.position = Vector3.zero; // inside the wall cube
            yield return Settle();

            Assert.That(_noPeeking.GetFadeState(), Is.True,
                "NoPeeking did not detect the wall. Physics.CheckSphere(collisionLayer) missed the collider — " +
                "verify the wall's layer is in NoPeeking.collisionLayer and the collider is not a trigger.");
            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("HeadInWall"));
            Assert.That(_rig.ReadSaturation(), Is.EqualTo(-100f).Within(1f),
                "Head is in the wall but the view did not desaturate — FadeMask link broken.");
        }

        [UnityTest]
        public IEnumerator SensorLeavesWall_ViewRecovers()
        {
            _sensor.transform.position = Vector3.zero;
            yield return Settle();
            Assert.That(_noPeeking.GetFadeState(), Is.True, "Precondition failed: wall not detected.");

            _sensor.transform.position = new Vector3(100f, 0f, 0f);
            yield return Settle();

            Assert.That(_noPeeking.GetFadeState(), Is.False,
                "NoPeeking still reports head-in-wall after leaving the collider.");
            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Clear"));
            Assert.That(_rig.ReadSaturation(), Is.EqualTo(0f).Within(1f),
                "Saturation did not recover after leaving the wall.");
        }

        [UnityTest]
        public IEnumerator SceneLoading_OverridesWallDetection_WithBlackScreen()
        {
            NoPeeking.SetIsLoadingState(true);
            yield return Settle();

            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Loading"),
                "While a scene loads, NoPeeking must force the Loading (black) state regardless of walls.");

            NoPeeking.SetIsLoadingState(false);
            yield return Settle();

            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Clear"),
                "After loading ends (sensor outside walls), the view must clear again.");
        }

        private static IEnumerator Settle()
        {
            // a few physics ticks for FixedUpdate detection, then the fade tween duration
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);
        }

        private static void SetCollisionLayer(NoPeeking noPeeking, int mask)
        {
            var field = typeof(NoPeeking).GetField("collisionLayer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "Field 'collisionLayer' not found on NoPeeking — it was renamed; update NoPeekingTests alongside the refactor.");
            field.SetValue(noPeeking, (LayerMask)mask);
        }
    }
}
