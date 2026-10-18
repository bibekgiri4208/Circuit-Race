# Arcade racing overhaul

## Controls

| Action | Keyboard | Xbox-style gamepad |
| --- | --- | --- |
| Accelerate | W / Up | RT |
| Brake, then reverse | S / Down | LT |
| Steer | A/D / Left/Right | Left stick |
| Handbrake / initiate a slide | Space | X |
| Return to last validated checkpoint / grid | R | Y |
| Rear view | Hold C | Hold A |
| Look around | Ctrl + mouse | Right stick |

Tap the handbrake while turning, then release it and use throttle and steering to carry the slide. Straighten the car to recover grip and bank the drift chain. Longer slides build a multiplier up to 3x. Heavy impacts, spins, and resets cancel the active chain. Drift points are a separate score; race placement depends on finishing order.

## Gameplay changes

- Grounded acceleration assistance fills the gap between wheel torque and a responsive acceleration target. Drive power tapers toward normal top speed.
- Speed-dependent steering limits cornering demand, with yaw assistance, stronger grip recovery, and an excessive-slip catch. Jumps retain physical momentum.
- Opponents follow distance-based look-ahead targets, plan braking before corners, obey authored waypoint speed caps, and use relative traffic speed to choose following gaps and passing lanes.
- Opponents have small pace differences. A car far ahead eases its cruising pace by at most 7%; cars behind use their ordinary speed and acceleration limits.
- The race manager fills sparse grids to six racers using configured scene opponents. Existing waypoint and wheel references are retained on the clones.
- Level 1's race manager is active again and its partial AI route is replaced by a complete 52-point circuit using the same track geometry as Level 2. Its finish/checkpoints are aligned around the complete lap, with a two-lap race. Level 2 now creates ordered route checkpoints to validate its lap.
- Cars recover to the grid or their last accepted checkpoint. Checkpoint and lap validation stay authoritative; resetting cannot complete a missing checkpoint or advance a lap.
- The HUD includes live position, speed, gear, circuit map, race messages, wrong-way feedback, and drift chains. Results include top speed and banked drift score.
- The chase camera looks into the direction of travel, widens its field of view with speed, pulls back slightly, and reacts to obstructions.

## Main tuning controls

`CarController`:

- `arcadeAcceleration`: target launch acceleration (default 10.5 m/s²).
- `corneringAcceleration`: maximum normal steering demand (default 24 m/s²).
- `maxForwardSpeed`: ordinary top speed in m/s; older km/h prefab values still migrate.
- `driftGripMultiplier`, `driftSteerMultiplier`, `driftAngleLimit`: slide grip, rotation, and catch threshold.
- `steeringAssist`, `maxYawAcceleration`: steering response and stability.

`SimpleAICarController`:

- `maxSpeedKmh`: cruise ceiling. Level 1/2 opponents are tuned around 166–172 km/h.
- `corneringAcceleration`, `brakingDeceleration`: corner pace and braking distance.
- `minimumLookAhead`, `lookAheadTime`: path tracking.
- `passingLaneWidth`: passing offset in metres; reduce it for a narrow course.
- `obstacleLayers`: walls/obstacles; a zero mask falls back to the default raycast layers. Traffic is detected separately across layers, and triggers/road surfaces/self-colliders are filtered.

`RaceManager`:

- `minimumRacers`: desired field size including the player (default six).
- `gridRowSpacing`, `gridLaneOffset`: grid placement along the configured route.
- `routePointsOverride`: optional complete route in world coordinates; used to repair Level 1. An override also aligns the finish and existing ordered checkpoints with that route.

## Verification

The runtime scripts were compiled against Unity 6000.4.1f1. An isolated headless Unity Play Mode harness checked route wrapping/projection, invalid routes, acceleration, top-speed control, high-speed steering stability, braking/reverse, airborne momentum, recovery/cooldown, drift scoring, AI corner tracking/overtaking, grid expansion, continuous standings, and ordered lap validation. All nine player prefabs and both scenes were also checked for valid wheel/path references. These synthetic physics checks complement playing both authored tracks with the real car prefabs and artwork.

Useful in-editor checks: complete a race in both levels, tap/release the handbrake through corners, pass a slower opponent, reset after a crash, and confirm the field fits the starting straight and the HUD is readable at the target resolution.
