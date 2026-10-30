# Reference meadow — Testing scene only

Open `Assets/Scenes/Testing.unity`. All new content is under **GHIBLI Reference Meadow**.

- **Main Camera** is the existing player chase camera, not a fixed screenshot view. **Press Play to drive** your active player car. If no active player is already placed, the existing spawner creates the selected garage car instead.
- **F** switches between driving and free flight. In free flight use **WASD**, **Q/E** down/up, **Shift** for speed, and the mouse to look. If the cursor is unlocked, hold the right mouse button to look. Press **F** again to return to the car. The existing pause/cursor controls still apply. Gamepad Back/Select also switches modes; left/right sticks move/look and shoulders move down/up in flight.
- **Reference Camera • winding path** and **Reference Camera • flower meadow** are optional screenshot viewpoints. Both are disabled and untagged during normal gameplay, with no audio listener to override the player camera. The preview menu renders them explicitly without making either the active gameplay camera.
- The imported GHIBLI prefabs supply the ancient tree, forest, shrubs, mossy stones, fences, shrine, grass and flowers. Source prefabs/materials are not modified.
- Rolling ground, the curved path and distant mountain ridges are generated meshes. Grass and flowers use the pack's original meshes and texture atlases with GPU instancing and compact placement data. A two-sided sunlit foliage shader adds gentle wind, colour gradients, cutouts and real tree shadows; the source pack's unlit grass/flower shaders are not modified. Trees retain their original leaf shaders, with the rim-light control treated as a Fresnel exponent rather than a brightness multiplier.
- Original Testing objects remain in the scene. The original chase camera/listener is re-enabled and bound to the existing active player, or to the project's car spawner when a player needs to be created. Newly spawned cars start on the collidable meadow path. The legacy terrain remains disabled because the meadow has its own ground collider. Other scenes and shared car/controller prefabs are not edited.
- `Preview-Path.png` and `Preview-Meadow.png` are editor renders of the two viewpoints.

`Tools > Ghibli Meadow > Build in Testing Scene` deterministically rebuilds this environment. **Rebuilding overwrites manual changes under the generated root and generated meadow resources.** It does not alter source pack assets, other scenes or build settings.

## Adjust the lighting

Under **GHIBLI Reference Meadow > 07 • Warm sunlight, blue shadows and soft atmospheric haze**, select **Meadow sun • upper right**. Change its **Transform > Rotation** (or use Unity's Rotate tool), and edit its **Light > Intensity / Color / Shadows** normally. The name describes the initial reference angle, not a restriction. There is no script locking or restoring its direction. The sky glow reads URP's current main light, so it follows rotations immediately in Edit mode and Play mode without rebuilding.

The original scene's **Directional Light** is disabled; it does not control this meadow. **Cool soft fill • sky bounce** is a separate, independently editable light. Lower its intensity or disable it if you want stronger directional contrast. Rebuilding preserves both meadow lights' rotations, colors/temperatures, intensities, enabled/active states and shadow type/strength/bias settings; the reference defaults are used only when no existing meadow light is present.

Use `Tools > Ghibli Meadow > Refresh Preview Images` to render the two viewpoints again after manual scene edits. Previews are isolated from other scenes that might be open additively. The meadow camera's post-processing volume mask is restricted to the existing Ignore Raycast layer, so other scenes' global grades cannot leak into it.

The screenshots guide the layout, palette and lighting; this is a reconstruction, not the original source scene or an exact pixel-for-pixel copy.

For screenshot comparisons, use the generated preview images or explicitly render the optional reference cameras. They do not control or restrict the normal gameplay camera.

## Using the map, rather than the reference views

The environment is ordinary 3D geometry with a ground mesh collider and colliders on the pack's solid props. It is not locked to the reference cameras. **Meadow Gameplay • driving + free camera** is a separate, optional Testing harness outside the environment root; its player setup survives environment rebuilds. Move your existing player car (or **Player Car Spawn**, when a spawner is used) to choose a different start, or enable **Start In Free Fly** on `MeadowMapControls`. Disable/remove this harness and use your own player/camera when integrating the map elsewhere. Instanced foliage also renders for cameras in other loaded scenes, not only cameras owned by the meadow scene.

`Tools > Ghibli Meadow > Enable Driving and Free Camera in Testing` installs the player setup without regenerating the environment or resetting the lighting. The builder no longer blanket-disables gameplay cameras or vehicle renderers.
