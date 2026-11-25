using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// 레시피가 부품을 쌓는 그릇(선언형 DSL). 좌표는 소품 로컬(피벗 = 바닥 가운데 권장, 정면 +Z).
    /// 부품은 생성 설명(PropGen) + 변환 행렬만 갖고, 메시는 PropBaker가 굽는 시점에 PropLod로 만든다.
    /// </summary>
    public sealed class PropParts
    {
        private static readonly CultureInfo INV = CultureInfo.InvariantCulture;

        public readonly List<PropPart> Parts = new List<PropPart>();
        public readonly List<PropPoint> Points = new List<PropPoint>();
        public readonly List<PropGroup> Groups = new List<PropGroup>();

        /// <summary>
        /// true(기본)면 원본 프로토타입의 메시 캐시와 같게, 반올림 키(소수 셋째 자리 등)가 같은 부품은 처음 부품의 생성 설명을 함께 쓴다.
        /// 원본과 비트 단위로 같은 메시가 필요할 때 켜 둔다.
        /// </summary>
        public bool ShareByKey = true;

        private readonly Dictionary<string, PropGen> _shared = new Dictionary<string, PropGen>();
        private int _group = -1;

        public bool IsInGroup => _group >= 0;

        private void Add(string mat, PropGen gen, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            Parts.Add(new PropPart { Gen = gen, Matrix = Matrix4x4.TRS(pos, Quaternion.Euler(euler), scale), Mat = mat, Group = _group });
        }

        private PropGen Shared(string key, Func<PropGen> make)
        {
            if (!ShareByKey) return make();
            if (!_shared.TryGetValue(key, out PropGen gen) || gen == null)
            {
                gen = make();
                _shared[key] = gen;
            }
            return gen;
        }

        private static string K(string format, params object[] args) => string.Format(INV, format, args);

        /// <summary>
        /// 이후 부품을 이름 붙은 자식으로 모은다(좌표는 계속 소품 로컬). EndGroup으로 닫는다.
        /// PropBaker는 기본적으로 그룹 부품을 굽지 않는다(includeGroups).
        /// </summary>
        public PropParts BeginGroup(string name, Vector3 pivot)
        {
            Groups.Add(new PropGroup { Name = name, Pivot = pivot });
            _group = Groups.Count - 1;
            return this;
        }

        public PropParts EndGroup()
        {
            _group = -1;
            return this;
        }

        /// <summary>둥근 상자. center = 가운데. 반지름이 작으면 모서리 분할을 줄인다(정점 수).</summary>
        public PropParts Box(string mat, Vector3 center, Vector3 size, float r = 0.08f, Vector3 euler = default)
        {
            int seg = r <= 0.025f ? 2 : 4;
            string key = K("box{0:F3},{1:F3},{2:F3},{3:F3},{4}", size.x, size.y, size.z, r, seg);
            Add(mat, Shared(key, () => new PropGenRoundBox(size, r, seg)), center, euler, Vector3.one);
            return this;
        }

        // ───────── 판. 외곽선은 XY, 두께는 Z, center = 판 가운데. euler 기본이면 앞면이 +Z ─────────

        /// <summary>임의 외곽선 판. outlineKey로 생성 설명을 공유한다(같은 키 = 같은 외곽선).</summary>
        public PropParts Plate(string mat, string outlineKey, IList<Vector2> outline, Vector3 center, float depth,
            Vector3 euler = default, Vector3 scale = default, float bevel = 0.025f)
        {
            string key = K("plate{0}|{1:F3},{2:F3}", outlineKey, depth, bevel);
            Add(mat, Shared(key, () => new PropGenExtrude(outline, depth, bevel)), center, euler, scale == default ? Vector3.one : scale);
            return this;
        }

        public PropParts Star(string mat, Vector3 center, float rOuter, float depth, Vector3 euler = default, int points = 5,
            float innerRatio = 0.5f, int smooth = 2, float bevel = 0.02f)
        {
            string k = K("star{0},{1:F3},{2:F3},{3}", points, rOuter, innerRatio, smooth);
            return Plate(mat, k, PropMeshKit.StarOutline(points, rOuter, rOuter * innerRatio, smooth), center, depth, euler, default, bevel);
        }

        public PropParts Heart(string mat, Vector3 center, float size, float depth, Vector3 euler = default, float bevel = 0.02f)
        {
            return Plate(mat, K("heart{0:F3}", size), PropMeshKit.HeartOutline(size), center, depth, euler, default, bevel);
        }

        public PropParts FlowerPlate(string mat, Vector3 center, float radius, float depth, int petals = 5, Vector3 euler = default,
            float innerRatio = 0.62f, float bevel = 0.02f)
        {
            string k = K("flower{0},{1:F3},{2:F3}", petals, radius, innerRatio);
            return Plate(mat, k, PropMeshKit.FlowerOutline(petals, radius, radius * innerRatio), center, depth, euler, default, bevel);
        }

        public PropParts Cloud(string mat, Vector3 center, float width, float height, float depth, Vector3 euler = default,
            int bumps = 7, float bevel = 0.025f)
        {
            string k = K("cloud{0:F3},{1:F3},{2}", width, height, bumps);
            return Plate(mat, k, PropMeshKit.CloudOutline(width, height, bumps), center, depth, euler, default, bevel);
        }

        /// <summary>아치 판. pos = 아래 변 가운데(두께 가운데).</summary>
        public PropParts Arch(string mat, Vector3 pos, float width, float height, float depth, Vector3 euler = default, float bevel = 0.03f)
        {
            string k = K("arch{0:F3},{1:F3}", width, height);
            return Plate(mat, k, PropMeshKit.ArchOutline(width, height), pos, depth, euler, default, bevel);
        }

        /// <summary>위쪽 반고리 판. pos = 원 가운데(아래 끝이 pos 높이).</summary>
        public PropParts HalfRing(string mat, Vector3 pos, float rOuter, float rInner, float depth, Vector3 euler = default, float bevel = 0.03f)
        {
            string k = K("halfring{0:F3},{1:F3}", rOuter, rInner);
            return Plate(mat, k, PropMeshKit.HalfRingOutline(rOuter, rInner), pos, depth, euler, default, bevel);
        }

        public PropParts RoundRect(string mat, Vector3 center, float width, float height, float radius, float depth,
            Vector3 euler = default, float bevel = 0.025f)
        {
            string k = K("rrect{0:F3},{1:F3},{2:F3}", width, height, radius);
            return Plate(mat, k, PropMeshKit.RoundRectOutline(width, height, radius), center, depth, euler, default, bevel);
        }

        public PropParts Shield(string mat, Vector3 center, float width, float height, float depth, Vector3 euler = default)
        {
            string k = K("shield{0:F3},{1:F3}", width, height);
            return Plate(mat, k, PropMeshKit.ShieldOutline(width, height), center, depth, euler, default, 0.025f);
        }

        public PropParts Crescent(string mat, Vector3 center, float radius, float depth, Vector3 euler = default, float innerRatio = 0.84f,
            float shift = 0.42f)
        {
            string k = K("crescent{0:F3},{1:F3},{2:F3}", radius, innerRatio, shift);
            return Plate(mat, k, PropMeshKit.CrescentOutline(radius, innerRatio, shift), center, depth, euler, default, 0.03f);
        }

        public PropParts Scallop(string mat, Vector3 center, float width, float height, int count, float depth, Vector3 euler = default)
        {
            string k = K("scallop{0:F3},{1:F3},{2}", width, height, count);
            return Plate(mat, k, PropMeshKit.ScallopStrip(width, height, count), center, depth, euler, default, 0.012f);
        }

        public PropParts Wave(string mat, Vector3 center, float width, float height, int waves, float depth, Vector3 euler = default)
        {
            string k = K("wave{0:F3},{1:F3},{2}", width, height, waves);
            return Plate(mat, k, PropMeshKit.WaveStrip(width, height, waves), center, depth, euler, default, 0.012f);
        }

        public PropParts Polygon(string mat, Vector3 center, int sides, float radius, float depth, Vector3 euler = default, int smooth = 1)
        {
            string k = K("poly{0},{1:F3},{2}", sides, radius, smooth);
            return Plate(mat, k, PropMeshKit.PolygonOutline(sides, radius, smooth), center, depth, euler, default, 0.015f);
        }

        public PropParts Bat(string mat, Vector3 center, float width, float depth, Vector3 euler = default)
        {
            return Plate(mat, K("bat{0:F3}", width), PropMeshKit.BatOutline(width), center, depth, euler, default, 0.012f);
        }

        /// <summary>도넛. center = 가운데, 축 = 로컬 Y(euler (90,0,0)이면 +Z를 본다). arcDeg &lt; 360이면 열린 고리.</summary>
        public PropParts Ring(string mat, Vector3 center, float major, float minor, Vector3 euler = default, float arcDeg = 360f,
            int segMajor = 32, int segMinor = 10)
        {
            string k = K("torus{0:F3},{1:F3},{2:F1},{3},{4}", major, minor, arcDeg, segMajor, segMinor);
            Add(mat, Shared(k, () => new PropGenTorus(major, minor, segMajor, segMinor, arcDeg)), center, euler, Vector3.one);
            return this;
        }

        /// <summary>임의 단면 회전체. profile = (반지름, 높이) 아래→위, pos = 축 위 y=0. profileKey로 생성 설명을 공유한다.</summary>
        public PropParts Lathe(string mat, string profileKey, IList<Vector2> profile, Vector3 pos, Vector3 euler = default,
            Vector3 scale = default, int segments = 32, int lobes = 0, float amp = 0f)
        {
            string k = K("lathe{0}|{1},{2},{3:F3}", profileKey, segments, lobes, amp);
            Add(mat, Shared(k, () => new PropGenLathe(profile, segments, lobes, amp, false)), pos, euler, scale == default ? Vector3.one : scale);
            return this;
        }

        /// <summary>둥근 원기둥/원뿔대. pos = 아래 면 가운데, 축 = 로컬 Y(euler로 돌림).</summary>
        public PropParts Cyl(string mat, Vector3 pos, float radius, float height, float bevel = 0.05f, float rTop = -1f,
            Vector3 euler = default, int lobes = 0, float amp = 0f, int segments = 32, bool sharp = false)
        {
            if (rTop < 0f) rTop = radius;
            float top = rTop;
            string key = K("cyl{0:F3},{1:F3},{2:F3},{3:F3},{4},{5:F3},{6},{7}", radius, rTop, height, bevel, lobes, amp, segments, sharp);
            Add(mat, Shared(key, () => new PropGenLathe(PropMeshKit.RoundCylProfile(radius, top, height, bevel), segments, lobes, amp, sharp)),
                pos, euler, Vector3.one);
            return this;
        }

        /// <summary>타원체. center = 가운데, size = 지름(x,y,z).</summary>
        public PropParts Ball(string mat, Vector3 center, Vector3 size, Vector3 euler = default, int lobes = 0, float amp = 0f)
        {
            string key = "ball" + lobes.ToString(INV) + "," + amp.ToString("F3", INV);
            Add(mat, Shared(key, () => new PropGenLathe(PropMeshKit.SphereProfile(), 28, lobes, amp, false)), center, euler, size);
            return this;
        }

        public PropParts Ball(string mat, Vector3 center, float diameter) => Ball(mat, center, Vector3.one * diameter);

        /// <summary>돔(반구). pos = 바닥 가운데, size = (지름, 높이, 지름).</summary>
        public PropParts Dome(string mat, Vector3 pos, Vector3 size, Vector3 euler = default, int lobes = 0, float amp = 0f)
        {
            string key = "dome" + lobes.ToString(INV) + "," + amp.ToString("F3", INV);
            Add(mat, Shared(key, () => new PropGenLathe(PropMeshKit.DomeProfile(), 28, lobes, amp, false)), pos, euler,
                new Vector3(size.x * 0.5f, size.y, size.z * 0.5f));
            return this;
        }

        /// <summary>임의 생성 설명을 그대로 넣는다(공유 없음).</summary>
        public PropParts Custom(string mat, PropGen gen, Matrix4x4 matrix)
        {
            Parts.Add(new PropPart { Gen = gen, Matrix = matrix, Mat = mat, Group = _group });
            return this;
        }

        /// <summary>상호작용 지점. pos = 캐릭터 루트 위치, yaw = 바라볼 방향(0 = +Z).</summary>
        public PropParts Point(string name, Vector3 pos, float yaw = 0f)
        {
            Points.Add(new PropPoint { Name = name, Pos = pos, Yaw = yaw });
            return this;
        }

        /// <summary>Point의 원본 이름(레시피 이식 호환).</summary>
        public PropParts IP(string name, Vector3 pos, float yaw = 0f) => Point(name, pos, yaw);

        /// <summary>사양으로 묶는다. palette = 재질 키 → 색(없으면 빈 팔레트, JSON에는 흰색으로 쓴다).</summary>
        public PropSpec ToSpec(string id, IReadOnlyDictionary<string, PropPaletteEntry> palette = null)
        {
            if (IsInGroup) Debug.LogWarning("[PropParts] EndGroup 누락 (" + id + ")");
            var spec = new PropSpec { Id = id };
            spec.Parts.AddRange(Parts);
            spec.Groups.AddRange(Groups);
            spec.Points.AddRange(Points);
            if (palette != null) foreach (var kv in palette) spec.Palette[kv.Key] = kv.Value;
            return spec;
        }
    }
}
