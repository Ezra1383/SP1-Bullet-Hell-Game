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
        [SerializeField] private float tapThreshold = 0.15f; // Max time for a single tap

        private InputAction moveAction;
        private float lastLeftTapTime;
        private float lastRightTapTime;
        private bool leftTapRegistered;
        private bool rightTapRegistered;

        public event Action leftTap;
        public event Action rightTap;

        public Vector2 Move => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        private void Awake()
        {
            if (playerInput == null)
                playerInput = GetComponent<PlayerInput>();

            moveAction = playerInput.actions["Move"];
        }

        private void OnEnable()
        {
            if (moveAction != null)
            {
                moveAction.started += OnMoveStarted;
                moveAction.canceled += OnMoveCanceled;
            }
        }

        private void OnDisable()
        {
            if (moveAction != null)
            {
                moveAction.started -= OnMoveStarted;
                moveAction.canceled -= OnMoveCanceled;
            }
        }

        private void OnMoveStarted(InputAction.CallbackContext ctx)
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
                    leftTapRegistered = true; // Prevent multiple triggers
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
                    rightTapRegistered = true; // Prevent multiple triggers
                }
                lastRightTapTime = currentTime;
            }
        }

        private void OnMoveCanceled(InputAction.CallbackContext ctx)
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