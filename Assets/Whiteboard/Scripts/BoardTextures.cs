using UnityEngine;

namespace QuestWhiteboard
{
    /// <summary>背景テクスチャ(白板・黒板・方眼紙)をプログラムで作る。画像ファイル不要。</summary>
    public static class BoardTextures
    {
        const int PixelsPerMeter = 256;

        public static Texture2D Create(WhiteboardBoard.Theme theme, float width, float height)
        {
            int w = Mathf.Clamp(Mathf.RoundToInt(width * PixelsPerMeter), 64, 4096);
            int h = Mathf.Clamp(Mathf.RoundToInt(height * PixelsPerMeter), 64, 4096);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8;

            Color32 bg, minor, major;
            switch (theme)
            {
                case WhiteboardBoard.Theme.Chalkboard:
                    bg = new Color32(28, 60, 48, 255);
                    minor = bg; major = bg;
                    break;
                case WhiteboardBoard.Theme.GraphPaper:
                    bg = new Color32(252, 252, 246, 255);
                    minor = new Color32(205, 225, 240, 255); // 5cm ごとの細線
                    major = new Color32(140, 185, 220, 255); // 25cm ごとの太線
                    break;
                default:
                    bg = new Color32(250, 250, 250, 255);
                    minor = bg; major = bg;
                    break;
            }

            var px = new Color32[w * h];
            int minorStep = PixelsPerMeter / 20; // 5cm
            int majorStep = minorStep * 5;      // 25cm
            int cx = w / 2, cy = h / 2;          // 中心を原点にする(座標軸を描きやすいように)
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color32 c = bg;
                if (theme == WhiteboardBoard.Theme.GraphPaper)
                {
                    int dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                    if (dx % majorStep == 0 || dy % majorStep == 0) c = major;
                    else if (dx % minorStep == 0 || dy % minorStep == 0) c = minor;
                }
                // 縁取り
                if (x < 3 || y < 3 || x >= w - 3 || y >= h - 3)
                    c = theme == WhiteboardBoard.Theme.Chalkboard ? new Color32(120, 85, 50, 255) : new Color32(170, 170, 175, 255);
                px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return tex;
        }
    }

    /// <summary>PNG 書き出し用の簡易ラスタライザ。</summary>
    public static class Raster
    {
        public static void Dot(Color32[] px, int w, int h, Vector2 c, int r, Color32 col)
        {
            int x0 = Mathf.FloorToInt(c.x) - r, x1 = Mathf.CeilToInt(c.x) + r;
            int y0 = Mathf.FloorToInt(c.y) - r, y1 = Mathf.CeilToInt(c.y) + r;
            float r2 = r * r;
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
            {
                float dx = x - c.x, dy = y - c.y;
                if (dx * dx + dy * dy <= r2) px[y * w + x] = col;
            }
        }

        public static void Line(Color32[] px, int w, int h, Vector2 a, Vector2 b, int r, Color32 col)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(1f, r * 0.5f)));
            for (int i = 0; i <= steps; i++) Dot(px, w, h, Vector2.Lerp(a, b, i / (float)steps), r, col);
        }
    }
}
