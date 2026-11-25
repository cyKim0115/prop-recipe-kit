namespace PropRecipe
{
    /// <summary>JSON 숫자 원문. float로 바꿀 때 PropJsonFormat.ParseFloat를 쓰면 쓸 때 값과 비트까지 같다.</summary>
    public sealed class PropJsonNumber
    {
        public readonly string Raw;

        public PropJsonNumber(string raw)
        {
            Raw = raw;
        }

        public float ToFloat() => PropJsonFormat.ParseFloat(Raw);

        public int ToInt() => PropJsonFormat.ParseInt(Raw);

        public override string ToString() => Raw;
    }
}
