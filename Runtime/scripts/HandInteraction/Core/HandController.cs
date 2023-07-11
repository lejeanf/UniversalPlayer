using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(ActionBasedController))]
public class HandController : MonoBehaviour
{
    private ActionBasedController _controller; 
    [SerializeField] private Hand _hand;

    private void Start()
    {
        _controller = GetComponent<ActionBasedController>();
        _hand = GetComponentInChildren<Hand>();
    }

    private void Update()
    {
        if(!_hand) _hand = GetComponentInChildren<Hand>();
        else
        {
            _hand.SetGrip(_controller.selectAction.action.ReadValue<float>());
            _hand.SetTrigger(_controller.activateAction.action.ReadValue<float>());
        }

    }
}
