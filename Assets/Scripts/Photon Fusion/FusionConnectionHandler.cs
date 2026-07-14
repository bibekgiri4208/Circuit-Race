using Fusion;
using Fusion.Sockets;
using UnityEngine;

public class FusionConnectionHandler : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _runner;

    [Header("Grid Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Multiplayer Spawner")]
    [SerializeField] private NetworkPrefabRef carPrefab;

    private void Start()
    {
        _runner = GetComponent<NetworkRunner>();
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

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (player == runner.LocalPlayer)
        {
            Vector3 finalSpawnPos = new Vector3(0, 1, 0);
            Quaternion finalSpawnRot = Quaternion.identity;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int spawnIndex = (player.PlayerId - 1) % spawnPoints.Length;
                spawnIndex = Mathf.Clamp(spawnIndex, 0, spawnPoints.Length - 1);

                if (spawnPoints[spawnIndex] != null)
                {
                    finalSpawnPos = spawnPoints[spawnIndex].position;
                    finalSpawnRot = spawnPoints[spawnIndex].rotation;
                }
            }

            // 1. Spawn the network object
            NetworkObject spawnedCar = runner.Spawn(carPrefab, finalSpawnPos, finalSpawnRot, player);

            // 2. Force the local physics engine to accept the position instantly
            if (spawnedCar != null)
            {
                Rigidbody rb = spawnedCar.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = finalSpawnPos;
                    rb.rotation = finalSpawnRot;
                    rb.linearVelocity = Vector3.zero; // Prevent any pre-spawn momentum physics glitches
                }
            }
        }
    }

    // --- Required Fusion Interface Boilerplate ---
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData myInputData = new NetworkInputData();

        myInputData.steering = Input.GetAxis("Horizontal");
        myInputData.acceleration = Input.GetAxis("Vertical");
        myInputData.brake = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1.0f : 0.0f;
        myInputData.handbrake = Input.GetKey(KeyCode.Space);

        input.Set(myInputData);
    }
    public void OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ReadOnlySpan<byte> data) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
#pragma warning disable CS0618
    public unsafe void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
}