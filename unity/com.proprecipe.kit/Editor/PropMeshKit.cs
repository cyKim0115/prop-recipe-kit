using System.Collections.Generic;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// 로우폴리 소품 메시 생성기(라운드 박스, 회전체, 판 돌출, 도넛)와 2D 외곽선·단면 도우미.
    /// 수치 연산은 원본 프로토타입과 비트 단위로 같게 유지한다(정리·최적화 금지). lod = null이면 감량 없음.
    /// </summary>
    public static class PropMeshKit
    {
        /// <summary>모서리가 둥근 상자. 피벗 = 가운데.</summary>
        public static Mesh RoundBox(Vector3 size, float radius, int seg = 4, PropLod lod = null)
        {
            if (lod == null) lod = new PropLod();
            seg = Mathf.Min(seg, lod.BoxSegMax);
            Vector3 h = size * 0.5f;
            float r = Mathf.Max(0.001f, Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f));
            Vector3 inner = new Vector3(h.x - r, h.y - r, h.z - r);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();

            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int ua = (axis + 1) % 3;
                    int va = (axis + 2) % 3;
                    float[] su = Samples(h[ua], r, seg);
                    float[] sv = Samples(h[va], r, seg);
                    int baseIndex = verts.Count;
                    for (int j = 0; j < sv.Length; j++)
                    {
                        for (int i = 0; i < su.Length; i++)
                        {
                            Vector3 p = Vector3.zero;
                            p[axis] = sign * h[axis];
                            p[ua] = su[i];
                            p[va] = sv[j];
                            Vector3 c = new Vector3(
                                Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 n = (p - c).normalized;
                            verts.Add(c + n * r);
                            norms.Add(n);
                        }
                    }
                    Vector3 faceN = Vector3.zero;
                    faceN[axis] = sign;
                    Vector3 du = Vector3.zero; du[ua] = 1f;
                    Vector3 dv = Vector3.zero; dv[va] = 1f;
                    bool flip = Vector3.Dot(Vector3.Cross(du, dv), faceN) < 0f;
                    int w = su.Length;
                    for (int j = 0; j < sv.Length - 1; j++)
                    {
                        for (int i = 0; i < w - 1; i++)
                        {
                            int a = baseIndex + j * w + i;
                            int b = a + 1;
                            int c = a + w;
                            int d = c + 1;
                            if (flip)
                            {
                                tris.Add(a); tris.Add(c); tris.Add(b);
                                tris.Add(b); tris.Add(c); tris.Add(d);
                            }
                            else
                            {
                                tris.Add(a); tris.Add(b); tris.Add(c);
                                tris.Add(b); tris.Add(d); tris.Add(c);
                            }
                        }
                    }
                }
            }
            var mesh = new Mesh { name = "RoundBox" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float[] Samples(float half, float r, int seg)
        {
            var list = new List<float>();
            for (int i = 0; i <= seg; i++)
            {
                list.Add(-half + r * i / seg);
            }
            for (int i = 0; i <= seg; i++)
            {
                float v = half - r + r * i / seg;
                if (v > list[list.Count - 1] + 1e-5f)
                {
                    list.Add(v);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 회전체. profile = (반지름, 높이) 점을 아래에서 위로. lobes &gt; 0이면 반지름을 cos(lobes·θ)로 물결지게(꽃잎·호박 골).
        /// 피벗 = 축 위 y=0.
        /// </summary>
        public static Mesh Lathe(IList<Vector2> profile, int segments = 32, int lobes = 0, float lobeAmp = 0f, bool sharpLobes = false, PropLod lod = null)
        {
            if (lod == null) lod = new PropLod();
            int baseSegments = segments;
            segments = lod.Seg(segments);
            if (lobes > 0 && segments != baseSegments)
            {
                // 꽃잎 끝을 놓치지 않게: 꽃잎마다 4점 이상, 꽃잎 수의 배수
                segments = Mathf.Max(segments, lobes * 4);
                segments = Mathf.CeilToInt(segments / (float)lobes) * lobes;
            }
            profile = lod.Thin(profile, lod.ProfileStep);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int ring = segments + 1;
            for (int p = 0; p < profile.Count; p++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float t = (s % segments) / (float)segments * Mathf.PI * 2f;
                    float k = 1f;
                    if (lobes > 0)
                    {
                        float c = Mathf.Cos(lobes * t);
                        if (sharpLobes)
                        {
                            c = Mathf.Pow(Mathf.Abs(Mathf.Cos(lobes * t * 0.5f)), 0.6f) * 2f - 1f;
                        }
                        k = 1f + lobeAmp * c;
                    }
                    float r = profile[p].x * k;
                    verts.Add(new Vector3(Mathf.Sin(t) * r, profile[p].y, Mathf.Cos(t) * r));
                }
            }
            for (int p = 0; p < profile.Count - 1; p++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = p * ring + s;
                    int b = a + 1;
                    int c = a + ring;
                    int d = c + 1;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            }
            var mesh = new Mesh { name = "Lathe" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // 이음매(첫 열·끝 열)와 극점 법선을 평균해 줄무늬를 없앤다.
            Vector3[] n = mesh.normals;
            for (int p = 0; p < profile.Count; p++)
            {
                int a = p * ring;
                int b = a + segments;
                if (profile[p].x < 1e-4f)
                {
                    Vector3 sum = Vector3.zero;
                    for (int s = 0; s <= segments; s++) sum += n[a + s];
                    sum = sum.normalized;
                    for (int s = 0; s <= segments; s++) n[a + s] = sum;
                }
                else
                {
                    Vector3 avg = (n[a] + n[b]).normalized;
                    n[a] = avg;
                    n[b] = avg;
                }
            }
            mesh.normals = n;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>모서리가 둥근 원기둥(또는 원뿔대) 단면. 아래 가운데 → 위 가운데.</summary>
        public static List<Vector2> RoundCylProfile(float rBottom, float rTop, float height, float bevel, int arc = 4)
        {
            float b = Mathf.Min(bevel, Mathf.Min(rBottom, rTop) * 0.95f, height * 0.49f);
            b = Mathf.Max(b, 0.001f);
            var pts = new List<Vector2> { new Vector2(0f, 0f) };
            for (int i = 0; i <= arc; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / arc;
                pts.Add(new Vector2(rBottom - b + Mathf.Cos(a) * b, b + Mathf.Sin(a) * b));
            }
            for (int i = 0; i <= arc; i++)
            {
                float a = Mathf.PI * 0.5f * i / arc;
                pts.Add(new Vector2(rTop - b + Mathf.Cos(a) * b, height - b + Mathf.Sin(a) * b));
            }
            pts.Add(new Vector2(0f, height));
            return pts;
        }

        /// <summary>구(지름 1, 아래 극이 y=-0.5). 피벗 = 가운데.</summary>
        public static List<Vector2> SphereProfile(int steps = 14)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float phi = Mathf.PI * i / steps;
                pts.Add(new Vector2(Mathf.Sin(phi) * 0.5f, -Mathf.Cos(phi) * 0.5f));
            }
            return pts;
        }

        /// <summary>위쪽 반구(돔, 반지름 1, 바닥 y=0 평평).</summary>
        public static List<Vector2> DomeProfile(int steps = 10)
        {
            var pts = new List<Vector2> { new Vector2(0f, 0f) };
            for (int i = 0; i <= steps; i++)
            {
                float phi = Mathf.PI * 0.5f * i / steps;
                pts.Add(new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)));
            }
            return pts;
        }

        // ───────── 판 돌출 ─────────

        /// <summary>
        /// 닫힌 2D 외곽선(XY)을 Z로 돌출한 판. 두께 depth(z ∈ [-depth/2, depth/2]), 테두리는 반지름 bevel로 둥글린다.
        /// 앞·뒷면은 외곽선 그대로(귀 자르기, 오목해도 뒤집히지 않음), 옆면은 bevel만큼 바깥으로 부푼다.
        /// 앞면 법선 = +Z. 외곽선 방향은 자동으로 맞춘다. 피벗 = 외곽선 원점, 두께 가운데.
        /// </summary>
        public static Mesh Extrude(IList<Vector2> outline, float depth, float bevel, int arc = 3, PropLod lod = null)
        {
            if (lod == null) lod = new PropLod();
            if (lod.ExtrudeArc >= 0) arc = lod.ExtrudeArc;
            var pts = Clean(lod.Thin(outline, lod.OutlineStep));
            int n = pts.Count;
            float h = Mathf.Max(depth * 0.5f, 0.001f);
            // 작은 판은 테두리를 줄인다(바깥으로 부푸는 양 = bevel).
            Vector2 bmin = pts[0], bmax = pts[0];
            foreach (Vector2 q in pts) { bmin = Vector2.Min(bmin, q); bmax = Vector2.Max(bmax, q); }
            float shapeMin = Mathf.Min(bmax.x - bmin.x, bmax.y - bmin.y);
            float b = Mathf.Clamp(Mathf.Min(bevel, shapeMin * 0.1f), 0.0005f, h * 0.95f);
            arc = Mathf.Max(1, arc);

            // 바깥 법선과 마이터 길이. 날카로운 모서리(45도 넘게 꺾임)는 음영 법선을 둘로 나눈다.
            var nrm = new Vector2[n];
            var miter = new float[n];
            var colIndex = new List<int>();
            var colNormal = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = pts[(i - 1 + n) % n];
                Vector2 cur = pts[i];
                Vector2 next = pts[(i + 1) % n];
                Vector2 e0 = (cur - prev).normalized;
                Vector2 e1 = (next - cur).normalized;
                Vector2 n0 = new Vector2(e0.y, -e0.x);
                Vector2 n1 = new Vector2(e1.y, -e1.x);
                Vector2 nv = n0 + n1;
                if (nv.sqrMagnitude < 1e-8f) nv = n0;
                nv.Normalize();
                nrm[i] = nv;
                miter[i] = 1f / Mathf.Max(Vector2.Dot(nv, n0), 0.35f);
                if (Vector2.Dot(n0, n1) < 0.7f)
                {
                    colIndex.Add(i); colNormal.Add(n0);
                    colIndex.Add(i); colNormal.Add(n1);
                }
                else
                {
                    colIndex.Add(i); colNormal.Add(nv);
                }
            }
            int cols = colIndex.Count;

            // 링(뒤 캡 → 앞 캡): inset, z, 법선 xy 배율, 법선 z
            var ringInset = new List<float>();
            var ringZ = new List<float>();
            var ringNxy = new List<float>();
            var ringNz = new List<float>();
            for (int j = arc; j >= 0; j--)
            {
                float t = Mathf.PI * 0.5f * j / arc;
                ringInset.Add(b * (1f - Mathf.Cos(t)));
                ringZ.Add(-(h - b) - b * Mathf.Sin(t));
                ringNxy.Add(Mathf.Cos(t));
                ringNz.Add(-Mathf.Sin(t));
            }
            for (int j = 0; j <= arc; j++)
            {
                float t = Mathf.PI * 0.5f * j / arc;
                ringInset.Add(b * (1f - Mathf.Cos(t)));
                ringZ.Add((h - b) + b * Mathf.Sin(t));
                ringNxy.Add(Mathf.Cos(t));
                ringNz.Add(Mathf.Sin(t));
            }
            int rings = ringZ.Count;

            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            for (int r = 0; r < rings; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i = colIndex[c];
                    Vector2 sn = colNormal[c];
                    Vector2 p = pts[i] + nrm[i] * (miter[i] * (b - ringInset[r]));
                    verts.Add(new Vector3(p.x, p.y, ringZ[r]));
                    norms.Add(new Vector3(sn.x * ringNxy[r], sn.y * ringNxy[r], ringNz[r]).normalized);
                }
            }
            for (int r = 0; r < rings - 1; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int c1 = (c + 1) % cols;
                    if (colIndex[c] == colIndex[c1]) continue;
                    int a = r * cols + c;
                    int bb = r * cols + c1;
                    int cc = (r + 1) * cols + c;
                    int d = (r + 1) * cols + c1;
                    tris.Add(a); tris.Add(bb); tris.Add(cc);
                    tris.Add(bb); tris.Add(d); tris.Add(cc);
                }
            }

            // 캡: 원래 외곽선을 귀 자르기로 삼각형화(테두리 링의 캡 쪽 끝과 같은 자리)
            List<int> capTris = Triangulate(pts);
            int front = verts.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(pts[i].x, pts[i].y, h));
                norms.Add(Vector3.forward);
            }
            int back = verts.Count;
            for (int i = 0; i < n; i++)
            {
                verts.Add(new Vector3(pts[i].x, pts[i].y, -h));
                norms.Add(Vector3.back);
            }
            for (int k = 0; k < capTris.Count; k += 3)
            {
                tris.Add(front + capTris[k]); tris.Add(front + capTris[k + 1]); tris.Add(front + capTris[k + 2]);
                tris.Add(back + capTris[k]); tris.Add(back + capTris[k + 2]); tris.Add(back + capTris[k + 1]);
            }

            var mesh = new Mesh { name = "Plate" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>도넛(링). 축 = 로컬 Y(링은 XZ 평면). major = 가운데 원 반지름, minor = 관 반지름. 피벗 = 가운데.</summary>
        public static Mesh Torus(float major, float minor, int segMajor = 32, int segMinor = 10, float arcDeg = 360f, PropLod lod = null)
        {
            if (lod == null) lod = new PropLod();
            segMajor = lod.Seg(segMajor);
            segMinor = lod.Seg(segMinor);
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            float arcRad = Mathf.Clamp(arcDeg, 1f, 360f) * Mathf.Deg2Rad;
            for (int i = 0; i <= segMajor; i++)
            {
                float u = arcRad * i / segMajor;
                float cu = Mathf.Cos(u), su = Mathf.Sin(u);
                for (int j = 0; j <= segMinor; j++)
                {
                    float v = Mathf.PI * 2f * j / segMinor;
                    float cv = Mathf.Cos(v), sv = Mathf.Sin(v);
                    verts.Add(new Vector3(cu * (major + minor * cv), minor * sv, su * (major + minor * cv)));
                    norms.Add(new Vector3(cu * cv, sv, su * cv));
                }
            }
            int w = segMinor + 1;
            for (int i = 0; i < segMajor; i++)
            {
                for (int j = 0; j < segMinor; j++)
                {
                    int a = i * w + j;
                    int b = (i + 1) * w + j;
                    int c = a + 1;
                    int d = b + 1;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            }
            var mesh = new Mesh { name = "Torus" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ───────── 외곽선 생성기(모두 반시계, 별도 표기가 없으면 가운데 = 원점) ─────────

        /// <summary>둥근 별. 첫 꼭짓점이 +Y. smooth = 모서리 깎기 반복 횟수(통통함).</summary>
        public static List<Vector2> StarOutline(int points, float rOuter, float rInner, int smooth = 2)
        {
            var pts = new List<Vector2>();
            for (int k = 0; k < points * 2; k++)
            {
                float a = Mathf.PI * 0.5f + Mathf.PI * k / points;
                float r = (k % 2 == 0) ? rOuter : rInner;
                pts.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r));
            }
            return Smooth(pts, smooth);
        }

        /// <summary>정다각형(첫 꼭짓점 +Y). 세모 눈·육각 같은 데 쓴다.</summary>
        public static List<Vector2> PolygonOutline(int sides, float radius, int smooth = 1)
        {
            var pts = new List<Vector2>();
            for (int k = 0; k < sides; k++)
            {
                float a = Mathf.PI * 0.5f + Mathf.PI * 2f * k / sides;
                pts.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            return Smooth(pts, smooth);
        }

        /// <summary>하트. size = 가로 폭, 뾰족한 끝이 -Y.</summary>
        public static List<Vector2> HeartOutline(float size, int seg = 56)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < seg; i++)
            {
                float t = Mathf.PI * 2f * i / seg;
                float s = Mathf.Sin(t);
                float x = 16f * s * s * s;
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                pts.Add(new Vector2(x, y));
            }
            return FitBox(pts, size, size * 0.92f);
        }

        /// <summary>꽃(둥근 꽃잎). 첫 꽃잎이 +Y.</summary>
        public static List<Vector2> FlowerOutline(int petals, float rOuter, float rInner, int segPerPetal = 12)
        {
            var pts = new List<Vector2>();
            int seg = petals * segPerPetal;
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                float k = Mathf.Pow(Mathf.Abs(Mathf.Cos(petals * a * 0.5f)), 0.55f);
                float r = rInner + (rOuter - rInner) * k;
                float th = a + Mathf.PI * 0.5f;
                pts.Add(new Vector2(Mathf.Cos(th) * r, Mathf.Sin(th) * r));
            }
            return pts;
        }

        /// <summary>뭉게구름(가장자리 혹 bumps개). width×height 상자에 맞춘다.</summary>
        public static List<Vector2> CloudOutline(float width, float height, int bumps = 7, int seg = 84)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i < seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                float k = 0.84f + 0.16f * Mathf.Pow(Mathf.Abs(Mathf.Cos(bumps * (a - Mathf.PI * 0.5f) * 0.5f)), 0.5f);
                // 아래쪽은 조금 납작하게
                float squash = Mathf.Sin(a) < 0f ? 0.86f : 1f;
                pts.Add(new Vector2(Mathf.Cos(a) * k, Mathf.Sin(a) * k * squash));
            }
            return FitBox(pts, width, height);
        }

        /// <summary>가리비 띠: 위 변은 곧고 아래로 반원 count개가 늘어진다. 벽 띠·차양·이불 끝단용.</summary>
        public static List<Vector2> ScallopStrip(float width, float height, int count, int seg = 8)
        {
            float rx = width / (2f * count);
            float ry = Mathf.Min(rx, height * 0.85f);
            float y0 = -height * 0.5f + ry;
            var pts = new List<Vector2>();
            for (int i = 0; i < count; i++)
            {
                float cx = -width * 0.5f + rx + 2f * rx * i;
                for (int s = (i == 0 ? 0 : 1); s <= seg; s++)
                {
                    float th = Mathf.PI + Mathf.PI * s / seg;
                    pts.Add(new Vector2(cx + Mathf.Cos(th) * rx, y0 + Mathf.Sin(th) * ry));
                }
            }
            pts.Add(new Vector2(width * 0.5f, height * 0.5f));
            pts.Add(new Vector2(-width * 0.5f, height * 0.5f));
            return pts;
        }

        /// <summary>물결 띠: 아래 변은 곧고 위 변이 waves번 출렁인다.</summary>
        public static List<Vector2> WaveStrip(float width, float height, int waves, float amp = -1f, int seg = 64)
        {
            if (amp < 0f) amp = height * 0.25f;
            var pts = new List<Vector2> { new Vector2(-width * 0.5f, -height * 0.5f), new Vector2(width * 0.5f, -height * 0.5f) };
            for (int i = 0; i <= seg; i++)
            {
                float u = 1f - (float)i / seg;
                float x = -width * 0.5f + width * u;
                float y = height * 0.5f - amp + amp * Mathf.Cos(Mathf.PI * 2f * waves * u);
                pts.Add(new Vector2(x, y));
            }
            return pts;
        }

        /// <summary>아치(위가 둥근 직사각형). 아래 변 가운데 = 원점(y ∈ [0, height]).</summary>
        public static List<Vector2> ArchOutline(float width, float height, int seg = 20)
        {
            float rx = width * 0.5f;
            float ry = Mathf.Min(rx, height);
            float top = height - ry;
            var pts = new List<Vector2> { new Vector2(-rx, 0f), new Vector2(rx, 0f) };
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI * i / seg;
                pts.Add(new Vector2(Mathf.Cos(th) * rx, top + Mathf.Sin(th) * ry));
            }
            return pts;
        }

        /// <summary>위쪽 반고리(아치 몰딩·무지개·차양 틀). 가운데 = 원점, 아래 끝이 y=0.</summary>
        public static List<Vector2> HalfRingOutline(float rOuter, float rInner, int seg = 20)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI * i / seg;
                pts.Add(new Vector2(Mathf.Cos(th) * rOuter, Mathf.Sin(th) * rOuter));
            }
            for (int i = seg; i >= 0; i--)
            {
                float th = Mathf.PI * i / seg;
                pts.Add(new Vector2(Mathf.Cos(th) * rInner, Mathf.Sin(th) * rInner));
            }
            return pts;
        }

        /// <summary>모서리가 둥근 직사각형.</summary>
        public static List<Vector2> RoundRectOutline(float width, float height, float radius, int seg = 5)
        {
            float r = Mathf.Clamp(radius, 0.001f, Mathf.Min(width, height) * 0.5f - 0.0005f);
            float hx = width * 0.5f - r;
            float hy = height * 0.5f - r;
            var pts = new List<Vector2>();
            Vector2[] centers = { new Vector2(hx, -hy), new Vector2(hx, hy), new Vector2(-hx, hy), new Vector2(-hx, -hy) };
            for (int c = 0; c < 4; c++)
            {
                float start = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * c;
                for (int i = 0; i <= seg; i++)
                {
                    float th = start + Mathf.PI * 0.5f * i / seg;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r);
                }
            }
            return pts;
        }

        /// <summary>방패(위는 둥근 모서리, 아래는 뾰족).</summary>
        public static List<Vector2> ShieldOutline(float width, float height, int seg = 12)
        {
            float hw = width * 0.5f;
            float hh = height * 0.5f;
            float rc = width * 0.14f;
            float side = hh * 0.1f;
            var pts = new List<Vector2> { new Vector2(0f, -hh) };
            // 끝점 → 오른쪽 옆(2차 베지어)
            Vector2 p0 = new Vector2(0f, -hh), c0 = new Vector2(hw * 0.95f, -hh * 0.55f), p1 = new Vector2(hw, side);
            for (int i = 1; i <= seg; i++)
            {
                float t = (float)i / seg;
                pts.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * c0 + t * t * p1);
            }
            for (int i = 0; i <= 5; i++)
            {
                float th = Mathf.PI * 0.5f * i / 5;
                pts.Add(new Vector2(hw - rc + Mathf.Cos(th) * rc, hh - rc + Mathf.Sin(th) * rc));
            }
            for (int i = 0; i <= 5; i++)
            {
                float th = Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / 5;
                pts.Add(new Vector2(-hw + rc + Mathf.Cos(th) * rc, hh - rc + Mathf.Sin(th) * rc));
            }
            Vector2 q0 = new Vector2(-hw, side), cq = new Vector2(-hw * 0.95f, -hh * 0.55f);
            for (int i = 0; i < seg; i++)
            {
                float t = (float)i / seg;
                pts.Add((1 - t) * (1 - t) * q0 + 2 * (1 - t) * t * cq + t * t * p0);
            }
            return pts;
        }

        /// <summary>초승달(오목한 쪽이 +X). radius = 바깥 반지름.</summary>
        public static List<Vector2> CrescentOutline(float radius, float innerRatio = 0.84f, float shift = 0.42f, int seg = 36)
        {
            float R = radius;
            float r2 = radius * innerRatio;
            float d = radius * shift;
            float x = (R * R - r2 * r2 + d * d) / (2f * d);
            float y = Mathf.Sqrt(Mathf.Max(R * R - x * x, 1e-6f));
            float phi = Mathf.Atan2(y, x);
            float alpha = Mathf.Atan2(y, x - d);
            var pts = new List<Vector2>();
            for (int i = 0; i <= seg; i++)
            {
                float th = phi + (Mathf.PI * 2f - 2f * phi) * i / seg;
                pts.Add(new Vector2(Mathf.Cos(th) * R, Mathf.Sin(th) * R));
            }
            for (int i = 1; i < seg; i++)
            {
                float th = -alpha - (Mathf.PI * 2f - 2f * alpha) * i / seg;
                pts.Add(new Vector2(d + Mathf.Cos(th) * r2, Mathf.Sin(th) * r2));
            }
            return pts;
        }

        /// <summary>박쥐 실루엣(날개 아래는 가리비). 가로 width.</summary>
        public static List<Vector2> BatOutline(float width)
        {
            float s = width / 2f;
            var raw = new List<Vector2>
            {
                new Vector2(0f, -0.22f), new Vector2(0.12f, -0.12f), new Vector2(0.24f, -0.2f), new Vector2(0.36f, -0.1f),
                new Vector2(0.5f, -0.22f), new Vector2(0.62f, -0.08f), new Vector2(0.8f, -0.18f), new Vector2(1f, 0.12f),
                new Vector2(0.72f, 0.2f), new Vector2(0.42f, 0.1f), new Vector2(0.2f, 0.16f), new Vector2(0.14f, 0.32f),
                new Vector2(0.06f, 0.2f), new Vector2(-0.06f, 0.2f), new Vector2(-0.14f, 0.32f), new Vector2(-0.2f, 0.16f),
                new Vector2(-0.42f, 0.1f), new Vector2(-0.72f, 0.2f), new Vector2(-1f, 0.12f), new Vector2(-0.8f, -0.18f),
                new Vector2(-0.62f, -0.08f), new Vector2(-0.5f, -0.22f), new Vector2(-0.36f, -0.1f), new Vector2(-0.24f, -0.2f),
                new Vector2(-0.12f, -0.12f),
            };
            var pts = new List<Vector2>();
            foreach (Vector2 v in raw) pts.Add(v * s);
            return Smooth(pts, 1);
        }

        // ───────── 외곽선 도우미 ─────────

        /// <summary>닫힌 다각형 모서리 깎기(Chaikin).</summary>
        public static List<Vector2> Smooth(IList<Vector2> pts, int iterations)
        {
            var cur = new List<Vector2>(pts);
            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector2>(cur.Count * 2);
                for (int i = 0; i < cur.Count; i++)
                {
                    Vector2 p = cur[i];
                    Vector2 q = cur[(i + 1) % cur.Count];
                    next.Add(Vector2.Lerp(p, q, 0.25f));
                    next.Add(Vector2.Lerp(p, q, 0.75f));
                }
                cur = next;
            }
            return cur;
        }

        /// <summary>외곽선을 width×height 상자 가운데에 맞춘다.</summary>
        public static List<Vector2> FitBox(IList<Vector2> pts, float width, float height)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (Vector2 p in pts)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            Vector2 size = max - min;
            Vector2 c = (min + max) * 0.5f;
            var result = new List<Vector2>(pts.Count);
            foreach (Vector2 p in pts)
            {
                result.Add(new Vector2((p.x - c.x) / Mathf.Max(size.x, 1e-5f) * width, (p.y - c.y) / Mathf.Max(size.y, 1e-5f) * height));
            }
            return result;
        }

        private static List<Vector2> Clean(IList<Vector2> outline)
        {
            var pts = new List<Vector2>();
            foreach (Vector2 p in outline)
            {
                if (pts.Count == 0 || (p - pts[pts.Count - 1]).sqrMagnitude > 1e-10f) pts.Add(p);
            }
            while (pts.Count > 3 && (pts[0] - pts[pts.Count - 1]).sqrMagnitude <= 1e-10f) pts.RemoveAt(pts.Count - 1);
            if (SignedArea(pts) < 0f) pts.Reverse();
            return pts;
        }

        private static float SignedArea(IList<Vector2> pts)
        {
            float a = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i];
                Vector2 q = pts[(i + 1) % pts.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a * 0.5f;
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        /// <summary>반시계 다각형 귀 자르기. 막히면 남은 것은 부채꼴로 채운다. 판정 오차를 일정하게 하려고 크기를 1로 맞춰 계산한다.</summary>
        private static List<int> Triangulate(IList<Vector2> source)
        {
            var poly = FitUnit(source);
            var result = new List<int>();
            var idx = new List<int>(poly.Count);
            for (int i = 0; i < poly.Count; i++) idx.Add(i);
            int guard = poly.Count * poly.Count + 10;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                int cnt = idx.Count;
                for (int k = 0; k < cnt; k++)
                {
                    int a = idx[(k - 1 + cnt) % cnt], b = idx[k], c = idx[(k + 1) % cnt];
                    Vector2 pa = poly[a], pb = poly[b], pc = poly[c];
                    if (Cross(pb - pa, pc - pb) <= 1e-9f) continue;
                    bool inside = false;
                    for (int m = 0; m < cnt; m++)
                    {
                        int q = idx[m];
                        if (q == a || q == b || q == c) continue;
                        if (PointInTri(poly[q], pa, pb, pc)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(a); result.Add(b); result.Add(c);
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (clipped) continue;
                // 일직선 점은 빼고 다시
                bool removed = false;
                for (int k = 0; k < cnt; k++)
                {
                    Vector2 pa = poly[idx[(k - 1 + cnt) % cnt]], pb = poly[idx[k]], pc = poly[idx[(k + 1) % cnt]];
                    if (Mathf.Abs(Cross(pb - pa, pc - pb)) <= 1e-7f)
                    {
                        idx.RemoveAt(k);
                        removed = true;
                        break;
                    }
                }
                if (!removed) break;
            }
            for (int k = 1; k < idx.Count - 1; k++)
            {
                result.Add(idx[0]); result.Add(idx[k]); result.Add(idx[k + 1]);
            }
            return result;
        }

        private static List<Vector2> FitUnit(IList<Vector2> pts)
        {
            Vector2 min = pts[0], max = pts[0];
            foreach (Vector2 p in pts) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            float s = Mathf.Max(Mathf.Max(max.x - min.x, max.y - min.y), 1e-6f);
            var result = new List<Vector2>(pts.Count);
            foreach (Vector2 p in pts) result.Add((p - min) / s);
            return result;
        }

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, p - a) > 1e-9f && Cross(c - b, p - b) > 1e-9f && Cross(a - c, p - c) > 1e-9f;
        }
    }
}
