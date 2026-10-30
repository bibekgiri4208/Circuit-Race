# Reference meadow — Testing scene only

Open `Assets/Scenes/Testing.unity`. All new content is under **GHIBLI Reference Meadow**.

- **Main Camera • winding path** is the default view, composed after reference 1.
- **Alternate Camera • flower meadow** is the low meadow view from reference 2. Disable the main camera and enable this camera to use it.
- The imported GHIBLI prefabs supply the ancient tree, forest, shrubs, mossy stones, fences, shrine, grass and flowers. Source prefabs/materials are not modified.
- Rolling ground, the curved path and distant mountain ridges are generated meshes. Grass and flowers use the pack's original meshes and texture atlases with GPU instancing and compact placement data. A two-sided sunlit foliage shader adds gentle wind, colour gradients, cutouts and real tree shadows; the source pack's unlit grass/flower shaders are not modified. Trees retain their original leaf shaders, with the rim-light control treated as a Fresnel exponent rather than a brightness multiplier.
- Original Testing objects remain in the scene. Its old cameras/audio listeners, directional light, terrain/collider and legacy renderers are disabled, not deleted. Other scenes are not edited.
- `Preview-Path.png` and `Preview-Meadow.png` are editor renders of the two viewpoints.

`Tools > Ghibli Meadow > Build in Testing Scene` deterministically rebuilds this environment. **Rebuilding overwrites manual changes under the generated root and generated meadow resources.** It does not alter source pack assets, other scenes or build settings.

Use `Tools > Ghibli Meadow > Refresh Preview Images` to render the two viewpoints again after manual scene edits. Previews are isolated from other scenes that might be open additively. The meadow camera's post-processing volume mask is restricted to the existing Ignore Raycast layer, so other scenes' global grades cannot leak into it.

The screenshots guide the layout, palette and lighting; this is a reconstruction, not the original source scene or an exact pixel-for-pixel copy.

Compare in **Game view** using the reference cameras, rather than an arbitrary Scene view angle. The upper-right sun, soft sky fill, lavender tree, S-shaped path, low enclosed forest edge and flower meadow are composed for these viewpoints.
