using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.EventSystem;

public class ControlSwapDetector : MonoBehaviour
{
    [Header("Listening On")]
    [SerializeField] private StringEventChannelSO activeSchemeChannelSO;


    private void OnEnable()
    {
        activeSchemeChannelSO.OnEventRaised += ctx => PrintCurrentControlScheme(ctx);
    }

    private void OnDisable() => Unsubscribe();

    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        activeSchemeChannelSO.OnEventRaised += ctx => PrintCurrentControlScheme(ctx);

    }


    private void PrintCurrentControlScheme(string scheme)
    {
        Debug.Log("active control scheme is: " + scheme);
    }
}
