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

        // Public properties
        public Vector2 Move => moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 Aim => aimAction?.ReadValue<Vector2>() ?? Vector2.zero;

        // NEW: Check if the fire button is currently held down
        public bool IsFiring => fireAction != null && fireAction.IsPressed();

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