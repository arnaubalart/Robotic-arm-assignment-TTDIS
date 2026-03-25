using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json; 

[System.Serializable]
public class QtmTransform
{
    public float positionX, positionY, positionZ;
    public float RotationX, RotationY, RotationZ, RotationW;
}

[System.Serializable]
public class QtmBodyMapping
{
    [Tooltip("The exact name of the 6D body coming from QTM")]
    public string bodyName;
    public GameObject prefab;
}

public class TrackingManager : MonoBehaviour
{
    [Header("Network Settings")]
    [SerializeField] private string _serverIP = "127.0.0.1";
    [SerializeField] private int _serverPort = 5005;

    [Header("QTM Bodies")]
    [SerializeField] private QtmBodyMapping[] _bodyMappings;

    [Header("Local Testing")]
    [Tooltip("Seconds to wait for QTM data before assuming no connection. Set to 0 to wait indefinitely.")]
    [SerializeField] private float _connectionTimeoutSeconds = 5f;

    /// <summary>True after the first QTM data packet has been received and applied.</summary>
    public static bool IsInitialized { get; private set; } = false;

    // Networking
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cts;
    private ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();
    
    // Tracking & Visuals
    private Dictionary<string, GameObject> _trackedBodies = new Dictionary<string, GameObject>();

    void Start()
    {
        // Use the existing prefab GameObjects directly as tracked bodies.
        foreach (var mapping in _bodyMappings)
        {
            if (!string.IsNullOrEmpty(mapping.bodyName) && mapping.prefab != null)
            {
                _trackedBodies[mapping.bodyName] = mapping.prefab;
            }
        }

        _cts = new CancellationTokenSource();
        _ = ConnectWebSocket();
    }

    private async Task ConnectWebSocket()
    {
        _webSocket = new ClientWebSocket();
        Uri serverUri = new Uri($"ws://{_serverIP}:{_serverPort}");

        try
        {
            Debug.Log($"Connecting to WebSocket at {serverUri}...");
            await _webSocket.ConnectAsync(serverUri, _cts.Token);
            Debug.Log("WebSocket connected successfully!");

            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket Connection Error: {e.Message}");
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];

        while (_webSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            var stringBuilder = new StringBuilder();
            WebSocketReceiveResult result;

            try
            {
                do
                {
                    result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        stringBuilder.Append(chunk);
                    }
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                    _messageQueue.Enqueue(stringBuilder.ToString());
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.Log("WebSocket closed by server.");
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"WebSocket Receive Error: {e.Message}");
                break;
            }
        }
    }

    void Update()
    {
        // Fall back gracefully when QTM is not available (e.g. local testing).
        if (!IsInitialized && _connectionTimeoutSeconds > 0f && Time.time >= _connectionTimeoutSeconds)
        {
            IsInitialized = true;
            Debug.LogWarning("[TrackingManager] No QTM data received within timeout — proceeding without tracking (local mode).");
        }

        while (_messageQueue.TryDequeue(out string jsonString))
            ProcessTrackingData(jsonString);
    }

    private void ProcessTrackingData(string jsonString)
    {
        Dictionary<string, QtmTransform> parsedData = null;
        try
        {
            parsedData = JsonConvert.DeserializeObject<Dictionary<string, QtmTransform>>(jsonString);
        }
        catch (JsonException ex)
        {
            Debug.LogError($"JSON Parsing error: {ex.Message}");
            return;
        }

        if (parsedData == null) return;

        IsInitialized = true;

        foreach (var kvp in parsedData)
        {
            string bodyName = kvp.Key;
            QtmTransform transformData = kvp.Value;

            Vector3 targetPosition = new Vector3(
                transformData.positionX, 
                transformData.positionY, 
                transformData.positionZ
            ) / 1000f; // Convert mm to meters
            
            Quaternion targetRotation = new Quaternion(
                transformData.RotationX, 
                transformData.RotationY, 
                transformData.RotationZ, 
                transformData.RotationW
            );

            if (!_trackedBodies.ContainsKey(bodyName))
                continue;

            GameObject trackedObj = _trackedBodies[bodyName];
            trackedObj.transform.localPosition = targetPosition;
            trackedObj.transform.localRotation = targetRotation;
        }
    }

    private void OnDestroy() 
    {
        IsInitialized = false;
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None).Wait();
            _webSocket.Dispose();
        }
    }
}