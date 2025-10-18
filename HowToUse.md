# How to Use (Shapekey Animation Converter)

This tool is a Unity Editor extension for efficiently editing blendshapes (shape keys) and creating animation files. The UI is designed for a top-to-bottom workflow—just follow the settings in order to create your animation.

---

## 1. Select Mesh
- At the top, assign your SkinnedMeshRenderer in the "Mesh" field.
- If no mesh is selected, a warning will be shown.

## 2. Align Keys to Existing Animation

When you enable "Align saved keys to existing animation," the "Base Animation" field below becomes active.
Assign a base animation (.anim file) and click "Apply"—only blendshapes contained in that animation will be marked as save targets (checked).
Blendshapes starting with vrc.* are automatically excluded.

### Main Purpose of This Feature
- **Exclude unnecessary blendshapes**
  - Automatically removes blendshapes that should not be controlled by facial animation (e.g., those used for expression menus or avatar gimmicks), based on the structure of the existing animation.
- **Create difference animations with the same key structure**
  - Useful when you want to create new facial animations that match the blendshape structure of an existing animation.

### Example Usage
- If your avatar's face mesh contains blendshapes for both facial expressions and other features (like expression menu controls),
  you can specify the default facial animation as the base animation and click "Apply" to select only the necessary blendshapes for saving.
  This prevents unnecessary blendshapes from being included in your facial animation.
- If you want to add extra blendshapes, you can manually check them as save targets.
- By continuing to use your created animation as the base animation, you can always edit with a consistent blendshape structure.

## 3. Apply Animation to Mesh
- In the "Apply Animation" section, assign an existing animation (.anim file) and click "Apply".
- The blendshape values at time 0s in the animation will be applied to the current mesh.
- This lets you preview or use existing animations as a base for editing.

## 4. Filter Save Targets
- Enable "Show only enabled shapes" to list only blendshapes that are checked (will be saved).

## 5. Snapshot Feature
- Click "Snapshot" to save the current blendshape values.
- Click "Restore Snapshot" to revert to the saved values.

## 6. Blendshape Search
- Use the "Blendshape Search" field to search by name. Multiple words are AND-searched.

## 7. Adjust Blendshapes
- Use the sliders to adjust each blendshape value.
- Use the checkboxes to select which shapes to save.
- You can also toggle entire groups on/off at once.

## 8. Save Animation
- At the bottom, click "Save Animation" to export the current settings as a .anim file.
- Set the save folder in the "Save To (default)" field.

## 9. English Mode
- Enable "English Mode" at the top right to switch the UI to English.

---

### FAQ

#### Q. What does "Align saved keys to existing animation" mean?
A. It outputs only the blendshapes present in the specified base animation (.anim) to the new animation. Use this when you want to match the structure of an existing facial animation or exclude unnecessary blendshapes.

#### Q. What happens when I click "Apply" in the Base Animation section?
A. Only blendshapes present in the specified animation will be marked as save targets (checked). vrc.* shapes are excluded.

#### Q. Is Undo/Redo supported?
A. Yes, adjusting blendshape values and toggling save targets supports Undo/Redo.
