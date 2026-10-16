using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace WallSight
{
    /// <summary>
    /// Streams the planned goal, the path and the simulated robot's motion commands as JSON lines over UDP
    /// (default 127.0.0.1:5005) for a robot-side listener (scripts/robot_bridge_listener.py).
    /// Coordinates are in the scan map frame, converted to ROS REP-103 axes:
    /// x forward = Unity z, y left = -Unity x, z up = Unity y; yaw counter-clockwise from +x.
    /// </summary>
    public class RobotCommandPublisher : MonoBehaviour
    {
        public RoutePlanner planner;
        public RouteFollower follower;
        public string host = "127.0.0.1";
        public int port = 5005;
        [Tooltip("Motion commands per second")]
        public float commandRateHz = 5f;
        [Tooltip("While a target is dragged, send the goal at most this often")]
        public float goalMinIntervalSeconds = 0.25f;

        UdpClient udp;
        float nextCommand, lastGoalSent = -10f;
        bool goalPending;
        Vector3 lastPos;
        float lastYawRos;
        bool havePose;

        void Start()
        {
            udp = new UdpClient();
            if (planner != null) planner.RouteChanged += OnRouteChanged;
            Send("{\"type\":\"hello\",\"frame\":\"scan_map\",\"source\":\"WallSight HoloLens 2\"}");
        }

        void OnDestroy()
        {
            if (planner != null) planner.RouteChanged -= OnRouteChanged;
            udp?.Close();
        }

        void OnRouteChanged(IReadOnlyList<Vector3> route) => goalPending = true;

        void Update()
        {
            if (goalPending && Time.time - lastGoalSent >= goalMinIntervalSeconds) SendGoal();
            if (Time.time >= nextCommand)
            {
                nextCommand = Time.time + 1f / Mathf.Max(0.5f, commandRateHz);
                SendCommand();
            }
        }

        void SendGoal()
        {
            goalPending = false;
            lastGoalSent = Time.time;
            var route = planner.RouteLocal;
            if (route.Count < 2)
            {
                Send($"{{\"type\":\"no_path\",\"stamp\":{Stamp()},\"frame\":\"scan_map\"}}");
                return;
            }
            var goal = ToRos(route[route.Count - 1]);
            var sb = new StringBuilder();
            sb.Append($"{{\"type\":\"goal\",\"stamp\":{Stamp()},\"frame\":\"scan_map\",");
            sb.Append($"\"x\":{F(goal.x)},\"y\":{F(goal.y)},\"z\":0.0,\"length_m\":{F(planner.RouteLengthMetres)},\"path\":[");
            for (int i = 0; i < route.Count; i++)
            {
                var p = ToRos(route[i]);
                sb.Append(i > 0 ? "," : "").Append($"[{F(p.x)},{F(p.y)}]");
            }
            sb.Append("]}");
            Send(sb.ToString());
        }

        void SendCommand()
        {
            var robot = follower != null ? follower.Robot : null;
            if (robot == null || !robot.gameObject.activeInHierarchy) return;
            var pos = robot.localPosition;
            float yawRos = -robot.localEulerAngles.y * Mathf.Deg2Rad;
            float dt = 1f / Mathf.Max(0.5f, commandRateHz);
            float linear = 0f, angular = 0f;
            if (havePose)
            {
                linear = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(lastPos.x, 0f, lastPos.z)) / dt;
                angular = Mathf.DeltaAngle(lastYawRos * Mathf.Rad2Deg, yawRos * Mathf.Rad2Deg) * Mathf.Deg2Rad / dt;
                if (Mathf.Abs(linear) > 3f) { linear = 0f; angular = 0f; } // jump back to the start, not motion
            }
            lastPos = pos;
            lastYawRos = yawRos;
            havePose = true;

            string action = linear < 0.02f && Mathf.Abs(angular) < 0.05f ? "STOP"
                : angular > 0.35f ? "TURN_LEFT"
                : angular < -0.35f ? "TURN_RIGHT"
                : "FORWARD";
            var p = ToRos(pos);
            Send($"{{\"type\":\"cmd\",\"stamp\":{Stamp()},\"frame\":\"scan_map\",\"x\":{F(p.x)},\"y\":{F(p.y)}," +
                 $"\"yaw\":{F(Mathf.Atan2(Mathf.Sin(yawRos), Mathf.Cos(yawRos)))},\"linear\":{F(linear)},\"angular\":{F(angular)},\"action\":\"{action}\"}}");
        }

        static Vector3 ToRos(Vector3 unityLocal) => new Vector3(unityLocal.z, -unityLocal.x, unityLocal.y);

        static string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

        static string Stamp() => (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0).ToString("0.000", CultureInfo.InvariantCulture);

        void Send(string json)
        {
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                udp?.Send(bytes, bytes.Length, host, port);
            }
            catch (Exception) { /* no listener running: the demo keeps going */ }
        }
    }
}
