using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Agojie.UI
{
    public class CompassUI : MonoBehaviour
    {
        [Header("Compass References")]
        [SerializeField] private RawImage compassStripImage;
        [SerializeField] private Transform playerTransform;

        [Header("Marker Settings")]
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private RectTransform markerContainer;

        private void Update()
        {
            if (playerTransform == null || compassStripImage == null) return;

            // UV rect offset according to player forward yaw
            float uvX = playerTransform.eulerAngles.y / 360f;
            compassStripImage.uvRect = new Rect(uvX, 0, 1, 1);
        }

        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }
    }
}
