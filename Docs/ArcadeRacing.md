# Garage and Practice

The overhaul baseline contains only the Garage → Practice flow. Build Settings starts in `Assets/Scenes/Garage.unity` and includes `Assets/Scenes/Practice.unity`.

## Car selection and spawning

- `Assets/Scripts/Garage/CarSelection.cs` switches Garage display cars, updates the name, and saves the selection under the existing `CarIndexValue` preference before loading Practice directly.
- The Garage Play button now starts Practice. Deleted-track menus and unused environment/multiplayer controls are hidden; their callbacks and scripts were removed.
- `Assets/Scripts/Car/CarSpawner.cs` spawns the saved selection, binds the chase camera and skidmarks, and falls back to a usable prefab for stale or invalid selections.
- Keep the Garage display cars, car names, and Practice spawner prefabs in the same order: Skyline, Mitsubishi, Ford GT, Subaru, Corvette, BMW M3. The spawner prefab uses this order too.
- Script GUIDs for retained components are preserved, including the moved `CarSpawner`.

## Preserved driving systems

`CarController`, `ArcadeVehicleUtility`, `ChaseCamera`, `CarAudio`, `TireSmokeController`, `WheelSkid`, and `Skidmarks` provide driving, recovery, camera, audio, and tire effects. `PauseMenu`, `MenuNavigation`, and `UIButtonFeedback` support pause/resume/restart/return to Garage and keyboard/gamepad menus.

Race management, checkpoints/laps, AI, results, drift scoring, the loading-screen script, Garage presentation scripts, and TextMesh Pro demo scripts were removed. Their serialized components and event callbacks were also removed so retained scenes/prefabs do not reference deleted scripts. Artwork, sample scenes, and unused prefabs remain available for the overhaul, but removed behaviors no longer run.

## Controls

| Action | Keyboard | Xbox-style gamepad |
| --- | --- | --- |
| Accelerate | W / Up | RT |
| Brake, then reverse | S / Down | LT |
| Steer | A/D / Left/Right | Left stick |
| Handbrake / slide | Space | X |
| Recover to Practice spawn | R | Y |
| Rear view | Hold C | Hold A |
| Look around | Ctrl + mouse | Right stick |
| Pause / resume | Escape | Start / B to resume |
| Previous / next Garage car | UI buttons | LB / RB |

Recovery uses the car's initial spawn pose, with an occupancy check and cooldown; it no longer depends on checkpoints. Slide physics and effects remain, without race/drift scoring.

## Verification

The 12 retained scripts compile against the project's Unity 6000.4.1f1 assemblies in both editor and player configurations. A serialized-reference check covered 120 assets, verified that removed-script GUIDs are no longer referenced, and checked all six car slots in both Practice and the reusable spawner prefab, including Rigidbody/wheel assignments and Garage callbacks. No missing script references were found in the game scenes or prefabs.

Unrelated unresolved package/test-component references in `Assets/Settings/DefaultVolumeProfile.asset` and the TextMesh Pro HDRP sample scene/profile were left untouched. These are not references to scripts removed by this cleanup.

### Manual check (still required)

Start Garage, select each car, and enter Practice. Confirm the selected model drives, the camera follows, and audio/smoke/skidmarks work. Test recovery, pause/resume, restart, and return to Garage; the selected car should remain selected. Deleted levels and the old loading screen are no longer in Build Settings.
