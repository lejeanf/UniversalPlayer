using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] InputActionReference moveAction;
        [SerializeField] CharacterController controller;
        [SerializeField] MouseLook mouseLook;

        float speed;
        float gravity = 9.81f;
        [SerializeField] float distToGround;
        [SerializeField] float speedChangeRate;
        [SerializeField] float moveSpeed;


        bool isMoving;
        Vector2 moveValue;


        private void OnEnable()
        {
            moveAction.action.performed += ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f, true);
            moveAction.action.canceled += ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f, false);

        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            moveAction.action.performed -= ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f, true);
            moveAction.action.canceled -= ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f, false);


        }

        private void LateUpdate()
        {
            if (isMoving)
            {
                Move(moveValue);
            }

            
            if (!IsGrounded())
            {
                controller.Move(new Vector3(0.0f, -gravity, 0.0f).normalized * Time.deltaTime * 10f);
            }

        }
        private void SetMoveValue(Vector2 move, bool isMoving)
        {
            moveValue = move;
            this.isMoving = isMoving;
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position, -Vector3.up, distToGround);
        }
        private void Move(Vector2 move)
        {
            float targetSpeed = moveSpeed;
            float verticalSpeed;


            if(move == Vector2.zero) targetSpeed = 0;

            float currentHorizontalSpeed = new Vector3(controller.velocity.x, 0.0f, controller.velocity.z).magnitude;

            float speedOffset = 0.1f;

            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * move.magnitude, Time.deltaTime * speedChangeRate);

                speed = Mathf.Round(speed * 1000f) / 1000f;
            }
            else
            {
                speed = targetSpeed;
            }

            Vector3 inputDirection = new Vector3(move.x, 0.0f, move.y).normalized;
            
            
            if (move != Vector2.zero)
            {
                inputDirection = mouseLook.CameraOffset.transform.right * move.x + mouseLook.CameraOffset.transform.forward * move.y;
            }

            
            controller.Move(new Vector3(inputDirection.x, 0.0f, inputDirection.z).normalized * (speed * Time.deltaTime));
        }
    }
}
