using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NetRaiders
{
    public class Character : MonoBehaviour
    {
        public static Character Instance;
        public int NetraiderUserID => netraiderPlayer == null ? -1 : netraiderPlayer.user_id;

        public Image squareImage;
        public Text usernameText;

        double last_unix_update = 0;
        double diff_factor = 0;
        float time_accumulator_interpolation;
        public NetraiderPlayer secondMostRecentNetraiderPlayer;
        public NetraiderPlayer netraiderPlayer;

        Vector3 authoritativePositionRecent;

        void Start()
        {
            NetraiderConnect.Instance.Simulation.PlayerUpdated += ReceivedSnapshotUpdate;
        }

        double UnixTime()
        {
            DateTime currentTime = DateTime.UtcNow;
            long unixTimeMilliseconds = ((DateTimeOffset)currentTime).ToUnixTimeMilliseconds();
            double unixTimeWithMilliseconds = unixTimeMilliseconds / 1000.0;
            return unixTimeWithMilliseconds;
        }

        // Initalization stuff.
        public void Spawned(NetraiderPlayer netraiderPlayer)
        {
            if (netraiderPlayer.user_id != NetraiderSimulation.LLLocalPlayerID)
            {
                GetComponent<AudioListener>().enabled = false;
            }
            this.secondMostRecentNetraiderPlayer = this.netraiderPlayer;
            this.netraiderPlayer = netraiderPlayer;
            usernameText.text = netraiderPlayer.username;
            //squareImage.color = NumberToRGB(netraiderPlayer.user_id);
            authoritativePositionRecent = new Vector3(
                netraiderPlayer.x,
                netraiderPlayer.y,
                0
            );
            transform.position = authoritativePositionRecent;
        }

        // Snap back to authoritative state
        public void ReceivedSnapshotUpdate(NetraiderPlayer netraiderPlayer)
        {
            if (this.netraiderPlayer.user_id != netraiderPlayer.user_id) {
                /// if player invoked by this invent is not this character, ignore the update.
                return;
            }
            int transmission_diff = netraiderPlayer.untransmitted - this.netraiderPlayer.untransmitted;
            GetComponent<ScalingLogic>().IncreaseScale(
                netraiderPlayer.scale,
                transmission_diff,
                netraiderPlayer.user_id == NetraiderConnect.Instance.Simulation.LocalPlayerID
            );
            if (this.netraiderPlayer.user_id != NetraiderConnect.Instance.Simulation.LocalPlayerID && //Remote player
                this.netraiderPlayer.tick != netraiderPlayer.tick) //New tick update
            {
                // UNUSED AS OF NOW!
                double now = UnixTime();
                double sample_diff_factor = now - last_unix_update;
                double alpha = .3;
                diff_factor = (diff_factor * (1 - alpha)) + (sample_diff_factor * alpha);
                //Debug.Log(diff_factor);
                last_unix_update = now;
                time_accumulator_interpolation = 0f;
                this.secondMostRecentNetraiderPlayer = this.netraiderPlayer;
                this.netraiderPlayer = netraiderPlayer;
                authoritativePositionRecent = new Vector3(
                    netraiderPlayer.x,
                    netraiderPlayer.y,
                    0
                );
                transform.position = authoritativePositionRecent;
            }
            else if (this.netraiderPlayer.user_id == NetraiderConnect.Instance.Simulation.LocalPlayerID) { // Local Player
                this.netraiderPlayer = netraiderPlayer;
                authoritativePositionRecent = new Vector3(
                    netraiderPlayer.x,
                    netraiderPlayer.y,
                    0
                );
                transform.position = authoritativePositionRecent;
            }
        }

        float CustomLerp(float a, float b, float t)
        {
            if (t > 1) t = 1;
            if (t < 0) t = 0;
            return a + (b - a) * t;
        }

        Vector2 ClampVectorToWorldBounds(Vector2 vector2) {
            if (vector2.x > 10) vector2.x = 10;
            if (vector2.x < -10) vector2.x = -10;
            if (vector2.y > 10) vector2.y = 10;
            if (vector2.y < -10) vector2.y = -10;
            return vector2;
        }

        private const float subgridSize = 5f;

        Vector3 ApplyInputToVector(Vector2 vector, NetraiderInput input)
        {
            if (NetraiderConnect.Instance.Simulation == null) {
                return new();
            }
            Vector2 input_vector = new Vector2(input.x, input.y);
            float distance = Vector2.Distance(vector, input_vector);
            float current_speed = CustomLerp(0, 1, distance/1f);
            float speed_factor = current_speed * (NetraiderConnect.Instance.Simulation.TickInSeconds);
            Vector2.MoveTowards(vector, input_vector, speed_factor);
            return ClampVectorToWorldBounds(Vector2.MoveTowards(vector, input_vector, speed_factor));
        }

        Vector3 GetResimulatedPosition(
            SortedDictionary<int, NetraiderInput>.ValueCollection inflight_inputs,
            NetraiderInput? queued_input = null)
        {
            Vector3 position = authoritativePositionRecent;
            foreach (NetraiderInput input in inflight_inputs) {
                position = ApplyInputToVector(position, input);
            }
            if (queued_input.HasValue)
            {
                position = ApplyInputToVector(position, queued_input.Value);
            }
            return position;
        }

        // Client side prediction
        public void ResimulateTicks()
        {
            if (NetraiderConnect.Instance.Simulation == null) {
                return;
            }
            if (NetraiderConnect.Instance.clientSidePrediction && !NetraiderConnect.Instance.serverReconcile)
            {
                var nextInputPosition = ApplyInputToVector(authoritativePositionRecent, NetraiderConnect.Instance.LastNetRaiderInput);
                var position = Vector3.Lerp(
                    authoritativePositionRecent,
                    nextInputPosition,
                    NetraiderConnect.Instance.Subtick
                );
                transform.position = position;
            }
            else if (NetraiderConnect.Instance.clientSidePrediction && NetraiderConnect.Instance.serverReconcile) {
                Vector3 lastInputPosition = GetResimulatedPosition(
                    NetraiderConnect.Instance.Simulation.InflightInputs.Values
                );
                Vector3 currentInputPosition = GetResimulatedPosition(
                    NetraiderConnect.Instance.Simulation.InflightInputs.Values,
                    NetraiderConnect.Instance.NetRaiderInput //If we take into account our current input, WHERE will we be?
                );
                var position = Vector3.Lerp(
                    lastInputPosition,
                    currentInputPosition,
                    NetraiderConnect.Instance.Subtick
                );
                transform.position = position;
            }
            //Debug.Log($"Subtick: {NetraiderConnect.Instance.Subtick}, Last Input Position: {lastInputPosition}, Current Input Position: {currentInputPosition}, Lerped Position: {position}");
        }

        /// Remote entity interpolation
        void RemoteEntityInterpolation()
        {
            if (netraiderPlayer == null || secondMostRecentNetraiderPlayer == null) {
                return;
            }
            Vector3 secondMostRecentVector = new Vector3(
                secondMostRecentNetraiderPlayer.x,
                secondMostRecentNetraiderPlayer.y,
                0f
            );
            var interpolatedPosition = Vector3.Lerp(
                secondMostRecentVector,
                authoritativePositionRecent,
                (float) (time_accumulator_interpolation/diff_factor)
            );
            transform.position = interpolatedPosition;
            //Debug.Log($"INTERPOLATION FACTOR: {(float)(time_accumulator_interpolation / diff_factor)} - LAST STATE: {secondMostRecentVector}, CURRENT STATE: {authoritativePositionRecent}, INTERPOLATED: {interpolatedPosition}");
        }

        // Updates the characters position. If local player performs client side prediction. If remote player performs entity interpolation.
        void Update()
        {
            if (NetraiderUserID == NetraiderConnect.Instance.Simulation.LocalPlayerID)
            {
                // LOCAL PLAYER - PERFORM CLIENT SIDE PREDICTION (LOCAL PLAYER INTERPOLATION)
                ResimulateTicks();
            }
            else
            {
                // REMOTE PLAYER - PERFORM ENTITY INTERPOLATION (REMOTE INTERPOLATION - SLIGHTLY BEHIND)
                time_accumulator_interpolation += Time.deltaTime;
                if (NetraiderConnect.Instance.remoteInterp) {
                    RemoteEntityInterpolation();
                }
            }
        }

        public void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("PooledObject")) {
                //other.gameObject.SetActive(false);
            }
        }
    }
}
