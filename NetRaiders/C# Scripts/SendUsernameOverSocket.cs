using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json;


namespace NetRaiders {
    public class SendUsernameOverSocket : MonoBehaviour
    {
        public static SendUsernameOverSocket Instance;
        public InputField inputField;
        public Dictionary<string, string> dictionary = new();

        private void Awake()
        {
            if (Instance) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }


        public string CheckInputField()
        {
            dictionary["username"] = inputField.text;
            return JsonConvert.SerializeObject(dictionary);
        }
    }
}
