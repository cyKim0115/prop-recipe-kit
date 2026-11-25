using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// 부품 메시 생성 설명(종류 + 인자). 굽는 시점에 PropLod를 받아 메시를 만든다.
    /// 사양 JSON의 "gen" 객체와 1:1로 대응한다.
    /// </summary>
    public abstract class PropGen
    {
        /// <summary>"roundbox" | "extrude" | "utorus" | "lathe".</summary>
        public abstract string Type { get; }

        /// <summary>메시를 새로 만든다. 호출한 쪽이 DestroyImmediate 한다. lod = null이면 감량 없음.</summary>
        public abstract Mesh Build(PropLod lod);

        /// <summary>"gen" JSON 객체를 쓴다({"type":..., ...}).</summary>
        public void WriteJson(StringBuilder sb)
        {
            sb.Append("{\"type\":").Append(PropJsonFormat.Str(Type));
            WriteFields(sb);
            sb.Append('}');
        }

        /// <summary>type 뒤의 필드를 ,"key":value 형태로 붙인다.</summary>
        protected abstract void WriteFields(StringBuilder sb);
    }
}
