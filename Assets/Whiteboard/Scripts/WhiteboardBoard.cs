using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace QuestWhiteboard
{
    /// <summary>
    /// 巨大ボード本体。線(ストローク)の追加・消去・Undo/Redo・保存・PNG書き出し・背景テーマを管理する。
    /// ボードの「表面」は子オブジェクトの Quad。線はこのオブジェクトのローカル座標で保持するので、
    /// ボードを動かしたり拡大縮小したりすると線も一緒についてくる。
    /// </summary>
    public class WhiteboardBoard : MonoBehaviour
    {
        public enum Theme { White, Chalkboard, GraphPaper }

        [Header("サイズ(メートル)")]
        public float width = 4f;
        public float height = 2f;

        [Header("マテリアル(セットアップメニューが自動で設定)")]
        public Material boardMaterial;
        public Material lineMaterial;

        [Header("背景")]
        public Theme theme = Theme.GraphPaper;

        [Header("保存")]
        public bool autoSave = true;
        public string saveFileName = "board.json";

        public Transform Surface { get; private set; }
        public Collider SurfaceCollider { get; private set; }

        public event Action<Theme> ThemeChanged;

        // 線は表面から少し手前に浮かせて、ちらつきを防ぐ
        const float StrokeOffset = 0.002f;

        readonly List<Stroke> strokes = new List<Stroke>();
        readonly List<IAction> undoStack = new List<IAction>();
        readonly List<IAction> redoStack = new List<IAction>();
        Material surfaceMatInstance;

        void Awake()
        {
            BuildSurface();
            ApplyTheme(theme);
        }

        void Start()
        {
            Load();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && autoSave) Save();
        }

        void OnApplicationQuit()
        {
            if (autoSave) Save();
        }

        // ---------------- 表面 ----------------

        void BuildSurface()
        {
            var existing = transform.Find("Surface");
            GameObject quad;
            if (existing != null)
            {
                quad = existing.gameObject;
            }
            else
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Surface";
                quad.transform.SetParent(transform, false);
            }
            quad.transform.localPosition = Vector3.zero;
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(width, height, 1f);
            Surface = quad.transform;
            SurfaceCollider = quad.GetComponent<Collider>();

            var r = quad.GetComponent<Renderer>();
            if (boardMaterial != null)
            {
                surfaceMatInstance = new Material(boardMaterial);
                r.sharedMaterial = surfaceMatInstance;
            }
            else
            {
                surfaceMatInstance = r.material;
            }
        }

        /// <summary>Unity の Quad は -Z 方向を向いているので、ボードの「表」は -forward。</summary>
        public Vector3 FrontNormal => -transform.forward;

        public void CycleTheme()
        {
            var next = (Theme)(((int)theme + 1) % Enum.GetValues(typeof(Theme)).Length);
            ApplyTheme(next);
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            if (surfaceMatInstance == null) return;
            var tex = BoardTextures.Create(t, width, height);
            SetTexture(surfaceMatInstance, tex);
            SetColor(surfaceMatInstance, Color.white);
            ThemeChanged?.Invoke(t);
        }

        /// <summary>背景に対して見やすい既定のペン色。</summary>
        public Color DefaultInkFor(Theme t) => t == Theme.Chalkboard ? Color.white : new Color(0.1f, 0.1f, 0.15f);

        // ---------------- 座標変換 ----------------

        /// <summary>ワールド座標の点を、ボード上(ローカル2D)に投影する。ボードの範囲外なら false。</summary>
        public bool TryProject(Vector3 world, out Vector2 local)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            local = new Vector2(p.x, p.y);
            return Mathf.Abs(p.x) <= width * 0.5f && Mathf.Abs(p.y) <= height * 0.5f;
        }

        Vector3 ToStrokeLocal(Vector2 p) => new Vector3(p.x, p.y, -StrokeOffset);

        // ---------------- 線を書く ----------------

        public Stroke BeginStroke(Color color, float lineWidth, Vector2 start)
        {
            var s = Stroke.Create(transform, lineMaterial, color, lineWidth);
            s.AddPoint(ToStrokeLocal(start));
            return s;
        }

        public void ContinueStroke(Stroke s, Vector2 p)
        {
            if (s != null) s.AddPoint(ToStrokeLocal(p));
        }

        public void EndStroke(Stroke s)
        {
            if (s == null) return;
            if (s.PointCount < 2)
            {
                // 点だけのときは小さな点として残す
                var p = s.GetPoint(0);
                s.AddPoint(p + new Vector3(0.0005f, 0f, 0f));
            }
            strokes.Add(s);
            Push(new AddAction(this, s));
        }

        // ---------------- 消しゴム ----------------

        /// <summary>指定位置の半径内に触れている線をまとめて消す。</summary>
        public int EraseAt(Vector2 p, float radius)
        {
            var hit = new List<Stroke>();
            foreach (var s in strokes)
            {
                if (s.gameObject.activeSelf && s.Touches(p, radius)) hit.Add(s);
            }
            if (hit.Count == 0) return 0;
            foreach (var s in hit) s.gameObject.SetActive(false);
            Push(new HideAction(hit));
            return hit.Count;
        }

        public void ClearAll()
        {
            var visible = strokes.FindAll(s => s.gameObject.activeSelf);
            if (visible.Count == 0) return;
            foreach (var s in visible) s.gameObject.SetActive(false);
            Push(new HideAction(visible));
        }

        // ---------------- Undo / Redo ----------------

        void Push(IAction a)
        {
            undoStack.Add(a);
            foreach (var r in redoStack) r.Discard();
            redoStack.Clear();
        }

        public void Undo()
        {
            if (undoStack.Count == 0) return;
            var a = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            a.Undo();
            redoStack.Add(a);
        }

        public void Redo()
        {
            if (redoStack.Count == 0) return;
            var a = redoStack[redoStack.Count - 1];
            redoStack.RemoveAt(redoStack.Count - 1);
            a.Redo();
            undoStack.Add(a);
        }

        interface IAction { void Undo(); void Redo(); void Discard(); }

        class AddAction : IAction
        {
            readonly WhiteboardBoard board; readonly Stroke s;
            public AddAction(WhiteboardBoard b, Stroke s) { board = b; this.s = s; }
            public void Undo() { s.gameObject.SetActive(false); }
            public void Redo() { s.gameObject.SetActive(true); }
            public void Discard() { board.strokes.Remove(s); UnityEngine.Object.Destroy(s.gameObject); }
        }

        class HideAction : IAction
        {
            readonly List<Stroke> list;
            public HideAction(List<Stroke> l) { list = l; }
            public void Undo() { foreach (var s in list) s.gameObject.SetActive(true); }
            public void Redo() { foreach (var s in list) s.gameObject.SetActive(false); }
            public void Discard() { }
        }

        // ---------------- 保存・読み込み ----------------

        string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        [Serializable] class StrokeData { public Color color; public float width; public List<Vector3> points = new List<Vector3>(); }
        [Serializable] class BoardData { public int theme; public List<StrokeData> strokes = new List<StrokeData>(); }

        public void Save()
        {
            var data = new BoardData { theme = (int)theme };
            foreach (var s in strokes)
            {
                if (!s.gameObject.activeSelf) continue;
                var d = new StrokeData { color = s.Color, width = s.Width };
                for (int i = 0; i < s.PointCount; i++) d.points.Add(s.GetPoint(i));
                data.strokes.Add(d);
            }
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data));
                Debug.Log($"[Whiteboard] 保存しました: {SavePath}");
            }
            catch (Exception e) { Debug.LogWarning($"[Whiteboard] 保存に失敗: {e.Message}"); }
        }

        public void Load()
        {
            if (!File.Exists(SavePath)) return;
            try
            {
                var data = JsonUtility.FromJson<BoardData>(File.ReadAllText(SavePath));
                if (data == null) return;
                ApplyTheme((Theme)Mathf.Clamp(data.theme, 0, 2));
                foreach (var d in data.strokes)
                {
                    if (d.points.Count == 0) continue;
                    var s = Stroke.Create(transform, lineMaterial, d.color, d.width);
                    foreach (var p in d.points) s.AddPoint(p);
                    strokes.Add(s);
                }
                Debug.Log($"[Whiteboard] {data.strokes.Count} 本の線を読み込みました");
            }
            catch (Exception e) { Debug.LogWarning($"[Whiteboard] 読み込みに失敗: {e.Message}"); }
        }

        // ---------------- PNG 書き出し ----------------

        /// <summary>ボードを PNG 画像として書き出す。戻り値は保存先のパス。</summary>
        public string ExportPng(int pixelWidth = 2048)
        {
            int pw = pixelWidth;
            int ph = Mathf.RoundToInt(pixelWidth * height / width);
            var tex = new Texture2D(pw, ph, TextureFormat.RGBA32, false);

            // 背景
            var bg = BoardTextures.Create(theme, width, height);
            var pixels = new Color32[pw * ph];
            for (int y = 0; y < ph; y++)
            for (int x = 0; x < pw; x++)
            {
                pixels[y * pw + x] = bg.GetPixelBilinear((x + 0.5f) / pw, (y + 0.5f) / ph);
            }

            Destroy(bg);

            float ppm = pw / width; // 1メートルあたりのピクセル数
            foreach (var s in strokes)
            {
                if (!s.gameObject.activeSelf) continue;
                int r = Mathf.Max(1, Mathf.RoundToInt(s.Width * ppm * 0.5f));
                Color32 c = s.Color;
                Vector2 prev = Vector2.zero;
                for (int i = 0; i < s.PointCount; i++)
                {
                    var lp = s.GetPoint(i);
                    var px = new Vector2((lp.x / width + 0.5f) * pw, (lp.y / height + 0.5f) * ph);
                    if (i == 0) Raster.Dot(pixels, pw, ph, px, r, c);
                    else Raster.Line(pixels, pw, ph, prev, px, r, c);
                    prev = px;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            var bytes = tex.EncodeToPNG();
            Destroy(tex);

            var dir = Path.Combine(Application.persistentDataPath, "Exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"board_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(path, bytes);
            Debug.Log($"[Whiteboard] PNG を書き出しました: {path}");
            return path;
        }

        // ---------------- マテリアル補助(URP / Built-in 両対応) ----------------

        internal static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }
    }
}
