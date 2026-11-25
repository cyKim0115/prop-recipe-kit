using UnityEngine;

namespace PropRecipe
{
    /// <summary>따로 움직이는 자식(문·뚜껑·서랍). Pivot = 소품 로컬 경첩 위치.</summary>
    public struct PropGroup
    {
        public string Name;
        public Vector3 Pivot;
    }
}
