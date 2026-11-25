using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>둥근 상자. gen {"type":"roundbox","size":[x,y,z],"radius","seg"}.</summary>
    public sealed class PropGenRoundBox : PropGen
    {
        public Vector3 Size;
        public float Radius;
        public int Seg;

        public PropGenRoundBox(Vector3 size, float radius, int seg)
        {
            Size = size;
            Radius = radius;
            Seg = seg;
        }

        public override string Type => "roundbox";

        public override Mesh Build(PropLod lod) => PropMeshKit.RoundBox(Size, Radius, Seg, lod);

        protected override void WriteFields(StringBuilder sb)
        {
            sb.Append(",\"size\":[").Append(PropJsonFormat.Float(Size.x)).Append(',').Append(PropJsonFormat.Float(Size.y))
                .Append(',').Append(PropJsonFormat.Float(Size.z)).Append(']');
            sb.Append(",\"radius\":").Append(PropJsonFormat.Float(Radius));
            sb.Append(",\"seg\":").Append(PropJsonFormat.Int(Seg));
        }
    }
}
