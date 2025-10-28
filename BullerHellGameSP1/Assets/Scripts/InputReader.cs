using UnityEngine;
using UnityEngine.InputSystem;

namespace BulletHell
{
    [RequireComponent(typeof(PlayerInput))]
    public class InputReader : MonoBehaviour
    {
        private PlayerInput playerInput;
        private InputAction moveAction;

        public Vector2 Move => moveAction.ReadValue<Vector2>();

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();

            // Access the action from the default action map
            moveAction = playerInput.actions["Move"];
        }

        private void OnEnable()
        {
            moveAction.Enable();
        }

        private void OnDisable()
        {
            moveAction.Disable();
        }
    }
}
