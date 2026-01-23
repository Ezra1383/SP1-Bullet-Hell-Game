using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BulletHell
{
    [RequireComponent(typeof(PlayerInput))]
    public class InputReader : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private float doubleTapTime = 0.3f;

        private InputAction moveAction;
        private InputAction aimAction;
        private InputAction fireAction;

        private float lastLeftTapTime;
        private float lastRightTapTime;
        private bool leftTapRegistered;
        private bool rightTapRegistered;

        public event Action leftTap;
        public event Action rightTap;
        public event Action OnFire;

        public bool useMediaPipeInput = false;
        private Vector2 _mediaPipeMove;
        private Vector2 _mediaPipeAim;
        private bool _mediaPipeFire;

        // Public properties
        public Vector2 Move
        {
            get
            {
                if (useMediaPipeInput) return _mediaPipeMove;
                return moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            }
        }

        public Vector2 Aim
        {
            get
            {
                if (useMediaPipeInput) return _mediaPipeAim;
                return aimAction?.ReadValue<Vector2>() ?? Vector2.zero;
            }
        }

        public bool IsFiring
        {
            get
            {
                if (useMediaPipeInput) return _mediaPipeFire;
                return fireAction != null && fireAction.IsPressed();
            }
        }

        public void SetMediaPipeMove(Vector2 move) => _mediaPipeMove = move;
        public void SetMediaPipeAim(Vector2 aim) => _mediaPipeAim = aim;
        public void SetMediaPipeFire(bool fire) => _mediaPipeFire = fire;

        void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            if (playerInput == null)
            {
                Debug.LogError("PlayerInput component not found on InputReader!");
                return;
            }

            if (playerInput.actions == null)
            {
                Debug.LogError("PlayerInput has no actions assigned! Assign an Input Actions asset.");
                return;
            }

            try
            {
                moveAction = playerInput.actions["Move"];
                aimAction = playerInput.actions["Aim"];
                fireAction = playerInput.actions["Fire"];
                Debug.Log("Input actions loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load input actions: {e.Message}");
            }
        }

        void OnEnable()
        {
            if (moveAction != null)
            {
                moveAction.started += OnMoveStarted;
                moveAction.canceled += OnMoveCanceled;
            }

            if (fireAction != null)
            {
                fireAction.performed += OnFirePerformed;
            }
        }

        void OnDisable()
        {
            if (moveAction != null)
            {
                moveAction.started -= OnMoveStarted;
                moveAction.canceled -= OnMoveCanceled;
            }

            if (fireAction != null)
            {
                fireAction.performed -= OnFirePerformed;
            }
        }

        void OnFirePerformed(InputAction.CallbackContext ctx)
        {
            OnFire?.Invoke();
        }

        void OnMoveStarted(InputAction.CallbackContext ctx)
        {
            Vector2 moveValue = ctx.ReadValue<Vector2>();
            float currentTime = Time.time;

            if (moveValue.x < -0.5f)
            {
                if (currentTime - lastLeftTapTime < doubleTapTime && !leftTapRegistered)
                {
                    leftTap?.Invoke();
                    leftTapRegistered = true;
                }
                lastLeftTapTime = currentTime;
            }
            else if (moveValue.x > 0.5f)
            {
                if (currentTime - lastRightTapTime < doubleTapTime && !rightTapRegistered)
                {
                    rightTap?.Invoke();
                    rightTapRegistered = true;
                }
                lastRightTapTime = currentTime;
            }
        }

        void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            leftTapRegistered = false;
            rightTapRegistered = false;
        }
    }
}