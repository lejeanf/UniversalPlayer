using System.Collections;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Scenario helper: the moment this object is enabled, the player is sat on the
    /// referenced <see cref="Seat"/>. Drop it in a scene (or enable it from a scenario)
    /// whenever a sequence requires the player to start seated at a specific chair.
    ///
    /// Goes through the normal sit-request flow (<see cref="PlayerEvents.RaiseSitRequest"/>)
    /// and, by default, waits for the screen to be FADED TO BLACK before raising it — the
    /// placement is then instant and the player never sees the transition (the scenario-load
    /// pattern: fade out → this seats them → the reveal shows them already in the chair).
    /// Being already seated elsewhere swaps seats. Additive-load safe: if the Player isn't
    /// alive yet when this enables, it waits for the SitController to appear first.
    /// </summary>
    public class SitPlayerOnEnable : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("The seat the player is forced onto when this object is enabled.")]
        [Validation("A Seat is required — without it there is nothing to sit the player on and this component does nothing.")]
        [SerializeField] private Seat seat;

        [Tooltip("Wait for the screen to be faded to black before seating, so the transition is never seen (placement while black is instant). Off = seat immediately on enable, glide and all.")]
        [SerializeField] private bool waitForFadeToBlack = true;

        [Tooltip("How long to keep waiting (for the Player to exist, and for the fade to black) before falling back — additive scene loading can enable this object before the player scene is in. On timeout the player is still seated, just visibly.")]
        [SerializeField] private float waitTimeout = 10f;

        private Coroutine _pending;

        private void OnEnable()
        {
            if (seat == null)
            {
                Debug.LogWarning($"{LogPrefix} SitPlayerOnEnable on '{name}': no Seat assigned — the player cannot " +
                    "be seated. Assign the target Seat in the inspector.", this);
                return;
            }
            _pending = StartCoroutine(SitWhenPlayerReady());
        }

        private void OnDisable()
        {
            if (_pending != null) StopCoroutine(_pending);
            _pending = null;
        }

        private IEnumerator SitWhenPlayerReady()
        {
            // Additive loading: this scene can enable before the Player scene has loaded,
            // in which case nothing is listening to the sit request yet — wait for it.
            var deadline = Time.unscaledTime + waitTimeout;
            while (SitController.Instance == null)
            {
                if (Time.unscaledTime >= deadline)
                {
                    Debug.LogWarning($"{LogPrefix} SitPlayerOnEnable on '{name}': no SitController appeared within " +
                        $"{waitTimeout:F0}s — the player was NOT seated on '{seat.name}'. Is the Player " +
                        "prefab (or variant) in a loaded scene?", this);
                    _pending = null;
                    yield break;
                }
                yield return null;
            }

            // Seat only while the screen is black so the placement is instant and invisible.
            // If the fade never comes, seat anyway (the scenario NEEDS the player in the chair)
            // — visibly, with a warning naming the likely wiring gap.
            if (waitForFadeToBlack && !FadeMask.ScreenFaded)
            {
                while (!FadeMask.ScreenFaded && Time.unscaledTime < deadline) yield return null;
                if (!FadeMask.ScreenFaded)
                    Debug.LogWarning($"{LogPrefix} SitPlayerOnEnable on '{name}': the screen never faded to black " +
                        $"within {waitTimeout:F0}s — seating the player on '{seat.name}' VISIBLY. If this enable is " +
                        "part of a scenario load, make sure the loading fade runs (FadeMask), or turn " +
                        "'Wait For Fade To Black' off to accept the visible transition.", this);
            }

            PlayerEvents.RaiseSitRequest(seat.gameObject);
            _pending = null;
        }
    }
}
