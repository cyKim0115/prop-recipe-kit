using System.Collections.Generic;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// 분할 수 감량 값. 새 인스턴스(기본값)는 감량 없음 = 프로토타입 기본 메시와 같다.
    /// 생성기마다 인자로 넘긴다(전역 상태 없음).
    /// </summary>
    public sealed class PropLod
    {
        /// <summary>RoundBox 모서리 분할 상한.</summary>
        public int BoxSegMax = int.MaxValue;
        /// <summary>Lathe·Torus 분할 배율(1 = 그대로).</summary>
        public float SegScale = 1f;
        /// <summary>배율 적용 뒤 최소 분할 수.</summary>
        public int SegMin = 3;
        /// <summary>0 이상이면 Extrude 테두리 arc를 이 값으로 고정한다(-1 = 인자 그대로).</summary>
        public int ExtrudeArc = -1;
        /// <summary>Lathe 단면 솎기 간격(Simplify가 0일 때만).</summary>
        public int ProfileStep = 1;
        /// <summary>Extrude 외곽선 솎기 간격(Simplify가 0일 때만).</summary>
        public int OutlineStep = 1;
        /// <summary>0보다 크면 외곽선·단면을 Douglas-Peucker로 줄인다(허용 오차 = 크기 대각선 × 이 값).</summary>
        public float Simplify = 0f;

        /// <summary>감량 없음(프로토타입 기본).</summary>
        public static PropLod None() => new PropLod();

        /// <summary>프로토타입 파일럿 「reduced」 프리셋.</summary>
        public static PropLod Reduced()
        {
            return new PropLod
            {
                BoxSegMax = 1,
                SegScale = 0.375f,
                SegMin = 6,
                ExtrudeArc = 1,
                Simplify = 0.01f,
            };
        }

        public PropLod Clone() => (PropLod)MemberwiseClone();

        public bool IsDefault => BoxSegMax == int.MaxValue && Mathf.Approximately(SegScale, 1f) && ExtrudeArc < 0
            && ProfileStep <= 1 && OutlineStep <= 1 && Simplify <= 0f;

        public int Seg(int segments)
        {
            return Mathf.Approximately(SegScale, 1f) ? segments : Mathf.Max(SegMin, Mathf.RoundToInt(segments * SegScale));
        }

        /// <summary>점 목록을 step 간격으로 솎는다. 처음·끝 점은 남긴다. Simplify &gt; 0이면 Douglas-Peucker를 쓴다.</summary>
        public IList<Vector2> Thin(IList<Vector2> pts, int step)
        {
            if (Simplify > 0f) return DouglasPeucker(pts, Simplify);
            if (step <= 1 || pts.Count <= 4) return pts;
            var outPts = new List<Vector2>();
            for (int i = 0; i < pts.Count; i += step) outPts.Add(pts[i]);
            if ((pts.Count - 1) % step != 0) outPts.Add(pts[pts.Count - 1]);
            return outPts;
        }

        public static IList<Vector2> DouglasPeucker(IList<Vector2> pts, float rel)
        {
            if (pts.Count <= 4) return pts;
            Vector2 mn = pts[0], mx = pts[0];
            foreach (Vector2 q in pts) { mn = Vector2.Min(mn, q); mx = Vector2.Max(mx, q); }
            float tol = (mx - mn).magnitude * rel;
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                var (a, b) = stack.Pop();
                float best = -1f;
                int idx = -1;
                for (int i = a + 1; i < b; i++)
                {
                    float d = DistToSegment(pts[i], pts[a], pts[b]);
                    if (d > best) { best = d; idx = i; }
                }
                if (idx >= 0 && best > tol)
                {
                    keep[idx] = true;
                    stack.Push((a, idx));
                    stack.Push((idx, b));
                }
            }
            var outPts = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++) if (keep[i]) outPts.Add(pts[i]);
            return outPts.Count >= 3 ? outPts : pts;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        public override string ToString()
        {
            return $"PropLod(boxSegMax {BoxSegMax}, segScale {SegScale}, segMin {SegMin}, extrudeArc {ExtrudeArc}, profileStep {ProfileStep}, outlineStep {OutlineStep}, simplify {Simplify})";
        }
    }
}
