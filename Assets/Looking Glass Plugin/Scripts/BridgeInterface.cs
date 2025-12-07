using LookingGlass.Toolkit;
using LookingGlass.Toolkit.Bridge;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class BridgeInterface : MonoBehaviour
{
#if UNITY_STANDALONE
    private BridgeConnectionHTTP bridgeConnection;
	public MultiviewData data;
	public List<LookingGlass.Toolkit.Display> displays;

    // private IEnumerator Start()
    // {
    //     yield return StartCoroutine(ConnectAndListDisplays());
    // }

    public IEnumerator ConnectAndListDisplays()
    {
		displays = null;

        int restPort = BridgeConnectionHTTP.DefaultPort;
        int websocketPort = BridgeConnectionHTTP.DefaultWebSocketPort;

        bridgeConnection = new BridgeConnectionHTTP(
			ServiceLocator.Instance.GetSystem<LookingGlass.Toolkit.ILogger>(),
			ServiceLocator.Instance.GetSystem<IHttpSender>(),
			"localhost",
			restPort,
			websocketPort
		)
		{
			LoggingFlags = (BridgeLoggingFlags)data.bridgeLogging,
		};

        bool connected = false;
        for (int i = 0; i < 2; i++)
        {
            connected = bridgeConnection.Connect();
            if (connected) break;
        }
        if (!connected)
        {
            Debug.LogError("Could not connect to Looking Glass Bridge.");
            yield break;
        }

        if (!bridgeConnection.TryEnterOrchestration())
        {
            Debug.LogError("Failed to enter orchestration mode.");
            yield break;
        }

        // Wrap the async call in a coroutine-friendly way
        bool updated;
        var updateTask = bridgeConnection.UpdateDevicesAsync(BridgeLoggingFlags.None);
        while (!updateTask.IsCompleted)
            yield return null;
        updated = updateTask.Result;

        if (!updated)
        {
            Debug.LogError("Failed to update devices.");
            yield break;
        }

        displays = bridgeConnection.GetLKGDisplays();
		if (displays.Count == 0)
		{
			Debug.Log("Found no LKG Displays.");
			yield break;
		}
        foreach (var display in displays)
        {
            Debug.Log($"Found LKG Display: {display.calibration.GetDeviceType()}");
        }
    }
#endif
}
