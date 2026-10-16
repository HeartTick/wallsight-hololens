using UnityEngine;
using UnityEngine.XR;

namespace WallSight
{
    /// <summary>
    /// Moves the camera with the headset (HoloLens via OpenXR / Holographic Remoting).
    /// Put it on the Main Camera. Updates every frame and again just before rendering for low lag.
    /// </summary>
    public class HeadPoseFollower : MonoBehaviour
    {
        InputDevice head;

        void OnEnable() => Application.onBeforeRender += UpdatePose;

        void OnDisable() => Application.onBeforeRender -= UpdatePose;

        void Update() => UpdatePose();

        void UpdatePose()
        {
            if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!head.isValid) return;

            if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 p) ||
                head.TryGetFeatureValue(CommonUsages.devicePosition, out p))
                transform.localPosition = p;
            if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion r) ||
                head.TryGetFeatureValue(CommonUsages.deviceRotation, out r))
                transform.localRotation = r;
        }
    }
}
