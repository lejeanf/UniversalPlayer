using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
namespace jeanf.vrplayer
{
    public class PrimaryItemController : MonoBehaviour
    {
        [SerializeField] private bool useInputAction = true; 
        [SerializeField] private InputActionReference drawPrimaryItem;

        //[SerializeField] private VoidEventChannelSO _invertMouselookStateChannel;
        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
        private bool primaryItemState = false;
        private void OnEnable()
        {
            if(useInputAction) drawPrimaryItem.action.performed += ctx=> InvertState();
            _PrimaryItemStateChannel.OnEventRaised += StateOverride;

        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            if(useInputAction) drawPrimaryItem.action.performed -= ctx=> InvertState();
            _PrimaryItemStateChannel.OnEventRaised -= StateOverride;
        }

        public void Reset()
        {
            primaryItemState = false;
            SetPrimaryItemState(primaryItemState);
        }

        public void InvertState()
        {
            primaryItemState = !primaryItemState;
            SetPrimaryItemState(primaryItemState);
        }

        private void SetPrimaryItemState(bool state)
        {
            primaryItemState = state;
            _PrimaryItemStateChannel.RaiseEvent(state);
        }

        private void StateOverride(bool state)
        {
            primaryItemState = state;
        }
    }
}
