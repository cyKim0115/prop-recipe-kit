using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// 부품 하나. Mat = 재질 키, Group = PropParts.Groups 번호(-1 = 본체), Matrix = 부품 로컬 → 소품 로컬 변환,
    /// Gen = 메시 생성 설명(굽는 시점에 PropLod로 만든다).
    /// </summary>
    public struct PropPart
    {
        public string Mat;
        public int Group;
        public Matrix4x4 Matrix;
        public PropGen Gen;
    }
}
