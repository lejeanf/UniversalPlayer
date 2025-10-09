using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
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

        // Cache camera transform to avoid Camera.main lookups
        private Transform cameraTransform;
        private const float INPUT_MULTIPLIER = 50f;

        private void Awake()
        {
            // Cache the camera transform once
            cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            // Use proper method references instead of lambdas
            fpsMoveAction.action.performed += OnFpsMovePerformed;
            fpsMoveAction.action.canceled += OnFpsMoveCanceled;
            xrMoveAction.action.performed += OnXrMovePerformed;
            xrMoveAction.action.canceled += OnXrMoveCanceled;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (fpsMoveAction != null && fpsMoveAction.action != null)
            {
                fpsMoveAction.action.performed -= OnFpsMovePerformed;
                fpsMoveAction.action.canceled -= OnFpsMoveCanceled;
            }
            
            if (xrMoveAction != null && xrMoveAction.action != null)
            {
                xrMoveAction.action.performed -= OnXrMovePerformed;
                xrMoveAction.action.canceled -= OnXrMoveCanceled;
            }
        }

        // Event handler methods
        private void OnFpsMovePerformed(InputAction.CallbackContext ctx)
        {
            SetMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * INPUT_MULTIPLIER);
            SetIsMoving(true);
        }

        private void OnFpsMoveCanceled(InputAction.CallbackContext ctx)
        {
            SetMoveValue(Vector2.zero);
            SetIsMoving(false);
        }

        private void OnXrMovePerformed(InputAction.CallbackContext ctx)
        {
            SetIsMoving(true);
        }

        private void OnXrMoveCanceled(InputAction.CallbackContext ctx)
        {
            SetIsMoving(false);
        }

        private void LateUpdate()
        {
            if (isMoving)
            {
                Move(moveValue);
            }

            if (!controller.isGrounded)
            {
                controller.Move(Vector3.down * gravity * Time.deltaTime);
            }
        }

        private void SetMoveValue(Vector2 move)
        {
            moveValue = move;
        }

        private void Move(Vector2 move)
        {
            // Use cached camera transform
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;

            Vector3 moveDirection = (forward * move.y) + (right * move.x);
            moveDirection.y = 0f;

            controller.Move(moveDirection.normalized * speed * Time.deltaTime);
        }

        private void SetIsMoving(bool isMoving)
        {
            this.isMoving = isMoving;
            playerIsMovingEvent?.RaiseEvent(isMoving);
        }
    }
}