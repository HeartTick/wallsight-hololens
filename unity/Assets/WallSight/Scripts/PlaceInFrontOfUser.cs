using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace WallSight
{
    /// <summary>
    /// Places this object (the tabletop model) in front of the wearer, where they are looking,
    /// a little below eye height and turned to face them. Runs once shortly after start
    /// (once head tracking is live) and again on the R key.
    /// </summary>
    public class PlaceInFrontOfUser : MonoBehaviour
    {
        public Transform head;
        public float distanceMetres = 1.0f;
        public float belowEyesMetres = 0.35f;
        public float startDelaySeconds = 2f;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(startDelaySeconds);
            Place();
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Place();
#else
            if (Input.GetKeyDown(KeyCode.R)) Place();
#endif
        }

        public void Place()
        {
            var h = head != null ? head : Camera.main != null ? Camera.main.transform : null;
            if (h == null) return;
            var forward = Vector3.ProjectOnPlane(h.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(h.up, Vector3.up); // looking straight down/up
            forward.Normalize();
            transform.position = h.position + forward * distanceMetres + Vector3.down * belowEyesMetres;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
