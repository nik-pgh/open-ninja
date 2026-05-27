using UnityEditor;
using UnityEngine;

namespace OpenNinja.EditorSetup
{
    /// <summary>
    /// One-shot helper: render the Game camera to a 256x256 texture and read
    /// back center-pixel RGB. Tells us what color the wall is actually
    /// rendering at, independent of any screenshot pipeline.
    /// </summary>
    public static class PixelProbe
    {
        public static string Execute()
        {
            var cam = Camera.main;
            if (cam == null) return "no Camera.main";

            var rt = RenderTexture.GetTemporary(256, 512, 16, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(256, 512, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 256, 512), 0, 0);
            tex.Apply();
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            // Sample a few key vertical positions in the center column.
            int x = 128;
            var sb = new System.Text.StringBuilder();
            int[] ys = { 30, 100, 200, 300, 400, 480 };
            foreach (int y in ys)
            {
                var c = tex.GetPixel(x, y);
                sb.AppendLine($"y={y,3} ({y/512f,4:F2}) → ({c.r:F3}, {c.g:F3}, {c.b:F3}) = {Mathf.Max(c.r, c.g, c.b)*100:F1}%");
            }

            Object.DestroyImmediate(tex);
            Debug.Log(sb.ToString());
            return sb.ToString();
        }
    }
}
