using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] InputActionReference fpsMoveAction;
        [SerializeField] InputActionReference xrMoveAction;
        [SerializeField] CharacterController controller;
        [SerializeField] FPSCameraMovement mouseLook;
        [SerializeField] BoolEventChannelSO playerIsMovingEvent;

        [SerializeField] private float speed;
        public float Speed
        {
            get => speed;
            set => speed = value;
        }
        float gravity = 9.81f;
        [SerializeField] float distToGround;
        [SerializeField] float speedChangeRate;
        bool isMoving;
        Vector2 moveValue;


        private void OnEnable()
        {
            fpsMoveAction.action.performed += ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f);
            fpsMoveAction.action.performed += ctx => SetIsMoving(true);
            xrMoveAction.action.performed += ctx => SetIsMoving(true);
            fpsMoveAction.action.canceled += ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50);
            fpsMoveAction.action.canceled += ctx => SetIsMoving(false);
            xrMoveAction.action.canceled += ctx => SetIsMoving(false);

        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            fpsMoveAction.action.performed -= ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f);
            fpsMoveAction.action.performed -= ctx => SetIsMoving(true);
            xrMoveAction.action.performed -= ctx => SetIsMoving(false);
            fpsMoveAction.action.canceled -= ctx => SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * 50f);
            fpsMoveAction.action.canceled -= ctx => SetIsMoving(false);
            xrMoveAction.action.canceled -= ctx => SetIsMoving(false);

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
        private void SetMoveValue(Vector2 move)
        {
            moveValue = move;
            
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

        private void SetIsMoving(bool isMoving)
        {
            this.isMoving = isMoving;
            playerIsMovingEvent.RaiseEvent(isMoving);
        }
    }
}
