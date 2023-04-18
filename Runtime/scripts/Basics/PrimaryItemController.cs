using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

public class PrimaryItemController : MonoBehaviour
{
    [SerializeField] private bool useInputAction = true; 
    [SerializeField] private InputActionReference drawPrimaryItem;

    //[SerializeField] private VoidEventChannelSO _invertMouselookStateChannel;
    [Header("Broadcasting on Channel:")]
    [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
    private bool primaryItemState = false;
    private void OnEnable()
    {
        if(useInputAction) drawPrimaryItem.action.performed += ctx=> InvertState();
    }

    private void OnDestroy() => Unsubscribe();
    private void OnDisable() => Unsubscribe();

    private void Unsubscribe()
    {
        if(useInputAction) drawPrimaryItem.action.performed -= null;
    }

    public void Reset()
    {
        primaryItemState = false;
        Set(primaryItemState);
    }

    public void InvertState()
    {
        primaryItemState = !primaryItemState;
        Set(primaryItemState);
    }

    private void Set(bool state)
    {
        _PrimaryItemStateChannel.RaiseEvent(state);
    }
}
