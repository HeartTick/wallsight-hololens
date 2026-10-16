"""Robot-side bridge for WallSight: receives goals, paths and motion commands from the HoloLens app.

Listens for JSON lines over UDP (default 127.0.0.1:5005) sent by RobotCommandPublisher.cs and prints them
as ROS 2-style topic messages in the scan map frame (REP-103 axes: x forward, y left, z up).
A simple follower robot can consume the same stream: goal + path, or the cmd_vel-style commands.

Usage: py -3 scripts/robot_bridge_listener.py [--port 5005]
"""

import argparse
import json
import math
import os
import socket
import time

GREEN, CYAN, YELLOW, RED, GREY, BOLD, RESET = "\033[92m", "\033[96m", "\033[93m", "\033[91m", "\033[90m", "\033[1m", "\033[0m"
ARROWS = {"FORWARD": "^ FORWARD", "TURN_LEFT": "< TURN LEFT", "TURN_RIGHT": "> TURN RIGHT", "STOP": "# STOP"}


def log(topic, colour, text):
    print(f"{GREY}[INFO] [{time.time():.3f}] [wallsight_robot_bridge]{RESET} {colour}{topic}{RESET} {text}", flush=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5005)
    a = ap.parse_args()
    os.system("")  # enable ANSI colours in the Windows console

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind((a.host, a.port))
    print(f"{BOLD}WallSight robot bridge{RESET}  udp://{a.host}:{a.port}  frame: scan_map (x forward, y left, z up, metres)")
    print(f"{GREY}topics: /goal_pose (geometry_msgs/PoseStamped)  /plan (nav_msgs/Path)  "
          f"/cmd_vel (geometry_msgs/Twist)  /odom (pose){RESET}")
    log("bridge", GREEN, "waiting for the HoloLens interface...")

    last_action, last_cmd_print = None, 0.0
    while True:
        data, _ = sock.recvfrom(65535)
        try:
            m = json.loads(data.decode("utf-8"))
        except ValueError:
            continue
        kind = m.get("type")
        if kind == "hello":
            log("bridge", GREEN, f"connected: {m.get('source', 'XR interface')}  frame={m.get('frame')}")
        elif kind == "goal":
            log("/goal_pose", YELLOW, f"PoseStamped frame={m['frame']}  position x={m['x']:+.2f} y={m['y']:+.2f} z={m['z']:+.2f} m")
            path = m.get("path", [])
            pts = "  ".join(f"({x:+.2f},{y:+.2f})" for x, y in path[:6]) + ("  ..." if len(path) > 6 else "")
            log("/plan", CYAN, f"Path {len(path)} poses, {m['length_m']:.2f} m: {pts}")
        elif kind == "no_path":
            log("/plan", RED, "no safe path to goal (blocked) -> robot holds position")
        elif kind == "cmd":
            action = m["action"]
            now = time.time()
            # Print on every change of action; while moving, also once per second; a standstill only once.
            if action != last_action or (action != "STOP" and now - last_cmd_print >= 1.0):
                colour = RED if action == "STOP" else GREEN
                log("/cmd_vel", colour, f"Twist linear.x={m['linear']:+.2f} m/s angular.z={m['angular']:+.2f} rad/s  {ARROWS.get(action, action)}")
                log("/odom", GREY, f"x={m['x']:+.2f} y={m['y']:+.2f} yaw={math.degrees(m['yaw']):+.0f} deg")
                last_action, last_cmd_print = action, now


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        pass
