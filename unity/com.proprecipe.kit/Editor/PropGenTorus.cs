using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>도넛(축 = 로컬 Y). gen {"type":"utorus","major","minor","seg_major","seg_minor","arc"(도)}.</summary>
    public sealed class PropGenTorus : PropGen
    {
        public float Major;
        public float Minor;
        public int SegMajor;
        public int SegMinor;
        public float ArcDeg;

        public PropGenTorus(float major, float minor, int segMajor = 32, int segMinor = 10, float arcDeg = 360f)
        {
            Major = major;
            Minor = minor;
            SegMajor = segMajor;
            SegMinor = segMinor;
            ArcDeg = arcDeg;
        }

        public override string Type => "utorus";

        public override Mesh Build(PropLod lod) => PropMeshKit.Torus(Major, Minor, SegMajor, SegMinor, ArcDeg, lod);

        protected override void WriteFields(StringBuilder sb)
        {
            sb.Append(",\"major\":").Append(PropJsonFormat.Float(Major));
            sb.Append(",\"minor\":").Append(PropJsonFormat.Float(Minor));
            sb.Append(",\"seg_major\":").Append(PropJsonFormat.Int(SegMajor));
            sb.Append(",\"seg_minor\":").Append(PropJsonFormat.Int(SegMinor));
            sb.Append(",\"arc\":").Append(PropJsonFormat.Float(ArcDeg));
        }
    }
}
