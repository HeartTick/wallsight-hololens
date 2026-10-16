# Technical notes

Design decisions and problems solved while building WallSight in about a day at the Mobile Robotics Hackathon 2026.

## 1. From robot scan to a walkable map
**Input:** `data/hallway-001_clean.ply`, a cleaned indoor scan recorded with a Boston Dynamics Spot. It is a binary PLY with 195,480 points (x, y, z float + RGB), already in Unity axes (x right, y up, z forward), in metres, with the floor levelled to y = 0.

**`scripts/build_occupancy_grid.py`** bins the points onto the floor plane in 10 cm cells:
- **floor:** points with y ≤ 0.10 m mark a cell as seen floor; small gaps are closed (dilate + erode, radius 2 cells).
- **obstacle:** at least 3 points between 0.15 m and 1.50 m. This band ignores the floor and anything overhead, and needs several hits so single noisy points don't count.
- **unknown:** everything else. Unknown cells are **not walkable**: the robot never saw them, so the planner never routes through them.

Result: 124 × 121 cells (16 % free, 19 % obstacle, 66 % unknown), exported as compact JSON plus a preview PNG.

## 2. Route planning (`RoutePlanner.cs`)
- **A\***, 8-connected, octile heuristic, no diagonal corner cutting past walls, binary-heap open list (no `PriorityQueue` in Unity's .NET profile).
- **Clearance:** a multi-source BFS gives every cell its distance to the nearest non-free cell. Cells within 1 cell are blocked, and a soft penalty over 4 cells keeps routes in the middle of corridors. The clearance was chosen from the data: with 10 cm the two corridors stay connected through the doorway, with 20 cm they split.
- **Largest-area filter:** small isolated floor fragments are scan noise. Only the largest connected free area is walkable, so a tap never lands on an unreachable island.
- **Snapping:** taps snap to the nearest walkable cell within 6 cells.
- **Hazards:** a disc (0.4 m) is blocked and a ring around it (+0.4 m) gets extra cost. The size was tuned to the scan: a 0.8 m hazard closes the 2 m corridor completely.
- **Verification:** the planner was first ported to Python and run on the real grid (route 10.7 m; detour 11.5 m; doorway blocked → no route) before it was run in Unity.
- Everything lives in the **scan's local metres** under the model's transform, so the same route works for the tabletop miniature (scale 0.04) and would work at 1:1.

## 3. HoloLens 2 without MRTK
Unity 6.6 is newer than Microsoft's recommended HoloLens 2 setup (2022.3 + MRTK3). To stay light and in control:
- **OpenXR 1.18 + Mixed Reality OpenXR plugin 1.11.2**, using **Holographic Remoting for Play Mode** (the editor renders, the headset displays) so there is no 10–20 minute UWP build per test.
- **Hand input:** Input System actions bound directly to the OpenXR *Microsoft Hand Interaction* profile (`<HoloLensHand>{RightHand}/pointerPosition`, `pointerRotation`, `selectPressed`, `isTracked`). The aim ray is intersected with the model's floor plane; pinch sets the target, pinch-and-hold drags it with throttled re-planning.
- **Head tracking:** `HeadPoseFollower` reads the centre-eye pose from `UnityEngine.XR.InputDevices` in `Update` and `onBeforeRender` (a Tracked Pose Driver added through the editor had no default bindings).
- **Placement:** `PlaceInFrontOfUser` puts the model 1 m ahead of the wearer's gaze, 0.35 m below eye level, after tracking starts (and again on **R**).
- **Rendering:** solid black camera background (black is transparent on the additive HoloLens display), Single Pass Instanced, 16-bit depth submission. About 57 FPS in the editor while remoting.

### Making Microsoft's plugin compile on Unity 6.6
The plugin is embedded under `unity/Packages/com.microsoft.mixedreality.openxr` with two small patches (both marked `WallSight patch`):
1. **AR Foundation 6.6 added its own `XRMarker` / `XRMarkerSubsystem` types**, so `ARMarkerManager.cs` had ambiguous references (CS0104). Fixed with three `using` aliases pointing at Microsoft's types.
2. **`RenderGraphSettings.enableRenderCompatibilityMode` is read-only in Unity 6.6** (CS0200) inside an optional editor validation "Fix" action. That assignment was removed; there's no runtime effect.

Installing the plugin also needed a manual tarball install, because the venue network aborted the registry download.

## 4. Robot command stream
`RobotCommandPublisher` sends JSON lines over UDP (default `127.0.0.1:5005`):
- `goal`: target position, full path and length on every route change (throttled to 4 Hz while dragging); `no_path` when blocked.
- `cmd`: at 5 Hz from the simulated robot's actual motion: pose, linear speed, angular speed, and an action (FORWARD / TURN_LEFT / TURN_RIGHT / STOP).

Coordinates are converted from Unity's left-handed frame to **ROS REP-103** (x forward, y left, z up): `x = z_unity`, `y = −x_unity`, `z = y_unity`, `yaw = −yaw_unity`. `scripts/robot_bridge_listener.py` prints them as `/goal_pose` (PoseStamped), `/plan` (Path), `/cmd_vel` (Twist) and `/odom`. A ROS 2 node that forwards this stream to Nav2 is the natural next step.

## 5. Known limitations
- The scan is a recorded snapshot and is not registered to the room the demo runs in; the model is a tabletop miniature.
- Hazards and the robot on the map are simulated.
- Holographic Remoting streams over Wi-Fi, so fast head movements can briefly drop the image. A native UWP build removes that.

## 6. Next steps
Native HoloLens build · live mapping from the robot · real hazard sensors (temperature, gas) · ROS 2 / Nav2 bridge to a real follower robot · registering the scan 1:1 to the building for a walk-in view.
