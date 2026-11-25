using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>회전체. gen {"type":"lathe","profile":[[r,y]...],"segments","lobes","amp","sharp"}.</summary>
    public sealed class PropGenLathe : PropGen
    {
        public Vector2[] Profile;
        public int Segments;
        public int Lobes;
        public float Amp;
        public bool Sharp;

        public PropGenLathe(IList<Vector2> profile, int segments = 32, int lobes = 0, float amp = 0f, bool sharp = false)
        {
            Profile = PropJsonFormat.Copy(profile);
            Segments = segments;
            Lobes = lobes;
            Amp = amp;
            Sharp = sharp;
        }

        public override string Type => "lathe";

        public override Mesh Build(PropLod lod) => PropMeshKit.Lathe(Profile, Segments, Lobes, Amp, Sharp, lod);

        protected override void WriteFields(StringBuilder sb)
        {
            sb.Append(",\"profile\":").Append(PropJsonFormat.Points(Profile));
            sb.Append(",\"segments\":").Append(PropJsonFormat.Int(Segments));
            sb.Append(",\"lobes\":").Append(PropJsonFormat.Int(Lobes));
            sb.Append(",\"amp\":").Append(PropJsonFormat.Float(Amp));
            sb.Append(",\"sharp\":").Append(Sharp ? "true" : "false");
        }
    }
}
