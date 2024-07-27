using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

namespace NetRaiders {
    public class VirtualCamInstance : MonoBehaviour
    {
        public static VirtualCamInstance Instance;
        public CinemachineVirtualCamera cinemachineVirtualCamera;
        public CinemachineRecomposer cinemachineRecomposer;

        void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            cinemachineVirtualCamera = GetComponent<CinemachineVirtualCamera>();
            cinemachineRecomposer = GetComponent<CinemachineRecomposer>();
        }
    }
}
