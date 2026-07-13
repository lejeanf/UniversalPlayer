using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    public class NoPeeking : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        
        [SerializeField] private LayerMask collisionLayer;
        [SerializeField] private float sphereCheckSize = .15f;

        [SerializeField] private bool _fadeState = false;
        private bool _fadeStateLastValue = false;
        private bool _wasSceneLoadingLastFrame;
        private static bool _isSceneLoading = true;
        
        private enum SentState { None, Loading, Clear, HeadInWall }
        private SentState _lastSentState = SentState.None;

        private void Awake()
        {
            if (collisionLayer.value == 0)
            {
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} NoPeeking on '{name}' has collisionLayer set to Nothing — " +
                    "head-in-wall detection is disabled. Set it to your walls' layer on the Player prefab variant of this project.", this);
            }
        }

        private void FixedUpdate()
        {
            _fadeStateLastValue = _fadeState;
            bool wasLoadingLastFrame = _wasSceneLoadingLastFrame;
            _wasSceneLoadingLastFrame = _isSceneLoading;

            if (_isSceneLoading)
            {
                _fadeState = true;
            }
            else
            {
                _fadeState = Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore);
            }

            SentState targetState;
            if (_isSceneLoading)
            {
                targetState = _fadeState ? SentState.Loading : SentState.Clear;
            }
            else
            {
                targetState = _fadeState ? SentState.HeadInWall : SentState.Clear;
            }

            if (targetState == _lastSentState) return;

            _lastSentState = targetState;

            switch (targetState)
            {
                case SentState.Loading:
                    FadeMask.SetStateLoading();
                    break;
                case SentState.Clear:
                    FadeMask.SetStateClear();
                    break;
                case SentState.HeadInWall:
                    FadeMask.SetStateHeadInWall();
                    break;
            }

            if (isDebug) Debug.Log($"NoPeeking: State changed to {targetState}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, .75f);
            Gizmos.DrawSphere(transform.position, sphereCheckSize);
        }
#endif

        private void OnEnable()
        {
            // Piped from the project's SceneIsLoading channel by the PlayerEventBridge —
            // replaces the BoolEventListener + UnityEvent that used to sit on the prefab.
            PlayerEvents.SceneLoadingChanged += SetIsLoadingState;
        }

        private void OnDisable()
        {
            PlayerEvents.SceneLoadingChanged -= SetIsLoadingState;
        }

        public static void SetIsLoadingState(bool isLoading)
        {
            _isSceneLoading = isLoading;
        }
        public static bool IsCurrentlyLoading()
        {
            return _isSceneLoading;
        }
        public bool GetFadeState()
        {
            return _fadeState;
        }
    }
}