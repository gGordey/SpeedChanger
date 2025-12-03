using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedChanger
{
    internal static class CanvasUtil
    {
        internal struct RectData
        {
            internal Vector2 size;
            internal Vector2 position;
            internal Vector2 anchorMin;
            internal Vector2 anchorMax;
            internal Vector2 pivot;

            internal RectData(Vector2 size, Vector2 position, Vector2 anchorMin, Vector2 anchorMax, Vector2? pivot = null)
            {
                this.size = size;
                this.position = position;
                this.anchorMin = anchorMin;
                this.anchorMax = anchorMax;
                this.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            }
        }

        internal static GameObject CreateCanvas(RenderMode renderMode, Vector2 referenceResolution, string name = "SpeedChangerCanvas", int sortOrder = 9999)
        {
            GameObject canvasObject = new GameObject(name);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = renderMode;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<CanvasGroup>();

            return canvasObject;
        }

        internal static GameObject CreateTextPanel(GameObject parent, string text, int fontSize, TextAnchor anchor, RectData rectData, Font font = null)
        {
            GameObject go = new GameObject("SpeedChangerText");
            go.transform.SetParent(parent.transform, false);

            Text txt = go.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = fontSize;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.font = font ?? GetFont("Arial");
            txt.color = Color.white;

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = rectData.size;
            rect.pivot = rectData.pivot;
            rect.anchorMin = rectData.anchorMin;
            rect.anchorMax = rectData.anchorMax;
            rect.anchoredPosition = rectData.position;

            return go;
        }

        internal static Font GetFont(string fontName)
        {
            Font existing = Resources.FindObjectsOfTypeAll<Font>().FirstOrDefault(f => f != null && f.name.Equals(fontName, System.StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (builtin != null) return builtin;
            }
            catch
            {
                // ignored
            }

            return Font.CreateDynamicFontFromOSFont("Arial", 14);
        }
    }
}
