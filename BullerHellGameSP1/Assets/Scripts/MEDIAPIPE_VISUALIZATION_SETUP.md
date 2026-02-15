# MediaPipe Webcam Visualization Setup Guide
**Following MediaPipe Unity Sample Pattern**

This guide shows you how to add webcam visualization with pose/hand annotations to your game, exactly like the MediaPipe Unity samples.

---

## Overview

The visualization creates this hierarchy (following MediaPipe Unity samples):

```
MediaPipe Visualization (GameObject)
├── MediaPipe Screen Wrapper (Mediapipe.Unity.Screen component)
└── MediaPipe Visualization Canvas (Canvas - Screen Space Overlay)
    └── Annotatable Screen (RectTransform + Image background)
        ├── RawImage Display (RawImage - shows webcam feed)
        ├── Pose Annotations (PoseLandmarkerResultAnnotationController)
        │   └── MultiPoseLandmarkListWithMaskAnnotation (Prefab instance)
        └── Hand Annotations (HandLandmarkerResultAnnotationController)
            └── Multi HandLandmarkList Annotation (Prefab instance)
```

---

## Quick Setup (Recommended)

### 1. Add MediaPipeVisualization Component

1. Create empty GameObject: `GameObject → Create Empty`
2. Rename to `MediaPipe Visualization`
3. Add component: `MediaPipeVisualization` script
4. The script will auto-create the Canvas and UI hierarchy

### 2. Add Annotation Controllers

**For Pose Annotations:**

1. Create empty GameObject as child of `Annotatable Screen`
2. Rename to `Pose Annotations`
3. Add component: `PoseLandmarkerResultAnnotationController`
4. In the Inspector, find the `Annotation` field
5. Drag in prefab: `Packages/MediaPipe Unity Plugin/PackageResources/Prefabs/MultiPoseLandmarkListWithMaskAnnotation.prefab`

**For Hand Annotations:**

1. Create empty GameObject as child of `Annotatable Screen`
2. Rename to `Hand Annotations`
3. Add component: `HandLandmarkerResultAnnotationController`
4. In the Inspector, find the `Annotation` field
5. Drag in prefab: `Packages/MediaPipe Unity Plugin/PackageResources/Prefabs/Multi HandLandmarkList Annotation.prefab`

### 3. Connect References

Select `MediaPipe Visualization` GameObject and in the Inspector:

**MediaPipe Components section:**
- Drag `Pose Annotations` → `Pose Annotation Controller` field
- Drag `Hand Annotations` → `Hand Annotation Controller` field

**MediaPipe Runners:** (Should auto-find)
- Verify `Pose Runner` references your PoseLandmarkerRunner
- Verify `Hand Runner` references your HandLandmarkerRunner

### 4. Configure Display

Adjust these settings in the Inspector:

```
Display Settings:
├─ Show Visualization: ✓
├─ Display Size: (640, 480)
└─ Display Position: (-320, -240)  // From top-right corner

Visualization Options:
├─ Show Pose Annotations: ✓
└─ Show Hand Annotations: ✓
```

---

## How It Works

### Initialization Sequence

1. **Awake:**
   - Auto-finds PoseLandmarkerRunner and HandLandmarkerRunner
   - Creates UI hierarchy (Canvas → Annotatable Screen → RawImage)
   - Creates MediaPipe Screen wrapper component

2. **Start:**
   - Gets ImageSource from static `ImageSourceProvider`
   - Waits for image source to be prepared (coroutine)

3. **InitializeVisualization:**
   - Initializes `Screen` component with ImageSource
   - Sets up annotation controllers with proper dimensions
   - Configures mirroring based on camera type

4. **Runtime:**
   - MediaPipe Screen wrapper handles texture updates
   - Annotation controllers draw landmarks on top

### Key Components

**Mediapipe.Unity.Screen Wrapper:**
- Handles texture assignment to RawImage
- Manages rotation and flipping
- Calculates proper UV rect for mirroring

**Annotatable Screen (RectTransform):**
- Container for webcam feed + annotations
- Positioned in top-right corner
- Has dark background for visibility

**Annotation Controllers:**
- Draw pose/hand landmarks on Canvas
- Auto-size to match webcam dimensions
- Support mirroring for front-facing cameras

---

## Manual Prefab Setup (If Needed)

If annotation prefabs aren't assigned automatically:

### Pose Annotation Prefab Path:
`Packages/com.github.homuler.mediapipe/PackageResources/Prefabs/MultiPoseLandmarkListWithMaskAnnotation.prefab`

### Hand Annotation Prefab Path:
`Packages/com.github.homuler.mediapipe/PackageResources/Prefabs/Multi HandLandmarkList Annotation.prefab`

**To assign:**
1. Select annotation controller GameObject
2. Find the `Annotation` field in Inspector
3. Click the circle icon → search for prefab name
4. Double-click to assign

---

## Controls

- **Press 'V'** to toggle visualization on/off during gameplay
- Adjust size/position in Inspector while playing (OnValidate updates in real-time)

---

## Customization

### Change Display Size

```csharp
GetComponent<MediaPipeVisualization>().SetDisplaySize(new Vector2(800, 600));
```

### Change Display Position

```csharp
// Position from top-right corner (negative values move left/down)
GetComponent<MediaPipeVisualization>().SetDisplayPosition(new Vector2(-400, -300));
```

### Toggle Visibility

```csharp
GetComponent<MediaPipeVisualization>().SetVisualizationVisible(false);
```

### Adjust Annotation Colors

1. Find the annotation prefab instance in scene hierarchy
2. Expand to find the landmark list annotation
3. Adjust colors in Inspector:
   - **Pose:** `Left Landmark Color`, `Right Landmark Color`
   - **Hand:** Similar color fields

---

## Troubleshooting

### No webcam feed showing

**Cause:** ImageSource not initialized
**Solution:**
- Check Console for "[MediaPipe Visualization] ImageSource not available"
- Ensure your scene has a Bootstrap or that MediaPipe runners are properly set up
- Verify `ImageSourceProvider.ImageSource` is not null

### Annotations not appearing

**Cause:** Annotation controllers not connected to runners
**Solution:**
- The annotation controllers need to receive results from the runners
- **Option A:** Manually connect in your runner scripts:
  ```csharp
  // In PoseLandmarkerRunner's result callback:
  poseAnnotationController.DrawLater(result);
  ```
- **Option B:** Use the MediaPipe sample scene as a base and add your game to it

### Annotations misaligned

**Cause:** Image dimensions mismatch
**Solution:**
- Check that `InitScreen()` was called with correct dimensions
- Verify `annotationController.imageSize` matches webcam resolution
- Check Console for initialization logs

### Canvas not showing on top

**Cause:** Sorting order too low
**Solution:**
- Select `MediaPipe Visualization Canvas`
- Set `Sorting Order` to 100 or higher

---

## Integration Notes

### With Existing MediaPipe Setup

If you already have MediaPipe runners in your scene:
- The script auto-finds them via `FindObjectOfType`
- Make sure only ONE of each runner type exists
- The visualization uses the same ImageSource as your game input

### Performance Considerations

- Annotations are drawn using Unity UI (Canvas)
- Minimal performance impact (mainly GPU for line rendering)
- Can safely disable during gameplay with 'V' key
- Clean up on destroy to prevent memory leaks

---

## Example Scene Setup

Your final scene should have:

```
Game Scene
├── Main Camera
├── Player Controller
├── Game Objects...
│
├── MediaPipe Setup
│   ├── Bootstrap (from MediaPipe samples)
│   ├── PoseLandmarkerRunner
│   ├── HandLandmarkerRunner
│   ├── MediaPipeInputBridge (your game input)
│   └── MediaPipeVisualization (visualization overlay) ← NEW
│       ├── MediaPipe Screen Wrapper
│       └── MediaPipe Visualization Canvas
│           └── Annotatable Screen
│               ├── RawImage Display
│               ├── Pose Annotations
│               └── Hand Annotations
└── ...
```

---

## Next Steps

1. **Add the component** to your scene
2. **Assign annotation prefabs** in Inspector
3. **Test with 'V' key** to verify visibility toggle
4. **Adjust size/position** to fit your game UI
5. **Use the visualization** to tune your MediaPipe input parameters!

This visualization is perfect for debugging depth tracking, calibrating sensitivity, and understanding how MediaPipe sees your movements!
