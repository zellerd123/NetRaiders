using System;
using System.Collections;
using System.Collections.Generic;
using NativeWebSocket;
using UnityEngine;

namespace NetRaiders
{
    public class NetraiderConnect : MonoBehaviour
    {
        public static NetraiderConnect Instance;
        // Spawned Character GameObjects
        public Dictionary<int, Character> CharactersMapping = new();

        // Access Simulation State
        public NetraiderSimulation Simulation => netraiderWebsocket != null ? netraiderWebsocket.netraiderSimulation : null;

        /// Character Object to spawn when player spawns
        [SerializeField] GameObject characterPrefab;
        [SerializeField] Transform spawnPoint;
        [SerializeField] GameObject pickupPrefab;
        [SerializeField] Transform pickupParent;
        public GameObject globalCinemachine;

        [Header("End Screens")]
        private GameObject currentScreen = null;
        public Transform spawnParent;
        public GameObject winScreen;
        public GameObject loseScreen;

        public NetraiderInput LastNetRaiderInput;
        public NetraiderInput NetRaiderInput;
        private NetraiderWebsocket netraiderWebsocket;


        public float Subtick => time_accumulator / Simulation.TickInSeconds;
        float time_accumulator;

        private void Awake() {
            if (Instance) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void PlayNetraiders() {
            if (currentScreen != null) {
                Destroy(currentScreen);
                globalCinemachine?.SetActive(false);
            }
            ConnectToSocket();
            StartCoroutine(SendFirstUpdate());
        }

        public bool clientSidePrediction = true;
        public bool remoteInterp = true;
        public bool serverReconcile = true;

        public void ToggleRemoteInterp()
        {
            remoteInterp = !remoteInterp;
        }

        public void ToggleClientSide() {
            clientSidePrediction = !clientSidePrediction;
        }

        public void ToggleServerReconcile() {
            serverReconcile = !serverReconcile;
        }

        private void OnDestroy()
        {
            if (netraiderWebsocket != null)
            {
                netraiderWebsocket.Disconnect();
            }
        }

        // We do this here and not in netraider simulation b/c we need coroutines.
        IEnumerator SendFirstUpdate() {
            while (Simulation == null)
            {
                yield return null; // Wait for the simulation to begin.. (to here from server start signal)
            }
            Simulation.UpdateClientSimulation(ConsumeInput());
        }

        private void ConnectToSocket() {
            netraiderWebsocket = new();
            //netraiderWebsocket.Connect("wss://biscuitbuddy.io/api/netraiderConnect");
            netraiderWebsocket.Connect("wss://spock.cs.colgate.edu/api/netraiderConnect");
        }

        private void DisconnectSocket()
        {
            netraiderWebsocket.Disconnect();
        }

        public void SpawnBitPickup(BitPickup bitPickup)
        {
            Transform go = Instantiate(pickupPrefab, pickupParent).GetComponent<Transform>();
            go.position = new Vector2(bitPickup.x, bitPickup.y);
        }

        public void SpawnPlayerPrefab(NetraiderPlayer player)
        {
            Character character = Instantiate(characterPrefab, spawnPoint).GetComponent<Character>();
            character.Spawned(player);
            CharactersMapping[player.user_id] = character;
        }

        public void DespawnPlayer(int user_id)
        {
            if (CharactersMapping.ContainsKey(user_id))
            {
                Destroy(CharactersMapping[user_id].gameObject);
                CharactersMapping.Remove(user_id);
            }
            else {
                Debug.LogError("Trying to despawn player who does not exist.");
            }
        }

        public void TryEndMatch(int game_over_winner) {
            if (game_over_winner < 0)
            {
                return;
            }
            if (currentScreen) {
                return;
            }
            if (game_over_winner == Simulation.LocalPlayerID)
            {
                currentScreen = Instantiate(winScreen, spawnParent);

            }
            else {
                currentScreen = Instantiate(loseScreen, spawnParent);
            }
            currentScreen.transform.localScale = Vector3.one;
            DisconnectSocket();
            foreach (Character player in CharactersMapping.Values)
            {
                Destroy(player.gameObject);
            }
            CharactersMapping.Clear();
            OverlayUIController.Instance.globalAudioListener.enabled = true;
            OverlayUIController.Instance.debugPopup?.SetActive(false);
            globalCinemachine?.SetActive(true);
            NetworkObjectPool.SharedInstance.DespawnAll();
        }

        private void Update() {
#if !UNITY_WEBGL || UNITY_EDITOR
            if(netraiderWebsocket != null && netraiderWebsocket.webSocket != null && netraiderWebsocket.webSocket.State != WebSocketState.Closed && netraiderWebsocket.webSocket.State != WebSocketState.Closing)
            {
                netraiderWebsocket.webSocket.DispatchMessageQueue();
            }
#endif
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Camera.main.nearClipPlane));
            NetRaiderInput.x = mousePosition.x;
            NetRaiderInput.y = mousePosition.y;
            if (Simulation != null)
            {
                if (time_accumulator >= Simulation.TickInSeconds)
                {
                    time_accumulator = 0f;
                    Simulation.UpdateClientSimulation(ConsumeInput());
                }
                time_accumulator += Time.deltaTime;
            }
        }
        

        public NetraiderInput ConsumeInput() {
            LastNetRaiderInput = NetRaiderInput;
            //NetRaiderInput = new(); //Resets input
            return LastNetRaiderInput;
        }
    }
}
