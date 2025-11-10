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
        [SerializeField] private float tapThreshold = 0.15f;

        private InputAction moveAction;
        private InputAction aimAction;
        private InputAction fireAction;

        private float lastLeftTapTime;
        private float lastRightTapTime;
        private bool leftTapRegistered;
        private bool rightTapRegistered;

        // Using original event names as requested
        public event Action leftTap;
        public event Action rightTap;
        public event Action OnFire; // Keeping this consistent with previous fix

        // Public properties with getters (read-only access)
        public Vector2 Move => moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 Aim => aimAction?.ReadValue<Vector2>() ?? Vector2.zero;

        void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            moveAction = playerInput.actions["Move"];
            aimAction = playerInput.actions["Aim"];
            fireAction = playerInput.actions["Fire"];
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

            // Check for left movement (A or Left Arrow)
            if (moveValue.x < -0.5f)
            {
                if (currentTime - lastLeftTapTime < doubleTapTime && !leftTapRegistered)
                {
                    Debug.Log("Left Double Tap Detected!");
                    leftTap?.Invoke();
                    leftTapRegistered = true;
                }
                lastLeftTapTime = currentTime;
            }
            // Check for right movement (D or Right Arrow)
            else if (moveValue.x > 0.5f)
            {
                if (currentTime - lastRightTapTime < doubleTapTime && !rightTapRegistered)
                {
                    Debug.Log("Right Double Tap Detected!");
                    rightTap?.Invoke();
                    rightTapRegistered = true;
                }
                lastRightTapTime = currentTime;
            }
        }

        void OnMoveCanceled(InputAction.CallbackContext ctx)
        {
            Vector2 moveValue = ctx.ReadValue<Vector2>();

            // Reset tap registration when movement is released
            if (moveValue.x >= -0.1f) // Left key released
            {
                leftTapRegistered = false;
            }
            if (moveValue.x <= 0.1f) // Right key released
            {
                rightTapRegistered = false;
            }
        }
    }
}