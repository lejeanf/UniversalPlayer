using System;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class EventOnCollision : MonoBehaviour, IDebugBehaviour
{
    public bool isDebug
    { 
        get => _isDebug;
        set => _isDebug = value; 
    }
    [SerializeField] private bool _isDebug = false;
    
    public UnityEvent colliderEnterEvent;
    public UnityEvent colliderExitEvent;

    private Collider _collider;

    private void Awake()
    {
        _collider = this.GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider collider)
    {
        colliderEnterEvent?.Invoke();
        if(isDebug) Debug.Log($"collision enter with {collider.gameObject.name}");
    }

    private void OnTriggerExit(Collider collider)
    {
        colliderExitEvent?.Invoke();
        if(isDebug) Debug.Log($"collision exit with {collider.gameObject.name}");
    }
}
