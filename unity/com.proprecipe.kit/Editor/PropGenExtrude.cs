using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>닫힌 외곽선(XY)을 Z로 돌출한 판. gen {"type":"extrude","outline":[[x,y]...],"depth","bevel","arc"}.</summary>
    public sealed class PropGenExtrude : PropGen
    {
        public Vector2[] Outline;
        public float Depth;
        public float Bevel;
        public int Arc;

        public PropGenExtrude(IList<Vector2> outline, float depth, float bevel, int arc = 3)
        {
            Outline = PropJsonFormat.Copy(outline);
            Depth = depth;
            Bevel = bevel;
            Arc = arc;
        }

        public override string Type => "extrude";

        public override Mesh Build(PropLod lod) => PropMeshKit.Extrude(Outline, Depth, Bevel, Arc, lod);

        protected override void WriteFields(StringBuilder sb)
        {
            sb.Append(",\"outline\":").Append(PropJsonFormat.Points(Outline));
            sb.Append(",\"depth\":").Append(PropJsonFormat.Float(Depth));
            sb.Append(",\"bevel\":").Append(PropJsonFormat.Float(Bevel));
            sb.Append(",\"arc\":").Append(PropJsonFormat.Int(Arc));
        }
    }
}
