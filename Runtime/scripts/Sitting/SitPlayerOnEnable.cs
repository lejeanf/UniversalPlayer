using System.Collections;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Scenario helper: the moment this object is enabled, the player is sat on the
    /// referenced <see cref="Seat"/>. Drop it in a scene (or enable it from a scenario)
    /// whenever a sequence requires the player to start seated at a specific chair.
    /// Works in BOTH worlds: reference the Seat directly, or — when that is impossible
    /// (a seat in another additive scene, or baked into a SubScene where no GameObject
    /// exists at runtime) — set the Seat Id authored on the Seat and it resolves through
    /// the <see cref="SeatRegistry"/> (live Seats + the entity world's SeatDataBridge).
    ///
    /// Goes through the normal sit-request flow (<see cref="PlayerEvents.RaiseSitRequest"/>)
    /// and, by default, hides the placement BEHIND A FADE TO BLACK so the transition is
    /// never seen: when the screen is already black (scenario loading) it simply seats the
    /// player behind it and leaves the fade to its owner; when the world is visible it
    /// TRIGGERS the fade itself (<see cref="FadeMask.SetStateLoading"/>), seats the player
    /// once the black covers the screen, then fades back in. Being already seated elsewhere
    /// swaps seats. Additive-load safe: if the Player isn't alive yet when this enables, it
    /// waits for the SitController to appear first.
    /// </summary>
    public class SitPlayerOnEnable : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("The seat the player is forced onto when this object is enabled. Leave EMPTY for a seat this scene cannot reference (another additive scene, or baked into a SubScene) and set Seat Id below instead.")]
        [Validation("Assign the target Seat — or, for a seat in another scene / baked into a SubScene (unreferencable from here), leave this empty and set its Seat Id below.")]
        [SerializeField] private Seat seat;

        [Tooltip("Target seat by its authored Seat Id (set on the Seat component) when a direct reference is impossible: seats in other additive scenes or baked into SubScenes. Ignored when a Seat is assigned above. 0 = unset.")]
        [SerializeField] private int seatId = 0;

        [Tooltip("Hide the placement behind a fade to black. Screen already black (scenario loading): seat behind it, the loading flow keeps owning the fade. World visible: fade to black, seat, fade back in. Off = seat immediately on enable, glide and all.")]
        [SerializeField] private bool fadeToBlackForPlacement = true;

        [Tooltip("Extra seconds the screen stays black after the placement before fading back in (only when this component triggered the fade itself).")]
        [SerializeField] private float holdBlackSeconds = 0.15f;

        [Tooltip("How long to keep waiting for the Player (SitController) to exist before giving up — additive scene loading can enable this object before the player scene is in.")]
        [SerializeField] private float waitTimeout = 10f;

        private Coroutine _pending;
        private bool _ownsFade; // we triggered the black — we must give it back, even if disabled mid-sequence

        private void OnEnable()
        {
            if (seat == null && seatId == 0)
            {
                Debug.LogWarning($"{LogPrefix} SitPlayerOnEnable on '{name}': no Seat assigned and no Seat Id set — " +
                    "the player cannot be seated. Assign the target Seat, or set the Seat Id authored on it " +
                    "(other-scene / SubScene-baked seats).", this);
                return;
            }
            _pending = StartCoroutine(SitWhenPlayerReady());
        }

        private void OnDisable()
        {
            if (_pending != null) StopCoroutine(_pending);
            _pending = null;
            if (_ownsFade)
            {
                // Killed mid-sequence after fading to black: never leave the screen black.
                FadeMask.SetStateClear();
                _ownsFade = false;
            }
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

            // Id-targeted seats (other scenes, SubScene-baked) resolve through the SeatRegistry —
            // BEFORE any fade, and with patience: the seat's SubScene may still be streaming in.
            // Never fade to black over a target that can't be found.
            var resolvedSeat = default(SeatData);
            var byId = seat == null;
            if (byId)
            {
                while (!SeatRegistry.TryGetSeatData(seatId, out resolvedSeat))
                {
                    if (Time.unscaledTime >= deadline)
                    {
                        Debug.LogWarning($"{LogPrefix} SitPlayerOnEnable on '{name}': Seat Id {seatId} never became " +
                            $"resolvable within {waitTimeout:F0}s — the player was NOT seated. Check that a Seat " +
                            "carries this exact id, its scene/SubScene is loaded, and (for baked seats) a " +
                            "SeatDataBridge is in the scene.", this);
                        _pending = null;
                        yield break;
                    }
                    yield return null;
                }
            }

            // Seat behind black so the placement is instant and invisible. If the screen is
            // not already black (no scenario load running), trigger the fade OURSELVES and
            // give it back afterwards; an already-black screen belongs to whoever faded it
            // (the loading flow), so it is left untouched.
            if (fadeToBlackForPlacement && !FadeMask.ScreenFaded)
            {
                FadeMask.SetStateLoading(); // warns loudly by itself when no FadeMask is set up
                _ownsFade = true;
                // ScreenFaded flips on the REQUEST — the visual takes FadeSeconds to cover
                // the screen, and seating early would show the teleport mid-fade.
                yield return new WaitForSeconds(FadeMask.FadeSeconds + 0.05f);
            }

            if (byId) SitController.Instance.RequestSit(resolvedSeat);
            else PlayerEvents.RaiseSitRequest(seat.gameObject);

            if (_ownsFade)
            {
                yield return new WaitForSeconds(holdBlackSeconds);
                FadeMask.SetStateClear();
                _ownsFade = false;
            }
            _pending = null;
        }
    }
}
