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
        [SerializeField] FPSCameraMovement mouseLook;

        [SerializeField] float speed;
        float gravity = 9.81f;
        [SerializeField] float distToGround;
        [SerializeField] float speedChangeRate;



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


            Vector3 forward = Camera.main.transform.forward;
            Vector3 right = Camera.main.transform.right;

            Vector3 moveDirection = (forward * speed * move.y) + (right * speed * move.x);


            
            Vector3 finalMoveDirection = new Vector3(moveDirection.x, 0.0f, moveDirection.z);
            controller.Move(finalMoveDirection.normalized * speed * Time.deltaTime);
        }
    }
}
