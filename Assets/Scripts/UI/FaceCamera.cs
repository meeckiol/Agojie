using UnityEngine;

namespace Agojie.UI
{
    /// <summary>Garde un élément (jauge au-dessus d'un ennemi) tourné vers la caméra.</summary>
    public class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}
