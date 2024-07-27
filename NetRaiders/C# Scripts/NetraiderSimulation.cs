using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetRaiders
{
    public class NetraiderSimulation
    {
        /// ---- THESE VALUES SHOULD BE CONSTANT AMONGST ALL CLIENTS

        // Active Players in the match.
        public Dictionary<int, NetraiderPlayer> ActivePlayers = new();

        /// How many ticks per second is simulation running at?
        public int TickRate { get; private set; }

        /// How many seconds is a tick?
        public float TickInSeconds => 1.0f / TickRate;

        /// ---- THESE VALUES ARE UNIQUE TO THIS CLIENT ----

        // Represents our websocket connection
        private NetraiderWebsocket NetraiderWebsocket { get; set; }

        // Local Players User ID.
        public int LocalPlayerID;

        public static int LLLocalPlayerID;

        /// Local Tick. Iterated on client, corrected by server on each snapshot update.
        public int LocalTick { get; private set; }

        /// What is the our RTT, in terms of ticks?
        public float TickRTT { get; private set; }
        
        public int TargetTransmission { get; private set; }

        /// What inputs are we waiting to receive ACK for from server. 
        public SortedDictionary<int, NetraiderInput> InflightInputs = new();

        /// ----- OTHER STUFF ------

        /// General utility lists so that we don't continously garbage collect lists (managed memory)
        private List<int> utility_list = new List<int>();
        private List<int> user_ids_connected = new List<int>();

        /// Event invoked on all character players everytime we receive a snapshot update.
        public delegate void OnSnapshotReceived(NetraiderPlayer netraiderPlayer);
        public event OnSnapshotReceived PlayerUpdated;

        public delegate void OnMatchStarted();
        public static event OnMatchStarted MatchStarted;
        public delegate void OnEnteredWAP(bool entered);
        public static event OnEnteredWAP AtWAP;

        /// CONSTRUCTOR: Simulation Object gets constructed the moment we receive our first snapshot.
        public NetraiderSimulation(NetraiderSnapshot inital_simulation_state, NetraiderWebsocket websocket_connection) {
            // Simulation can only start once we receive our inital state.
            LocalPlayerID = inital_simulation_state.local_player_id;
            LLLocalPlayerID = LocalPlayerID;
            TickRate = inital_simulation_state.tick_rate;
            NetraiderWebsocket = websocket_connection;
            ReceiveSimulationSnapshot(inital_simulation_state);
            MatchStarted?.Invoke();
        }
        bool wasAtWap = false;
        /// Function called as soon as we receive a new authoritative snapshot from the server.
        public void ReceiveSimulationSnapshot(NetraiderSnapshot netraiderSnapshot)
        {
            // Spawn new players + broadcast updates
            user_ids_connected.Clear();
            foreach (NetraiderPlayer player in netraiderSnapshot.player_deltas) {
                // Spawn any players who just joined.
                if (!ActivePlayers.ContainsKey(player.user_id)) {
                    NetraiderConnect.Instance.SpawnPlayerPrefab(player);
                }
                else {
                    PlayerUpdated?.Invoke(player);
                }
                user_ids_connected.Add(player.user_id);
                ActivePlayers[player.user_id] = player;
            }
            // Spawns any new pickups
            foreach (BitPickup bitPickup in netraiderSnapshot.spawn_pickups) {
                NetworkObjectPool.SharedInstance.SpawnFromPool(bitPickup);
            }
            // Despawns any pickups that were consumed
            foreach (int despawnPickup in netraiderSnapshot.despawn_pickups) {
                NetworkObjectPool.SharedInstance.DespawnFromPool(despawnPickup);
            }
            // Despawns any players that left.
            foreach (int despawnPlayer in netraiderSnapshot.despawn_players)
            {
                NetraiderConnect.Instance.DespawnPlayer(despawnPlayer);
                ActivePlayers.Remove(despawnPlayer);
            }
            // Update new Tick RTT.
            TickRTT = ActivePlayers[LocalPlayerID].tick_rtt;
            TargetTransmission = netraiderSnapshot.target_transmission;

            /// ---- IMPORTANT: TICK SYNC ----
            /// the players local simulation is always constantly resynced with servers.
            /// The server tick + half of the RTT is our best guess of the tick that the server is on
            /// at this exact moment. We then reset our tick timer to

            LocalTick = netraiderSnapshot.server_tick + (int)(TickRTT / 2.0f);

            /// --- CLIENT SIDE PREDICTION ---
            // Clear all old ticks, as we now have servers most recent state.
            ClearOldTicks(ActivePlayers[LocalPlayerID].tick);

            VirtualCamInstance.Instance.cinemachineVirtualCamera.Follow = NetraiderConnect.Instance.CharactersMapping[LocalPlayerID].transform;
            VirtualCamInstance.Instance.cinemachineVirtualCamera.LookAt = NetraiderConnect.Instance.CharactersMapping[LocalPlayerID].transform;

            if (wasAtWap && !netraiderSnapshot.at_wap)
            {
                AtWAP?.Invoke(false);
            }
            else if (!wasAtWap && netraiderSnapshot.at_wap)
            {
                AtWAP?.Invoke(true);
            }

            NetraiderConnect.Instance.TryEndMatch(netraiderSnapshot.game_over_winner);
        }

        // Clears all ticks older than the current.
        void ClearOldTicks(float reference_tick) {
            utility_list.Clear();
            foreach (int tick in InflightInputs.Keys)
            {
                if (tick <= reference_tick)
                {
                    utility_list.Add(tick);
                }
            }
            foreach (int tick in utility_list)
            {
                InflightInputs.Remove(tick);
            }
        }

        /// SENDS UPDATES TO SERVER
        /// SOLUTION --> BETTER SYNC
        public void UpdateClientSimulation(NetraiderInput netraiderInput) {
            int highest_count_tick = 0;
            this.LocalTick += 1;
            foreach (int tick in InflightInputs.Keys) {
                if (this.LocalTick <= tick) {
                    if (highest_count_tick < tick) {
                        highest_count_tick = tick;
                    }
                    //return;
                }
            }
            if (highest_count_tick != 0)
            {
                Debug.LogWarning("Local Tick value has already been sent to server - sending next tick.");
                this.LocalTick = highest_count_tick + 1;
            }
            // Set our expected tick as the local simulations stick.
            netraiderInput.expected_tick = LocalTick;
            // Send inputs to the server;
            NetraiderWebsocket.SendInputs(netraiderInput);
            InflightInputs[(int)this.LocalTick] = netraiderInput;
        }

        public bool EqualVectors(Vector3 first, Vector3 second, float tolerance = 0.5f)
        {
            return Mathf.Abs(first.x - second.x) < tolerance &&
                   Mathf.Abs(first.y - second.y) < tolerance &&
                   Mathf.Abs(first.z - second.z) < tolerance;
        }
    }
}

