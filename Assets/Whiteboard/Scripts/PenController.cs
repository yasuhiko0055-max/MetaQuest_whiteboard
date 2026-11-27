using UnityEngine;

namespace QuestWhiteboard
{
    /// <summary>
    /// Quest のコントローラーをペンにする。コントローラーの先から出る光線がボードに当たった所に書く。
    /// 左右どちらのコントローラーにも付けられる(OVRCameraRig の Left/RightControllerAnchor に付ける)。
    ///
    /// 操作:
    ///   人差し指トリガー … 書く(消しゴムモードなら消す)
    ///   スティック左右   … 色を変える
    ///   スティック上下   … 太さを変える
    ///   A / X           … 元に戻す(Undo)
    ///   スティック押し込み … やり直し(Redo)
    ///   B / Y           … 消しゴム切り替え(1.5秒長押しで全消去。全消去も Undo で戻せる)
    ///   メニューボタン(左手のみ) … 背景切り替え(白板→黒板→方眼紙)、1秒長押しで PNG 書き出し+保存
    /// </summary>
    public class PenController : MonoBehaviour
    {
        public OVRInput.Controller controller = OVRInput.Controller.RTouch;
        public WhiteboardBoard board;

        [Header("ペン")]
        public float maxRayDistance = 8f;
        public float[] widths = { 0.004f, 0.008f, 0.015f, 0.03f, 0.06f };
        public int widthIndex = 1;
        public Color[] palette =
        {
            new Color(0.10f, 0.10f, 0.15f), // 黒
            Color.white,
            new Color(0.90f, 0.20f, 0.25f), // 赤
            new Color(0.15f, 0.45f, 0.95f), // 青
            new Color(0.15f, 0.70f, 0.35f), // 緑
            new Color(1.00f, 0.60f, 0.10f), // オレンジ
            new Color(0.60f, 0.30f, 0.85f), // 紫
            new Color(1.00f, 0.85f, 0.15f), // 黄
            new Color(1.00f, 0.45f, 0.70f), // ピンク
        };
        public int colorIndex = 0;

        [Header("消しゴム")]
        public float eraserRadius = 0.04f;

        bool eraser;
        Stroke current;
        float stickXLatch, stickYLatch;
        float twoHeldSince = -1f, startHeldSince = -1f;
        bool twoLongFired, startLongFired;

        LineRenderer laser;
        Transform cursor;
        Renderer cursorRenderer;
        Transform tip;
        Renderer tipRenderer;

        void Start()
        {
            if (board == null) board = FindFirstObjectByType<WhiteboardBoard>();
            if (board != null)
            {
                colorIndex = ClosestPaletteIndex(board.DefaultInkFor(board.theme));
                board.ThemeChanged += t => colorIndex = ClosestPaletteIndex(board.DefaultInkFor(t));
            }
            BuildVisuals();
        }

        void OnDisable()
        {
            EndStroke();
            OVRInput.SetControllerVibration(0f, 0f, controller);
        }

        void Update()
        {
            if (board == null) return;
            HandleButtons();

            var ray = new Ray(transform.position, transform.forward);
            bool onBoard = false;
            Vector2 local = Vector2.zero;
            Vector3 hitPoint = ray.origin + ray.direction * 0.15f;

            if (board.SurfaceCollider != null &&
                board.SurfaceCollider.Raycast(ray, out var hit, maxRayDistance) &&
                Vector3.Dot(ray.direction, board.FrontNormal) < 0f &&
                board.TryProject(hit.point, out local))
            {
                onBoard = true;
                hitPoint = hit.point;
            }

            float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller);
            bool pressing = trigger > 0.55f;

            if (pressing && onBoard)
            {
                if (eraser)
                {
                    if (board.EraseAt(local, eraserRadius) > 0) Buzz(0.4f, 0.05f);
                }
                else if (current == null)
                {
                    current = board.BeginStroke(palette[colorIndex], widths[widthIndex], local);
                }
                else
                {
                    board.ContinueStroke(current, local);
                }
                if (!eraser) OVRInput.SetControllerVibration(0.3f, 0.08f, controller); // 書いている手ごたえ
            }
            else
            {
                EndStroke();
            }

            UpdateVisuals(ray.origin, hitPoint, onBoard);
        }

        void EndStroke()
        {
            if (current != null)
            {
                board.EndStroke(current);
                current = null;
                OVRInput.SetControllerVibration(0f, 0f, controller);
            }
        }

        void HandleButtons()
        {
            // スティック:倒して戻すと1段階だけ変わる
            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, controller);
            if (Mathf.Abs(stick.x) > 0.7f && stickXLatch == 0f)
            {
                stickXLatch = Mathf.Sign(stick.x);
                colorIndex = (colorIndex + (int)stickXLatch + palette.Length) % palette.Length;
                eraser = false;
                Buzz(0.5f, 0.03f);
            }
            else if (Mathf.Abs(stick.x) < 0.3f) stickXLatch = 0f;

            if (Mathf.Abs(stick.y) > 0.7f && stickYLatch == 0f)
            {
                stickYLatch = Mathf.Sign(stick.y);
                widthIndex = Mathf.Clamp(widthIndex + (int)stickYLatch, 0, widths.Length - 1);
                Buzz(0.5f, 0.03f);
            }
            else if (Mathf.Abs(stick.y) < 0.3f) stickYLatch = 0f;

            if (OVRInput.GetDown(OVRInput.Button.One, controller)) { EndStroke(); board.Undo(); Buzz(0.6f, 0.05f); }
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, controller)) { EndStroke(); board.Redo(); Buzz(0.6f, 0.05f); }

            // B / Y:短押しで消しゴム、長押しで全消去
            if (OVRInput.GetDown(OVRInput.Button.Two, controller)) { twoHeldSince = Time.time; twoLongFired = false; }
            if (OVRInput.Get(OVRInput.Button.Two, controller) && twoHeldSince >= 0f && !twoLongFired && Time.time - twoHeldSince > 1.5f)
            {
                twoLongFired = true;
                EndStroke();
                board.ClearAll();
                Buzz(1f, 0.25f);
            }
            if (OVRInput.GetUp(OVRInput.Button.Two, controller))
            {
                if (!twoLongFired) { eraser = !eraser; Buzz(0.5f, 0.05f); }
                twoHeldSince = -1f;
            }

            // メニューボタンは左手にしかない
            if (controller == OVRInput.Controller.LTouch)
            {
                if (OVRInput.GetDown(OVRInput.Button.Start, controller)) { startHeldSince = Time.time; startLongFired = false; }
                if (OVRInput.Get(OVRInput.Button.Start, controller) && startHeldSince >= 0f && !startLongFired && Time.time - startHeldSince > 1f)
                {
                    startLongFired = true;
                    board.Save();
                    board.ExportPng();
                    Buzz(1f, 0.3f);
                }
                if (OVRInput.GetUp(OVRInput.Button.Start, controller))
                {
                    if (!startLongFired) board.CycleTheme();
                    startHeldSince = -1f;
                }
            }
        }

        // ---------------- 見た目(光線・カーソル・ペン先) ----------------

        void BuildVisuals()
        {
            var unlit = board != null && board.lineMaterial != null ? board.lineMaterial : new Material(Shader.Find("Sprites/Default"));

            var laserGo = new GameObject("PenLaser");
            laserGo.transform.SetParent(transform, false);
            laser = laserGo.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.widthMultiplier = 0.002f;
            laser.sharedMaterial = new Material(unlit);
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            cursor = MakeSphere("PenCursor", unlit, out cursorRenderer);
            cursor.SetParent(null, false);

            tip = MakeSphere("PenTip", unlit, out tipRenderer);
            tip.SetParent(transform, false);
            tip.localPosition = new Vector3(0f, 0f, 0.03f);
            tip.localScale = Vector3.one * 0.015f;
        }

        static Transform MakeSphere(string name, Material baseMat, out Renderer r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            r = go.GetComponent<Renderer>();
            r.sharedMaterial = new Material(baseMat);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void UpdateVisuals(Vector3 origin, Vector3 hitPoint, bool onBoard)
        {
            Color c = eraser ? new Color(1f, 1f, 1f, 0.6f) : palette[colorIndex];

            laser.enabled = true;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, hitPoint);
            var lc = c; lc.a = onBoard ? 0.8f : 0.3f;
            laser.startColor = lc; laser.endColor = lc;
            WhiteboardBoard.SetColor(laser.sharedMaterial, lc);

            cursor.gameObject.SetActive(onBoard);
            if (onBoard)
            {
                cursor.position = hitPoint + board.FrontNormal * 0.003f;
                float d = eraser ? eraserRadius * 2f : Mathf.Max(widths[widthIndex], 0.01f);
                cursor.localScale = Vector3.one * d;
                WhiteboardBoard.SetColor(cursorRenderer.sharedMaterial, c);
            }

            WhiteboardBoard.SetColor(tipRenderer.sharedMaterial, c);
            tip.localScale = Vector3.one * (eraser ? 0.03f : 0.01f + widths[widthIndex] * 0.5f);
        }

        void OnDestroy()
        {
            if (cursor != null) Destroy(cursor.gameObject);
        }

        void Buzz(float amplitude, float seconds)
        {
            OVRInput.SetControllerVibration(0.5f, amplitude, controller);
            CancelInvoke(nameof(StopBuzz));
            Invoke(nameof(StopBuzz), seconds);
        }

        void StopBuzz()
        {
            if (current == null) OVRInput.SetControllerVibration(0f, 0f, controller);
        }

        int ClosestPaletteIndex(Color target)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                var p = palette[i];
                float d = (p.r - target.r) * (p.r - target.r) + (p.g - target.g) * (p.g - target.g) + (p.b - target.b) * (p.b - target.b);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
    }
}
