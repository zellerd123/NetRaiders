using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;
using Newtonsoft.Json;

namespace NetRaiders
{
    public class NetraiderWebsocket
    {
        public NetraiderSimulation netraiderSimulation;
        public WebSocket webSocket;
        public bool socketAccepted;
        public WebSocketCloseCode WebSocketCloseCode;

        public async void Connect(string url)
        { 
            if (webSocket != null && webSocket.State != WebSocketState.Closed)
            {
                return;
            }
            webSocket = new WebSocket(url);
            webSocket.OnOpen += async () =>
            {
                Debug.Log("Socket Connected");
                await webSocket.SendText(SendUsernameOverSocket.Instance.CheckInputField());
            };
            webSocket.OnMessage += async (bytes) =>
            {
                string data = System.Text.Encoding.UTF8.GetString(bytes);
                if (data == "ping") {
                    await webSocket.SendText("pong");
                    return;
                }
                NetraiderSnapshot netraiderSnapshot = JsonConvert.DeserializeObject<NetraiderSnapshot>(data);
                if (netraiderSimulation == null) {
                    netraiderSimulation = new NetraiderSimulation(netraiderSnapshot, this);
                    return;
                }
                netraiderSimulation.ReceiveSimulationSnapshot(netraiderSnapshot);
            };
            // Handle Errors
            webSocket.OnError += (e) => Debug.LogError($"WebSocket Error: {e}");
            // Close socket when ready to close.
            webSocket.OnClose += (e) => {
                Debug.Log(e);
                WebSocketCloseCode = e;
                socketAccepted = false;
            };
            await webSocket.Connect();
        }

        public async void Disconnect()
        {
            if (webSocket != null && webSocket.State != WebSocketState.Closed)
            {
                await webSocket.Close();
            }
        }

        // THIS GETS CALLED EVERY TICK.
        public async void SendInputs(NetraiderInput consumeInput)
        {
            await webSocket.SendText(JsonConvert.SerializeObject(consumeInput));
        }

    }
}
