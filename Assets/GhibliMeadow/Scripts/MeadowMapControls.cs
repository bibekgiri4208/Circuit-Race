using UnityEngine;
using UnityEngine.InputSystem;

namespace CircuitRace.Environment
{
    /// <summary>Optional Testing-scene harness; the environment does not depend on this player setup.</summary>
    [DefaultExecutionOrder(-50), DisallowMultipleComponent]
    public sealed class MeadowMapControls : MonoBehaviour
    {
        public Camera playerCamera;
        public ChaseCamera chaseCamera;
        public MeadowFreeFlyCamera freeFlyCamera;
        public CarSpawner carSpawner;
        public CarController playerCar;
        public bool startInFreeFly;

        public bool IsFreeFly { get; private set; }
        private bool previousCarControls;
        private bool previousChaseEnabled;
        private bool started;

        private void Start()
        {
            if (playerCamera == null || chaseCamera == null || freeFlyCamera == null)
            {
                Debug.LogError("Meadow player camera bindings are missing. Run the Testing player setup.", this);
                enabled = false;
                return;
            }
            playerCamera.enabled = true;
            freeFlyCamera.enabled = false;
            if (playerCar == null && carSpawner != null && carSpawner.TrySpawnCar())
                playerCar = carSpawner.SpawnedCar.GetComponent<CarController>();
            if (playerCar != null)
            {
                chaseCamera.target = playerCar.transform;
                chaseCamera.targetRb = playerCar.GetComponent<Rigidbody>();
                chaseCamera.holdPosition = false;
                chaseCamera.enabled = true;
                chaseCamera.SnapToTarget();
            }
            started = true;
            SetFreeFly(startInFreeFly || playerCar == null);
        }

        private void Update()
        {
            if (!started || !Application.isFocused || Time.timeScale <= 0f) return;
            if (!IsFreeFly && playerCar == null) SetFreeFly(true);
            bool toggle = Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
            toggle |= Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame;
            if (toggle) SetFreeFly(!IsFreeFly);
        }

        public void SetFreeFly(bool value)
        {
            if (freeFlyCamera == null || chaseCamera == null) return;
            // A missing vehicle should never strand the player at another fixed viewpoint.
            if (!value && playerCar == null) value = true;
            if (value == IsFreeFly) return;
            if (value)
            {
                previousChaseEnabled = chaseCamera.enabled;
                previousCarControls = playerCar != null && (playerCar.CanDrive || Time.timeScale <= 0f);
                if (playerCar != null) playerCar.SetControlsEnabled(false);
                chaseCamera.enabled = false;
                freeFlyCamera.enabled = true;
                freeFlyCamera.SyncLookToTransform();
            }
            else
            {
                freeFlyCamera.enabled = false;
                if (playerCar != null) playerCar.SetControlsEnabled(previousCarControls);
                chaseCamera.enabled = previousChaseEnabled;
                if (chaseCamera.enabled) chaseCamera.SnapToTarget();
            }
            IsFreeFly = value;
        }

        private void OnDisable()
        {
            if (!IsFreeFly) return;
            if (freeFlyCamera != null) freeFlyCamera.enabled = false;
            if (playerCar != null) playerCar.SetControlsEnabled(previousCarControls);
            if (chaseCamera != null) chaseCamera.enabled = previousChaseEnabled;
            IsFreeFly = false;
        }
    }
}
