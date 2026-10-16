# WallSight: presentation guide (hackathon jury demo)

> The one page the team presented from at the final jury session.

---

## 1. The project in 30 seconds

**Event:** Mobile Robotics Hackathon 2026, TU Dresden · **Track 05: Intuitive Human-Robot Interaction**. The track is judged on **usability, responsiveness, intuitiveness**.

**One sentence:** A robot scans a building first. A person wearing a HoloLens 2 holds that scan as a hologram, pinches where they need to go, and gets a route that is planned live on the robot's own map and re-planned around danger zones, before anyone walks in.

**Problem:** People who must enter a building they cannot see or do not know (smoke, darkness, an unfamiliar site) have no sense of the space. A robot can go in first and scan it. The hard part is handing that scan to a person in a form they understand at a glance and can act on, without a joystick or a 2D floor plan.

**Transformation:** "A trained operator reads point clouds on a laptop" → "anyone pinches a spot on a hologram and sees the safe way there."

---

## 2. What we built

| Part | What it does | Status |
|---|---|---|
| Robot scan | Spot indoor scan `hallway-001`: 195,480 points, cleaned, floor levelled, in metres | ✅ real data |
| Occupancy map | Scan → 2D grid of 10 cm cells: free floor / obstacle / unknown (`scripts/build_occupancy_grid.py`) | ✅ |
| Route planner | A* on the grid, live in Unity: 10 cm wall clearance, keeps to the middle of corridors, unscanned space counts as not walkable | ✅ |
| Hazards | Simulated danger zones (0.4 m radius + 0.4 m margin): the route detours around them, or reports "No safe route" | ✅ (simulated) |
| Simulated robot | Drives the planned route on the map | ✅ (simulated) |
| HoloLens 2 | Tabletop hologram 1 m in front of the user, head-tracked, through Holographic Remoting from the laptop | ✅ |
| Hand control | Right hand: ray + pinch sets the target, pinch and drag moves it (route follows live). Left hand: pinch adds or removes a hazard | ✅ |
| Robot command stream | Goal, path and motion commands sent from Unity over UDP to a robot-side bridge terminal, in the scan map frame with ROS axes | ✅ |

### How it fits together
```
Spot scan (point cloud, metres)
   │  build_occupancy_grid.py (offline, once)
   ▼
Occupancy grid (10 cm) ──► Unity RoutePlanner (A*, live) ──► route, hazards, simulated robot
Scan mesh ──────────────► tabletop hologram ◄──────────────┘
                                 │                                 │
                 HoloLens 2 (remoting): look, pinch, drag       UDP 127.0.0.1:5005
                                                                   ▼
                                                     robot bridge terminal:
                                                     /goal_pose /plan /cmd_vel /odom
```

### Numbers you can quote (all measured today)
- Scan: **195,480 points** → grid **124 × 121 cells** at **10 cm** (16 % free floor, 19 % obstacle, 66 % unknown).
- Example route **10.7 m**; with one hazard in the corridor the detour is **11.5 m**; a hazard in the doorway gives **"no safe route"**.
- Remoting: **57 FPS** in Unity, GPU **2.7 ms** per frame.
- Re-planning while dragging: up to **8 times per second**.

---

## 3. What is real and what is simulated

Say this proactively. It makes the real parts more credible.

| Real | Simulated / not yet |
|---|---|
| The 3D scan from a Spot robot | The hazards (fire / danger zones) are placed by hand |
| Map, A* planning, clearance, re-planning | The robot on the map is a marker, not a live robot |
| HoloLens 2 head tracking, hand ray, pinch | The scan is a fixed snapshot recorded earlier, not live mapping |
| Goal, path and motion commands, computed live and streamed | The bridge terminal is our own UDP process with ROS message formats; it is not a ROS 2 / Nav2 stack |
| | The hallway is not the jury hall; the model is a tabletop miniature |

---

## 4. Demo script (about 2 minutes)

**Roles:** **A** wears the HoloLens · **B** presents · **C** sits at the laptop (Unity, terminal, projector).

| Time | B says | A / C does |
|---|---|---|
| 0:00–0:15 | "In a fire or a blackout, rescuers enter buildings they can't see. A robot can go first, but its map is a point cloud on a laptop. Nobody reads that under stress." | Slide: problem |
| 0:15–0:25 | "WallSight puts the robot's scan in your hands." | C: switch the projector to the live view + terminal |
| 0:25–0:40 | "This is a real scan Spot recorded of a hallway, on the table in front of her, in metres." | A walks half a step around the model |
| 0:40–1:00 | "She pinches where she needs to go. The route is planned on the robot's own map, live, keeping clear of walls." | A: right pinch at the far end → route + robot appear. Terminal: `/goal_pose`, `/plan` |
| 1:00–1:15 | "If the target moves, the route follows instantly." | A: pinch, hold, drag the target |
| 1:15–1:30 | "Now a fire, simulated, in the corridor. The route goes around it." | A: left pinch on the route → detour, longer `/plan` |
| 1:30–1:40 | "Block the only doorway, and it says so: no safe route. The robot holds position." | A: left pinch in the doorway → "No safe route", terminal `STOP` |
| 1:40–1:50 | "And every decision streams to the robot side: goal, path and velocity commands, in the robot's map frame." | Point at `/cmd_vel FORWARD / TURN LEFT` lines |
| 1:50–2:00 | "Next: a real follower robot subscribes to this stream, localised in the same scan, and live hazard sensors replace the simulated ones." | Slide: next steps |

**Backup if the headset fails:** C plays the fallback video. B narrates the same script over it.

---

## 5. Setup checklist (do it in the hall before 16:55)

**Laptop**
- [ ] Charger plugged in · Focus assist on (no notifications) · VPN **off**
- [ ] Unity open, scene **`WallSight_XR`**, Console with 0 red errors
- [ ] **Mixed Reality → Remoting → Holographic Remoting for Play Mode:** IP = the one the HoloLens Remoting Player shows **now** (it changes with the network) · button shows **"Disable…"** (meaning it is enabled)
- [ ] Terminal open in the repo folder: `py -3 scripts\robot_bridge_listener.py` → "waiting for the HoloLens interface..." · font large (Ctrl + mouse wheel)
- [ ] Device Portal open in the browser (`https://<HoloLens IP>`) → **Views → Mixed Reality Capture → Live preview** (Holograms + PV camera ticked)
- [ ] Projector: live view on the left (**Win + ←**), terminal on the right (**Win + →**)
- [ ] Fallback video file ready to play offline, outside the repo

**HoloLens 2**
- [ ] Charged · on the same network as the laptop (phone hotspot if the hall Wi-Fi is bad)
- [ ] Holographic Remoting Player open and waiting
- [ ] Wearer put it on **before** Play is pressed

**Start sequence**
1. A puts on the HoloLens and looks at the table.
2. C presses **▶ Play** → after 2 s the model appears 1 m in front of A.
3. Model in the wrong place: A looks where it should go, C presses **R** in the Game view.
4. Check: the terminal says "connected: WallSight HoloLens 2".

---

## 6. Controls cheat sheet

| Where | Input | Effect |
|---|---|---|
| HoloLens, right hand | point at the model floor | white ray + yellow cursor dot |
| | pinch | set target → route + robot |
| | pinch, hold, move | drag the target, route follows |
| HoloLens, left hand | pinch on the floor | add / remove a simulated hazard |
| Laptop, Game view | **R** | re-place the model in front of the wearer |
| | right-click | set target (backup for the hand) |
| | Shift + right-click | move the start point |
| | H + right-click | add / remove a hazard |
| | **C** | clear all hazards |

**Hand tips for the wearer:** keep the hand in front of the chest, palm away, point **down** at the model, pinch firmly, and move your head calmly (fast head turns make the remoted image drop out).

---

## 7. Likely jury questions, and answers

| Question | Answer |
|---|---|
| Is the robot real? | The scan is real Spot data. The robot on the map is a simulated marker driving our planned route; its commands stream live to the robot side. |
| Does the headset see through walls? | No. It shows the robot's scan. The robot is the sensor; the headset is the interface. |
| Is that terminal ROS? | It's our bridge process with ROS message formats and topic names, in the scan map frame and ROS axes. The next step is a ROS 2 node that forwards it to Nav2. |
| How accurate is the route? | It is planned on 10 cm cells with 10 cm wall clearance; unscanned space is never used. Accuracy is bounded by the scan, which was cleaned and floor-levelled. |
| Why HoloLens and not a tablet? | Hands stay free, the map is 3D at a glance, and you pinch where you mean: no menus, no joystick. That is the usability point of the track. |
| What happens when it's blocked? | It says "No safe route" and the robot holds position. It never invents a path through unknown space. |
| Why does it flicker when you move fast? | The image is streamed from the laptop over Wi-Fi (Holographic Remoting). A native HoloLens build removes that, and it's next on the list. |
| What would you do with one more week? | Live mapping from the robot instead of a snapshot · real hazard sensors (temperature, gas) · ROS 2 / Nav2 on a real robot · align the scan 1:1 with the real building for a walk-in view. |

---

## 8. Slide outline (5 slides max)

1. **Title:** WallSight. *Robot scan → safe route, in your hands.* Team names. One image: the HoloLens capture of the hologram over the room.
2. **Problem:** rescuers enter unknown, dark or smoky buildings. The robot's map is unreadable under stress. *(One photo or icon, one sentence.)*
3. **How it works:** the pipeline diagram from section 2 (scan → grid → A* → HoloLens + robot stream).
4. **Live demo** (switch to the projector).
5. **Real vs simulated, and next steps:** the table from section 3 (short) + three next steps. End with the one sentence.

---

## 9. Presentation tips

- **Lead with the human, not the tech.** Problem in one sentence before any word like "A*" or "OpenXR".
- **One person talks, one person does the demo.** The wearer stays silent and slow.
- **Narrate what the jury can't see.** They see the projector, not the headset: "She's pinching now…".
- **Name the limits first.** Saying "hazards are simulated, the scan is a snapshot" before anyone asks earns trust.
- **Show numbers.** "10.7 m route, 11.5 m detour, re-plans 8× per second" beats "it's fast".
- **Rehearse the switch moments** (slides → live view → back) at least twice; they are where demos stall.
- **If something breaks:** say "let me show you the recording" and play the video without apologising. Don't debug on stage.
- **Stop on time.** End with the one sentence, then "Thank you, questions?"
- **Battery and network:** HoloLens on the charger until the last minute; hotspot ready as plan B.
- **Privacy:** no bystanders' faces in the projected live view or the video.
