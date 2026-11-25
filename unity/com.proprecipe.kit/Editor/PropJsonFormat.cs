using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// JSON 숫자·문자열 서식. float은 다시 읽으면 비트까지 같은 값이 나오는 가장 짧은 문자열로 쓴다
    /// ("R" → "G9" → double "G17" 순으로 시험, -0은 "-0").
    /// </summary>
    public static class PropJsonFormat
    {
        private static readonly CultureInfo INV = CultureInfo.InvariantCulture;
        private static readonly float NEG_ZERO = BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0);

        public static string Float(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
            {
                Debug.LogWarning("[PropJsonFormat] NaN/Infinity를 0으로 씀");
                return "0";
            }
            int bits = Bits(v);
            if (bits == Bits(NEG_ZERO)) return "-0";
            string s = v.ToString("R", INV);
            if (Bits(ParseFloat(s)) == bits) return s;
            s = v.ToString("G9", INV);
            if (Bits(ParseFloat(s)) == bits) return s;
            return ((double)v).ToString("G17", INV);
        }

        public static string Int(int v) => v.ToString(INV);

        /// <summary>JSON 숫자 문자열 → float. "-0"은 음의 0으로 읽는다.</summary>
        public static float ParseFloat(string raw)
        {
            float v = float.Parse(raw, NumberStyles.Float, INV);
            if (v == 0f && raw.TrimStart().StartsWith("-", StringComparison.Ordinal)) return NEG_ZERO;
            return v;
        }

        public static int ParseInt(string raw)
        {
            if (int.TryParse(raw, NumberStyles.Integer, INV, out int i)) return i;
            return (int)Math.Round(double.Parse(raw, NumberStyles.Float, INV));
        }

        public static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", INV));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>[[x,y],...]</summary>
        public static string Points(IList<Vector2> pts)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < pts.Count; i++)
            {
                sb.Append(i == 0 ? "" : ",").Append('[').Append(Float(pts[i].x)).Append(',').Append(Float(pts[i].y)).Append(']');
            }
            return sb.Append(']').ToString();
        }

        public static Vector2[] Copy(IList<Vector2> pts)
        {
            var arr = new Vector2[pts.Count];
            for (int i = 0; i < arr.Length; i++) arr[i] = pts[i];
            return arr;
        }

        private static int Bits(float v) => BitConverter.ToInt32(BitConverter.GetBytes(v), 0);
    }
}
