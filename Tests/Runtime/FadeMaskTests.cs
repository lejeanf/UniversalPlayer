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

        [UnityTest]
        public IEnumerator MenuOpen_FadesToBlack_AndCloseRestoresClear()
        {
            FadeMask.SetStateClear();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            PlayerEvents.RaiseMenuState(true);
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);
            AssertColorNear(_rig.ReadColorFilter(), Color.black,
                "Opening the main menu must fade the world to black (any mode).");

            PlayerEvents.RaiseMenuState(false);
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);
            AssertColorNear(_rig.ReadColorFilter(), Color.white,
                "Closing the main menu must fade back to clear.");
        }

        [UnityTest]
        public IEnumerator MenuOverlay_RemembersHeadInWall_RequestedWhileOpen()
        {
            // The tricky NoPeeking interplay: base-state changes arriving while
            // the menu is open must be REMEMBERED, not lost or applied early.
            FadeMask.SetStateClear();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            PlayerEvents.RaiseMenuState(true);
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            FadeMask.SetStateHeadInWall(); // NoPeeking fires while the menu is open
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);
            AssertColorNear(_rig.ReadColorFilter(), Color.black,
                "While the menu is open the screen must STAY black, whatever NoPeeking requests.");

            PlayerEvents.RaiseMenuState(false);
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);
            Assert.That(_rig.ReadSaturation(), Is.EqualTo(-100f).Within(1f),
                "Closing the menu must restore the REMEMBERED base state (head-in-wall desaturation), not Clear.");
            AssertColorNear(_rig.ReadColorFilter(), Color.white,
                "Head-in-wall desaturates but must not darken after the menu closes.");
        }

        [UnityTest]
        public IEnumerator MenuClose_WhileStillLoading_StaysBlack()
        {
            // Initial state is Loading (black). Opening and closing the menu
            // during a load must never reveal the half-loaded world.
            PlayerEvents.RaiseMenuState(true);
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            PlayerEvents.RaiseMenuState(false);
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);
            AssertColorNear(_rig.ReadColorFilter(), Color.black,
                "Closing the menu while the scene is still loading must keep the screen black.");
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
