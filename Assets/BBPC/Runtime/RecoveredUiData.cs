using System;
namespace BBPC.UnityRuntime
{
    // Portable projection of decoded serialized data; field provenance lives in analysis/p2.
    [Serializable] public sealed class RecoveredUiData { public string sourceVersion; public UiScene[] scenes; public UiScene row; public UiAsset[] assets;public UiFilterStyle filterStyle; }
    [Serializable] public sealed class UiFilterStyle {public string upChoiceSprite,downChoiceSprite,upNormalSprite,downNormalSprite;}
    [Serializable] public sealed class UiScene { public string name; public UiObject[] objects; }
    [Serializable] public sealed class UiAsset { public string id, name, type, resource, sha256; public float ppu; public float[] border, pivot; }
    [Serializable] public sealed class UiObject
    {
        public string id, name, transformId, parent, sourceHash, buttonTarget; public string[] children, scripts;
        public bool active, canvas, button, mask, maskGraphic; public float[] position, rotation, scale, rect; public int[] fit;
        public UiMarquee marquee;public UiRawImage rawImage; public UiShadow[] shadows; public UiLayout layout; public UiLayoutElement layoutElement; public UiSlider slider; public UiAspect aspect; public UiCanvas canvasSettings; public UiImage image; public UiText text; public UiScaler scaler; public UiScroll scroll;
    }
    [Serializable] public sealed class UiMarquee {public bool recovered,autoStart;public float delay,speed;}
    [Serializable] public sealed class UiRawImage { public string texture,pending; public bool enabled,raycast; public float[] color,uv; }
    [Serializable] public sealed class UiShadow { public bool enabled, useGraphicAlpha; public int style; public float[] color, distance; public float blur; }
    [Serializable] public sealed class UiLocalizedSprite { public string language, sprite; }
    [Serializable] public sealed class UiLayout { public bool enabled; public int kind, alignment, startCorner, startAxis, constraint, constraintCount; public int[] padding; public float[] spacing, cellSize; public bool[] flags; }
    [Serializable] public sealed class UiLayoutElement { public bool enabled, ignore; public int priority; public float[] sizes; }
    [Serializable] public sealed class UiSlider { public string fillRect, handleRect; public float min, max, value; public bool wholeNumbers, interactable; public int direction; }
    [Serializable] public sealed class UiAspect { public int mode; public float ratio; public bool enabled; }
    [Serializable] public sealed class UiCanvas { public bool enabled, overrideSorting; public int mode, cameraFile, sortingOrder, targetDisplay; public long cameraId; public float planeDistance; }
    [Serializable] public sealed class UiImage { public UiLocalizedSprite[] locales; public string sprite; public float[] color; public bool enabled, raycast, preserve, clockwise, fillCenter, useSpriteMesh; public int type, fillMethod, fillOrigin; public float fill, ppuMultiplier; }
    [Serializable] public sealed class UiText { public string value, font, key; public int size, style, alignment, minSize, maxSize, horizontal, vertical; public bool enabled, bestFit, rich, raycast; public float lineSpacing; public float[] color; }
    [Serializable] public sealed class UiScaler { public int mode; public float[] resolution; public float match, ppu; }
    [Serializable] public sealed class UiScroll { public string content, viewport; public bool horizontal, vertical, inertia; public int movement; public float elasticity, deceleration, sensitivity; }
}
