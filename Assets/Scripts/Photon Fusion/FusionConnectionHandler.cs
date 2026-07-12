using Fusion;
using Fusion.Sockets;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class FusionConnectionHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;

    [Header("Multiplayer Spawner")]
    [SerializeField] private NetworkPrefabRef carPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 1, 0);

    private void Start()
    {
        _runner = GetComponent<NetworkRunner>();

        // Correctly register the callbacks before starting the game
        _runner.AddCallbacks(this);

        StartGame();
    }

    async void StartGame()
    {
        _runner.ProvideInput = true;

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = "RacingRoom",
            SceneManager = GetComponent<NetworkSceneManagerDefault>()
        });
    }

    // This will trigger reliably now
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            Debug.Log("Local player joined! Spawning car...");
            runner.Spawn(carPrefab, spawnPosition, Quaternion.identity, player);
        }
    }

    // --- EXACT INTERFACE METHODS CONFORMING TO YOUR VERSION ---
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    // Remaining required boilerplate
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public unsafe void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData inputData = new NetworkInputData();

        // Keyboard controls
        if (Input.GetKey(KeyCode.W)) inputData.acceleration = 1f;
        if (Input.GetKey(KeyCode.S)) inputData.brake = 1f;
        if (Input.GetKey(KeyCode.A)) inputData.steering = -1f;
        if (Input.GetKey(KeyCode.D)) inputData.steering = 1f;
        inputData.handbrake = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E);

        // Controller inputs override if detected
        if (Gamepad.current != null)
        {
            float rt = Gamepad.current.rightTrigger.ReadValue();
            float lt = Gamepad.current.leftTrigger.ReadValue();
            float stickX = Gamepad.current.leftStick.x.ReadValue();

            if (rt > 0.08f) inputData.acceleration = Mathf.Pow(rt, 0.65f);
            if (lt > 0.08f) inputData.brake = Mathf.Pow(lt, 0.65f);
            if (Mathf.Abs(stickX) > 0.12f) inputData.steering = Mathf.Sign(stickX) * Mathf.Pow(Mathf.Abs(stickX), 0.75f);
            if (Gamepad.current.buttonSouth.isPressed || Gamepad.current.rightShoulder.isPressed) inputData.handbrake = true;
        }

        input.Set(inputData);
    }

}

public struct NetworkInputData : INetworkInput
{
    public float steering;
    public float acceleration;
    public float brake;
    public bool handbrake;
}