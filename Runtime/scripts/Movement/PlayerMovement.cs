using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] InputActionReference fpsMoveAction;
        [SerializeField] InputActionReference xrMoveAction;
        [SerializeField] InputActionReference fpsElevateAction;
        [SerializeField] InputActionReference freeCamAction;
        [SerializeField] PlayerInput playerInput;
        [SerializeField] CharacterController controller;
        [SerializeField] FPSCameraMovement mouseLook;
        [SerializeField] BoolEventChannelSO playerIsMovingEvent;
        [SerializeField] bool isFreeCamOn;
        [SerializeField] private float speed;
        BroadcastControlsStatus.ControlScheme controlScheme;
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
        Vector2 verticalMoveValue;
        private Transform cameraTransform;
        private const float INPUT_MULTIPLIER = 50f;

        private void Awake()
        {
            cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += ctx => OnReceivedControlSchemeChange(ctx);
            fpsElevateAction.action.performed += OnFpsElevatePerformed;
            fpsElevateAction.action.canceled += OnFpsElevateCancelled;
            fpsMoveAction.action.performed += OnFpsMovePerformed;
            freeCamAction.action.performed += ctx => ActivateFreeMove();
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
            if (fpsElevateAction != null && fpsElevateAction.action != null)
            {
                fpsElevateAction.action.performed -= OnFpsElevatePerformed;
                fpsElevateAction.action.canceled -= OnFpsElevateCancelled;
            }

            BroadcastControlsStatus.SendControlScheme -= ctx => OnReceivedControlSchemeChange(ctx);
        }

        private void ActivateFreeMove()
        {
            if (controlScheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse)
            {
                playerInput.SwitchCurrentControlScheme("FreeCam", Keyboard.current, Mouse.current);
            }
            else if (controlScheme == BroadcastControlsStatus.ControlScheme.Freecam)
            {
                playerInput.SwitchCurrentControlScheme("Keyboard&Mouse", Keyboard.current, Mouse.current);
            }
        }
        private void OnFpsElevatePerformed(InputAction.CallbackContext ctx)
        {
            SetVerticalMoveValue(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * INPUT_MULTIPLIER);
            SetIsMoving(true);
        }

        private void OnReceivedControlSchemeChange(BroadcastControlsStatus.ControlScheme controlScheme)
        {
            controller.enabled = false;
            playerInput.gameObject.transform.position = new Vector3(playerInput.gameObject.transform.position.x, 0, playerInput.gameObject.transform.position.z);
            
            this.controlScheme = controlScheme;
            controller.enabled = true;

        }
        private void OnFpsElevateCancelled(InputAction.CallbackContext ctx)
        {
            SetVerticalMoveValue(Vector2.zero);
            SetIsMoving(false);
        }

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
                Move(moveValue, verticalMoveValue);
            }

            if (!controller.isGrounded && controlScheme != BroadcastControlsStatus.ControlScheme.Freecam)
            {
                controller.Move(Vector3.down * gravity * Time.deltaTime);
            }
        }

        private void SetMoveValue(Vector2 move)
        {
            moveValue = move;
        }

        private void SetVerticalMoveValue(Vector2 move)
        {
            verticalMoveValue = move;
        }
        private void Move(Vector2 move, Vector2 verticalMove)
        {
            Vector3 forward = cameraTransform.forward;
            Vector3 right = cameraTransform.right;
            Vector3 up = cameraTransform.up;
            Vector3 moveDirection = Vector3.zero;
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.Freecam)
            {
                moveDirection = (forward * move.y) + (right * move.x) + (up * verticalMove.y);
            }
            else
            {
                moveDirection = (forward * move.y) + (right * move.x);
                moveDirection.y = 0f;

            }

            controller.Move(moveDirection.normalized * speed * Time.deltaTime);
        }

        private void SetIsMoving(bool isMoving)
        {
            this.isMoving = isMoving;
            playerIsMovingEvent?.RaiseEvent(isMoving);
        }
    }
}