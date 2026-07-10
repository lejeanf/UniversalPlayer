using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Behavior tests for the event-driven fade: the screen must go black on
    /// Loading, restore on Clear, and desaturate on HeadInWall. Runs against
    /// whichever pipeline (URP/HDRP) is active in the project.
    /// </summary>
    public class FadeMaskTests
    {
        private FadeTestRig _rig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _rig = new FadeTestRig();
            yield return null; // let Awake/first frame complete
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _rig?.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartsInLoadingState_ScreenBlack()
        {
            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Loading"),
                "FadeMask must initialize to Loading (black) so players never see an unloaded scene.");
            AssertColorNear(_rig.ReadColorFilter(), Color.black,
                "Screen is not black in the initial Loading state.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SetStateClear_FadesToVisible()
        {
            FadeMask.SetStateClear();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Clear"));
            AssertColorNear(_rig.ReadColorFilter(), Color.white,
                "Clear state should restore the color filter to white (no tint).");
            Assert.That(_rig.ReadSaturation(), Is.EqualTo(0f).Within(1f),
                "Clear state should restore saturation to 0.");
        }

        [UnityTest]
        public IEnumerator SetStateLoading_FadesToBlack()
        {
            FadeMask.SetStateClear();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            FadeMask.SetStateLoading();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Loading"));
            AssertColorNear(_rig.ReadColorFilter(), Color.black,
                "Loading state should fade the color filter to black (fade-to-black broken).");
        }

        [UnityTest]
        public IEnumerator SetStateHeadInWall_Desaturates()
        {
            FadeMask.SetStateClear();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            FadeMask.SetStateHeadInWall();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("HeadInWall"));
            Assert.That(_rig.ReadSaturation(), Is.EqualTo(-100f).Within(1f),
                "HeadInWall state should fully desaturate the view (NoPeeking effect broken).");
            AssertColorNear(_rig.ReadColorFilter(), Color.white,
                "HeadInWall desaturates but must not darken — color filter should stay white.");
        }

        private static void AssertColorNear(Color actual, Color expected, string message)
        {
            const float tolerance = 0.02f;
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), $"{message} (r) actual={actual}");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), $"{message} (g) actual={actual}");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), $"{message} (b) actual={actual}");
        }
    }
}
