using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PropRecipe
{
    /// <summary>
    /// 부품을 한 메시로 굽는다: 부품 메시 생성(PropLod) → 소품 좌표로 변환 → 다른 닫힌 부품 안에 묻힌 삼각형 제거
    /// → 재질별 서브메시 또는 팔레트 1개 서브메시 → Mesh.Optimize. 수치 연산은 원본 파일럿과 같게 유지한다.
    /// </summary>
    public static class PropBaker
    {
        private const int GRID = 16;
        private const float KEY_SCALE = 20000f;

        private sealed class PartTris
        {
            public string Mat;
            public List<Vector3> V = new List<Vector3>();
            public List<Vector3> N = new List<Vector3>();
            public List<int> T = new List<int>();
            public Bounds Bounds;
            public bool Closed;
            public List<int>[] Grid;
        }

        /// <summary>
        /// parts를 굽는다. lod = null이면 감량 없음. materialOrder = null이면 부품 등장 순서(목록에 없는 키는 뒤에 붙는다).
        /// 기본은 그룹 밖 부품(Group == -1)만 굽는다. 부품이 없으면 null.
        /// </summary>
        public static Mesh Bake(IReadOnlyList<PropPart> parts, PropLod lod, PropBakeMode mode, IList<string> materialOrder,
            out PropBakeReport report, string meshName = null, bool includeGroups = false)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            report = new PropBakeReport { Mode = mode };
            if (lod == null) lod = new PropLod();
            var made = new Dictionary<PropGen, Mesh>();
            try
            {
                var ps = WorldParts(parts, lod, made, includeGroups);
                if (ps.Count == 0)
                {
                    Debug.LogWarning("[PropBaker] 구울 부품이 없음");
                    return null;
                }
                int before = ps.Sum(p => p.T.Count / 3);
                int kept = CullHidden(ps, out int removed);
                var order = new List<string>();
                if (materialOrder != null) foreach (string k in materialOrder) if (!order.Contains(k)) order.Add(k);
                foreach (var p in ps)
                {
                    if (order.Contains(p.Mat)) continue;
                    if (materialOrder != null) Debug.LogWarning("[PropBaker] materialOrder에 없는 재질 " + p.Mat + "을(를) 뒤에 붙임");
                    order.Add(p.Mat);
                }
                bool palette = mode == PropBakeMode.Palette;
                var verts = new List<Vector3>();
                var norms = new List<Vector3>();
                var uvs = new List<Vector2>();
                var subs = new List<List<int>>();
                foreach (string _ in palette ? new[] { "all" } : order.ToArray()) subs.Add(new List<int>());
                foreach (var p in ps)
                {
                    int mi = order.IndexOf(p.Mat);
                    var map = new Dictionary<int, int>();
                    var dst = subs[palette ? 0 : mi];
                    foreach (int src in p.T)
                    {
                        if (!map.TryGetValue(src, out int di))
                        {
                            di = verts.Count;
                            map[src] = di;
                            verts.Add(p.V[src]);
                            norms.Add(p.N[src]);
                            uvs.Add(new Vector2((mi + 0.5f) / order.Count, 0.5f));
                        }
                        dst.Add(di);
                    }
                }
                var mesh = new Mesh { name = string.IsNullOrEmpty(meshName) ? "PropMesh" : meshName };
                mesh.indexFormat = verts.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(verts);
                mesh.SetNormals(norms);
                if (palette) mesh.SetUVs(0, uvs);
                mesh.subMeshCount = subs.Count;
                for (int s = 0; s < subs.Count; s++) mesh.SetTriangles(subs[s], s);
                mesh.RecalculateBounds();
                mesh.Optimize();

                report.TrisBefore = before;
                report.TrisAfter = kept;
                report.Culled = removed;
                report.Verts = verts.Count;
                report.Submeshes = subs.Count;
                report.MaterialOrder.AddRange(order);
                report.Milliseconds = sw.ElapsedMilliseconds;
                return mesh;
            }
            finally
            {
                foreach (Mesh m in made.Values) if (m != null) Object.DestroyImmediate(m);
            }
        }

        /// <summary>사양의 부품을 굽는다(이름 = "PM_" + id).</summary>
        public static Mesh Bake(PropSpec spec, PropLod lod, PropBakeMode mode, out PropBakeReport report, IList<string> materialOrder = null)
        {
            return Bake(spec.Parts, lod, mode, materialOrder, out report, "PM_" + spec.Id);
        }

        /// <summary>
        /// 묻힌 면 제거 없이 그룹 하나(-1 = 본체)의 부품을 재질별 서브메시로 합친다(원본 프리팹 빌더 방식, CombineMeshes).
        /// 정점은 pivot 기준. materialOrder = 서브메시 순서(부품 등장 순서). 부품이 없으면 null.
        /// </summary>
        public static Mesh Combine(IReadOnlyList<PropPart> parts, PropLod lod, int group, Vector3 pivot, out List<string> materialOrder,
            string meshName = null)
        {
            if (lod == null) lod = new PropLod();
            Matrix4x4 toPivot = Matrix4x4.Translate(-pivot);
            var order = new List<string>();
            var byMat = new Dictionary<string, List<CombineInstance>>();
            var made = new Dictionary<PropGen, Mesh>();
            var temps = new List<Mesh>();
            try
            {
                foreach (var part in parts)
                {
                    if (part.Group != group || part.Gen == null) continue;
                    if (!made.TryGetValue(part.Gen, out Mesh mesh) || mesh == null)
                    {
                        mesh = part.Gen.Build(lod);
                        made[part.Gen] = mesh;
                    }
                    if (!byMat.TryGetValue(part.Mat, out var list))
                    {
                        list = new List<CombineInstance>();
                        byMat[part.Mat] = list;
                        order.Add(part.Mat);
                    }
                    list.Add(new CombineInstance { mesh = mesh, transform = toPivot * part.Matrix });
                }
                materialOrder = order;
                if (order.Count == 0) return null;
                var subMeshes = new List<CombineInstance>();
                foreach (string key in order)
                {
                    var m = new Mesh { indexFormat = IndexFormat.UInt32 };
                    m.CombineMeshes(byMat[key].ToArray(), true, true);
                    temps.Add(m);
                    subMeshes.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
                }
                var combined = new Mesh { name = string.IsNullOrEmpty(meshName) ? "PropMesh" : meshName, indexFormat = IndexFormat.UInt32 };
                combined.CombineMeshes(subMeshes.ToArray(), false, true);
                combined.RecalculateBounds();
                return combined;
            }
            finally
            {
                foreach (Mesh t in temps) if (t != null) Object.DestroyImmediate(t);
                foreach (Mesh m in made.Values) if (m != null) Object.DestroyImmediate(m);
            }
        }

        private static List<PartTris> WorldParts(IReadOnlyList<PropPart> parts, PropLod lod, Dictionary<PropGen, Mesh> made, bool includeGroups)
        {
            var list = new List<PartTris>();
            foreach (var part in parts)
            {
                if (!includeGroups && part.Group != -1) continue;
                if (part.Gen == null)
                {
                    Debug.LogWarning("[PropBaker] gen 없는 부품은 건너뜀");
                    continue;
                }
                if (!made.TryGetValue(part.Gen, out Mesh mesh) || mesh == null)
                {
                    mesh = part.Gen.Build(lod);
                    made[part.Gen] = mesh;
                }
                var pt = new PartTris { Mat = part.Mat };
                Vector3[] v = mesh.vertices;
                Vector3[] n = mesh.normals;
                Matrix4x4 nm = part.Matrix.inverse.transpose;
                for (int i = 0; i < v.Length; i++)
                {
                    pt.V.Add(part.Matrix.MultiplyPoint3x4(v[i]));
                    pt.N.Add(n.Length == v.Length ? nm.MultiplyVector(n[i]).normalized : Vector3.up);
                }
                pt.T.AddRange(mesh.triangles);
                if (part.Matrix.determinant < 0f)
                {
                    for (int i = 0; i < pt.T.Count; i += 3) (pt.T[i + 1], pt.T[i + 2]) = (pt.T[i + 2], pt.T[i + 1]);
                }
                pt.Bounds = new Bounds(pt.V[0], Vector3.zero);
                foreach (var q in pt.V) pt.Bounds.Encapsulate(q);
                pt.Closed = IsClosed(pt);
                list.Add(pt);
            }
            return list;
        }

        private static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * KEY_SCALE), Mathf.RoundToInt(p.y * KEY_SCALE), Mathf.RoundToInt(p.z * KEY_SCALE));

        /// <summary>모든 변을 짝수 번 공유하면(양자화 키 기준) 닫힌 부품으로 본다.</summary>
        private static bool IsClosed(PartTris pt)
        {
            var count = new Dictionary<(Vector3Int, Vector3Int), int>();
            for (int i = 0; i < pt.T.Count; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    Vector3Int a = Key(pt.V[pt.T[i + e]]), b = Key(pt.V[pt.T[i + (e + 1) % 3]]);
                    if (a == b) continue;
                    var k = Compare(a, b) < 0 ? (a, b) : (b, a);
                    count[k] = count.TryGetValue(k, out int c) ? c + 1 : 1;
                }
            }
            return count.Count > 0 && count.Values.All(c => c % 2 == 0);
        }

        private static int Compare(Vector3Int a, Vector3Int b)
        {
            if (a.x != b.x) return a.x.CompareTo(b.x);
            if (a.y != b.y) return a.y.CompareTo(b.y);
            return a.z.CompareTo(b.z);
        }

        private static void BuildGrid(PartTris pt)
        {
            pt.Grid = new List<int>[GRID * GRID];
            for (int i = 0; i < pt.Grid.Length; i++) pt.Grid[i] = new List<int>();
            Vector3 mn = pt.Bounds.min, sz = pt.Bounds.size;
            for (int i = 0; i < pt.T.Count; i += 3)
            {
                Vector3 a = pt.V[pt.T[i]], b = pt.V[pt.T[i + 1]], c = pt.V[pt.T[i + 2]];
                int x0 = Cell(Mathf.Min(a.x, Mathf.Min(b.x, c.x)), mn.x, sz.x), x1 = Cell(Mathf.Max(a.x, Mathf.Max(b.x, c.x)), mn.x, sz.x);
                int z0 = Cell(Mathf.Min(a.z, Mathf.Min(b.z, c.z)), mn.z, sz.z), z1 = Cell(Mathf.Max(a.z, Mathf.Max(b.z, c.z)), mn.z, sz.z);
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                        pt.Grid[x * GRID + z].Add(i);
            }
        }

        private static int Cell(float v, float mn, float sz) => Mathf.Clamp((int)((v - mn) / Mathf.Max(sz, 1e-6f) * GRID), 0, GRID - 1);

        /// <summary>+Y 방향 반직선 교차 횟수 홀짝으로 닫힌 부품 안쪽인지 본다.</summary>
        private static bool Inside(PartTris pt, Vector3 p)
        {
            if (!pt.Bounds.Contains(p)) return false;
            if (pt.Grid == null) BuildGrid(pt);
            var cell = pt.Grid[Cell(p.x, pt.Bounds.min.x, pt.Bounds.size.x) * GRID + Cell(p.z, pt.Bounds.min.z, pt.Bounds.size.z)];
            int hits = 0;
            foreach (int i in cell)
            {
                Vector3 a = pt.V[pt.T[i]], b = pt.V[pt.T[i + 1]], c = pt.V[pt.T[i + 2]];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-12f) continue;
                float l1 = ((b.z - c.z) * (p.x - c.x) + (c.x - b.x) * (p.z - c.z)) / d;
                float l2 = ((c.z - a.z) * (p.x - c.x) + (a.x - c.x) * (p.z - c.z)) / d;
                float l3 = 1f - l1 - l2;
                if (l1 < 0f || l2 < 0f || l3 < 0f) continue;
                float y = l1 * a.y + l2 * b.y + l3 * c.y;
                if (y > p.y) hits++;
            }
            return (hits & 1) == 1;
        }

        /// <summary>다른 닫힌 부품 안에 완전히 묻힌 삼각형을 뺀다. 남은 삼각형 수를 돌려준다.</summary>
        private static int CullHidden(List<PartTris> ps, out int removed)
        {
            removed = 0;
            int kept = 0;
            var result = new List<int>[ps.Count];
            for (int i = 0; i < ps.Count; i++)
            {
                var keep = new List<int>();
                for (int k = 0; k < ps[i].T.Count; k += 3)
                {
                    Vector3 a = ps[i].V[ps[i].T[k]], b = ps[i].V[ps[i].T[k + 1]], c = ps[i].V[ps[i].T[k + 2]];
                    Vector3 g = (a + b + c) / 3f;
                    Vector3[] probes = { g, Vector3.Lerp(a, g, 0.03f), Vector3.Lerp(b, g, 0.03f), Vector3.Lerp(c, g, 0.03f) };
                    bool hidden = false;
                    for (int j = 0; j < ps.Count && !hidden; j++)
                    {
                        if (j == i || !ps[j].Closed) continue;
                        hidden = probes.All(q => Inside(ps[j], q));
                    }
                    if (hidden) removed++;
                    else { keep.Add(ps[i].T[k]); keep.Add(ps[i].T[k + 1]); keep.Add(ps[i].T[k + 2]); }
                }
                result[i] = keep;
                kept += keep.Count / 3;
            }
            // 다른 부품의 안쪽 판정이 끝난 뒤에 바꾼다(격자가 원래 삼각형 번호를 가리킨다).
            for (int i = 0; i < ps.Count; i++) ps[i].T = result[i];
            return kept;
        }
    }
}
