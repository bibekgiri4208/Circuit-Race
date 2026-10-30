using UnityEngine;
using UnityEngine.InputSystem;

namespace CircuitRace.Environment
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class MeadowFreeFlyCamera : MonoBehaviour
    {
        [Min(0.1f)] public float moveSpeed = 12f;
        [Min(1f)] public float fastMultiplier = 3f;
        [Min(0.01f)] public float mouseSensitivity = 0.12f;
        [Min(1f)] public float gamepadLookSpeed = 100f;
        private float yaw;
        private float pitch;

        private void OnEnable() => SyncLookToTransform();

        public void SyncLookToTransform()
        {
            yaw = transform.eulerAngles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0, transform.eulerAngles.x), -89, 89);
        }

        private void Update()
        {
            if (!Application.isFocused || Time.timeScale <= 0f) return;
            var movement = Vector3.zero;
            var look = Vector2.zero;
            bool fast = false;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1 : 0) -
                             (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1 : 0);
                movement.z = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1 : 0) -
                             (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1 : 0);
                movement.y = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                fast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
            var mouse = Mouse.current;
            if (mouse != null && (Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed))
                look = mouse.delta.ReadValue() * mouseSensitivity;
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                movement.x += stick.x;
                movement.z += stick.y;
                movement.y += (pad.rightShoulder.isPressed ? 1 : 0) - (pad.leftShoulder.isPressed ? 1 : 0);
                look += pad.rightStick.ReadValue() * gamepadLookSpeed * Time.unscaledDeltaTime;
                fast |= pad.leftStickButton.isPressed;
            }
            ApplyInput(movement, look, Time.unscaledDeltaTime, fast);
        }

        public void ApplyInput(Vector3 movement, Vector2 lookDelta, float deltaTime, bool fast)
        {
            yaw += lookDelta.x;
            pitch = Mathf.Clamp(pitch - lookDelta.y, -89, 89);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 local = Vector3.ClampMagnitude(movement, 1);
            Vector3 world = transform.right * local.x + Vector3.up * local.y + transform.forward * local.z;
            transform.position += Vector3.ClampMagnitude(world, 1) * moveSpeed * (fast ? fastMultiplier : 1) * Mathf.Max(0, deltaTime);
        }
    }
}
