using System.Collections.Generic;

namespace PropRecipe
{
    /// <summary>PropBaker.Bake 결과 요약.</summary>
    public sealed class PropBakeReport
    {
        public PropBakeMode Mode;
        /// <summary>LOD 적용 뒤, 묻힌 면 제거 전 삼각형 수.</summary>
        public int TrisBefore;
        /// <summary>묻힌 면 제거 뒤 삼각형 수.</summary>
        public int TrisAfter;
        public int Culled;
        public int Verts;
        public int Submeshes;
        public long Milliseconds;
        /// <summary>서브메시(PerMaterial) 또는 팔레트 칸(Palette) 순서의 재질 키.</summary>
        public readonly List<string> MaterialOrder = new List<string>();

        public override string ToString()
        {
            return $"{Mode}: tris {TrisBefore} -> {TrisAfter} (culled {Culled}), verts {Verts}, submeshes {Submeshes}, {Milliseconds} ms, materials [{string.Join(",", MaterialOrder)}]";
        }
    }
}
