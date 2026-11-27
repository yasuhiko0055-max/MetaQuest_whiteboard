using System.Collections.Generic;
using UnityEngine;

namespace QuestWhiteboard
{
    /// <summary>1本の線。ボードのローカル座標で点を持ち、LineRenderer で描く。</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class Stroke : MonoBehaviour
    {
        // 手ぶれで点が増えすぎないよう、これより近い点は捨てる(メートル)
        const float MinSpacing = 0.002f;

        readonly List<Vector3> points = new List<Vector3>();
        LineRenderer lr;

        public Color Color { get; private set; }
        public float Width { get; private set; }
        public int PointCount => points.Count;
        public Vector3 GetPoint(int i) => points[i];

        public static Stroke Create(Transform board, Material baseMaterial, Color color, float width)
        {
            var go = new GameObject("Stroke");
            go.transform.SetParent(board, false);
            var lr = go.AddComponent<LineRenderer>();
            var s = go.AddComponent<Stroke>();
            s.lr = lr;
            s.Color = color;
            s.Width = width;

            lr.useWorldSpace = false;
            lr.alignment = LineAlignment.TransformZ; // ボード面に沿って平たく描く
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.widthMultiplier = width;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = color;
            lr.endColor = color;

            var mat = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Sprites/Default"));
            WhiteboardBoard.SetColor(mat, color);
            lr.sharedMaterial = mat;
            return s;
        }

        public void AddPoint(Vector3 local)
        {
            if (points.Count > 0 && (points[points.Count - 1] - local).sqrMagnitude < MinSpacing * MinSpacing) return;
            points.Add(local);
            lr.positionCount = points.Count;
            lr.SetPosition(points.Count - 1, local);
        }

        /// <summary>ボード上の点 p から半径 radius 以内にこの線が触れているか。</summary>
        public bool Touches(Vector2 p, float radius)
        {
            float r = radius + Width * 0.5f;
            float r2 = r * r;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                if (i == 0)
                {
                    if ((a - p).sqrMagnitude <= r2) return true;
                    continue;
                }
                Vector2 b = points[i - 1];
                if (DistSqToSegment(p, b, a) <= r2) return true;
            }
            return false;
        }

        static float DistSqToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).sqrMagnitude;
        }

        void OnDestroy()
        {
            if (lr != null && lr.sharedMaterial != null) Destroy(lr.sharedMaterial);
        }
    }
}
