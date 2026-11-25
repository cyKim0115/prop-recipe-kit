namespace PropRecipe
{
    /// <summary>PerMaterial = 재질 키마다 서브메시. Palette = 서브메시 1개 + 팔레트 UV(u = (재질 번호 + 0.5) / 재질 수, v = 0.5).</summary>
    public enum PropBakeMode
    {
        PerMaterial,
        Palette,
    }
}
