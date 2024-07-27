using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NetRaiders
{
    public class NetworkObjectPool : MonoBehaviour
    {
        public static NetworkObjectPool SharedInstance;
        public List<GameObject> pooledObjects;
        public GameObject objectToPool;
        public int amountToPool;
        public RectTransform parentObject;
        public Text totalObjectsText;
        public Text collectedObjectsText;

        private int totalActiveObjects;
        private int totalCollected;
        private Dictionary<int, GameObject> activeObjects = new Dictionary<int, GameObject>();

        void Awake()
        {
            SharedInstance = this;
        }

        void Start()
        {
            pooledObjects = new List<GameObject>();
            for (int i = 0; i < amountToPool; i++)
            {
                GameObject tmp = Instantiate(objectToPool, parentObject);
                tmp.SetActive(false);
                pooledObjects.Add(tmp);
            }
        }

        public void SpawnFromPool(BitPickup pickup)
        {
            GameObject obj = pooledObjects.Find(p => !p.activeSelf);
            if (obj != null)
            {
                Vector2 vector = new Vector2(pickup.x, pickup.y);
                RectTransform rectTransform = obj.GetComponent<RectTransform>();
                rectTransform.anchoredPosition = vector;
                obj.SetActive(true);
                activeObjects[pickup.id] = obj;
                totalActiveObjects++;
            }
        }

        public void DespawnFromPool(int id)
        {
            if (activeObjects.TryGetValue(id, out GameObject obj))
            {
                obj.SetActive(false);
                activeObjects.Remove(id);
                totalActiveObjects--;
            }
        }

        public void DespawnAll() {
            foreach (int go_id in new List<int>(activeObjects.Keys)) {
                activeObjects[go_id].SetActive(false);
                activeObjects.Remove(go_id);
                totalActiveObjects--;
            }
        }
    }
}
