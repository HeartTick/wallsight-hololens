using TMPro;
using UnityEngine;

namespace WallSight
{
    /// <summary>
    /// Floating 3D text above the tabletop model, so the headset shows what the screen overlay shows:
    /// title, the live route status, and a short legend. Screen-space UI is not visible in the HoloLens.
    /// Put it on the model root (Hallway_Diorama); it follows the model and turns to face the viewer.
    /// </summary>
    public class HeadsetInfoPanel : MonoBehaviour
    {
        public RoutePlanner planner;
        public Transform head;
        [Tooltip("Height above the model root, in metres")]
        public float heightMetres = 0.28f;
        [Tooltip("Base letter size; 0.2 gives about 2 cm high letters")]
        public float fontSize = 0.2f;
        public string title = "WallSight";
        public string subtitle = "Robot scan -> safe route for people";
        public string legend = "<color=#19FF99>route</color>   <color=#FFD91A>target</color>   <color=#33FF55>start</color>   "
                               + "<color=#FF2619>simulated hazard</color>   <color=#338CFF>simulated robot</color>\n"
                               + "Right pinch: set / drag target  -  Left pinch: add / remove hazard";

        Transform panel;
        TextMeshPro text;

        void Start()
        {
            panel = new GameObject("HeadsetInfoPanel").transform;
            text = panel.gameObject.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = Color.white;
        }

        void LateUpdate()
        {
            if (panel == null) return;
            panel.position = transform.position + Vector3.up * heightMetres;
            var h = head != null ? head : Camera.main != null ? Camera.main.transform : null;
            if (h != null)
            {
                var away = panel.position - h.position;
                away.y = 0f;
                if (away.sqrMagnitude > 1e-4f) panel.rotation = Quaternion.LookRotation(away, Vector3.up);
            }
            string status = planner != null && planner.statusLabel != null ? planner.statusLabel.text : "";
            text.text = $"<size=160%><b>{title}</b></size>\n<size=80%>{subtitle}</size>\n\n" +
                        $"<color=#19FF99>{status}</color>\n\n<size=60%>{legend}</size>";
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel.gameObject);
        }
    }
}
