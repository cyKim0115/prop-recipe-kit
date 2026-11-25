using UnityEngine;

namespace PropRecipe
{
    /// <summary>상호작용 지점. Pos = 소품 로컬 위치, Yaw = 바라볼 방향(0 = +Z, 도).</summary>
    public struct PropPoint
    {
        public string Name;
        public Vector3 Pos;
        public float Yaw;
    }
}
