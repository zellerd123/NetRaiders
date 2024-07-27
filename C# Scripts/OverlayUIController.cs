using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace NetRaiders
{
    public class OverlayUIController : MonoBehaviour
    {
        public static OverlayUIController Instance;
        public GameObject joinMatchPopup;
        public AudioListener globalAudioListener;
        public Text transmittedText;
        public Slider greenSlider;
        //public Text greenSliderText;
        public Text untransmittedText;
        public Slider blueSlider;
        //public Text blueSliderText;
        public GameObject wapPopup;
        public GameObject debugPopup;

        private void Awake()
        {
            if (Instance) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            NetraiderSimulation.MatchStarted += DisablePopup;
            NetraiderSimulation.AtWAP += AtWAP;
        }

        private void OnDestroy()
        {
            NetraiderSimulation.MatchStarted -= DisablePopup;
            NetraiderSimulation.AtWAP -= AtWAP;
        }

        private void DisablePopup()
        {
            joinMatchPopup.SetActive(false);
            debugPopup?.SetActive(true);
            globalAudioListener.enabled = false;
        }

        public void AtWAP(bool state)
        {
            wapPopup?.SetActive(state);
        }

        private void Update()
        {
            if (NetraiderConnect.Instance == null || NetraiderConnect.Instance.Simulation == null)
            {
                return;
            }
            int local_player_id = NetraiderConnect.Instance.Simulation.LocalPlayerID;
            NetraiderPlayer localPlayer = NetraiderConnect.Instance.Simulation.ActivePlayers[local_player_id];

            transmittedText.text = $"TRANSMITTED: {Mathf.Round(localPlayer.transmitted*100)/100f} TB";
            greenSlider.value = (float) localPlayer.transmitted/NetraiderConnect.Instance.Simulation.TargetTransmission;

            untransmittedText.text = $"UNTRANSMITTED: {Mathf.Round(localPlayer.untransmitted * 100) / 100f} TB";
            blueSlider.value = (float)localPlayer.untransmitted / NetraiderConnect.Instance.Simulation.TargetTransmission;
        }
    }
}
