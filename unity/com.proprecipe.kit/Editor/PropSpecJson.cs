using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PropRecipe
{
    /// <summary>
    /// 사양 JSON 쓰기·읽기. 형식:
    /// {"id","frame":"unity","parts":[{"mat","group","matrix":[16, 행 우선],"gen":{...}}],"palette":{"key":{"hex":"#RRGGBB","glow":0}}}
    /// (+ 있으면 "groups":[{"name","pivot":[x,y,z]}], "points":[{"name","pos":[x,y,z],"yaw"}]).
    /// float은 다시 읽으면 같은 비트가 나오게 쓴다.
    /// </summary>
    public static class PropSpecJson
    {
        // ───────── 쓰기 ─────────

        public static string ToJson(PropSpec spec)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":").Append(PropJsonFormat.Str(spec.Id ?? "")).Append(",\"frame\":\"unity\",\"parts\":[");
            int n = 0;
            foreach (PropPart part in spec.Parts)
            {
                if (part.Gen == null)
                {
                    Debug.LogWarning("[PropSpecJson] gen 없는 부품은 건너뜀 (" + spec.Id + ")");
                    continue;
                }
                Matrix4x4 m = part.Matrix;
                sb.Append(n++ == 0 ? "" : ",").Append("{\"mat\":").Append(PropJsonFormat.Str(part.Mat)).Append(",\"group\":")
                    .Append(PropJsonFormat.Int(part.Group)).Append(",\"matrix\":[");
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        sb.Append(r + c == 0 ? "" : ",").Append(PropJsonFormat.Float(m[r, c]));
                sb.Append("],\"gen\":");
                part.Gen.WriteJson(sb);
                sb.Append('}');
            }
            sb.Append("],\"palette\":{");
            List<string> used = spec.UsedMaterials();
            for (int i = 0; i < used.Count; i++)
            {
                PropPaletteEntry e = spec.Palette.TryGetValue(used[i], out PropPaletteEntry pe) ? pe : new PropPaletteEntry(Color.white, 0f);
                sb.Append(i == 0 ? "" : ",").Append(PropJsonFormat.Str(used[i])).Append(":{\"hex\":\"#").Append(ColorUtility.ToHtmlStringRGB(e.Color))
                    .Append("\",\"glow\":").Append(PropJsonFormat.Float(e.Glow)).Append('}');
            }
            sb.Append('}');
            if (spec.Groups.Count > 0)
            {
                sb.Append(",\"groups\":[");
                for (int i = 0; i < spec.Groups.Count; i++)
                {
                    PropGroup g = spec.Groups[i];
                    sb.Append(i == 0 ? "" : ",").Append("{\"name\":").Append(PropJsonFormat.Str(g.Name)).Append(",\"pivot\":").Append(Vec3(g.Pivot)).Append('}');
                }
                sb.Append(']');
            }
            if (spec.Points.Count > 0)
            {
                sb.Append(",\"points\":[");
                for (int i = 0; i < spec.Points.Count; i++)
                {
                    PropPoint p = spec.Points[i];
                    sb.Append(i == 0 ? "" : ",").Append("{\"name\":").Append(PropJsonFormat.Str(p.Name)).Append(",\"pos\":").Append(Vec3(p.Pos))
                        .Append(",\"yaw\":").Append(PropJsonFormat.Float(p.Yaw)).Append('}');
                }
                sb.Append(']');
            }
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>파일로 쓴다(UTF-8, BOM 없음). 폴더가 없으면 만든다.</summary>
        public static void WriteFile(PropSpec spec, string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, ToJson(spec), new UTF8Encoding(false));
        }

        private static string Vec3(Vector3 v)
        {
            return "[" + PropJsonFormat.Float(v.x) + "," + PropJsonFormat.Float(v.y) + "," + PropJsonFormat.Float(v.z) + "]";
        }

        // ───────── 읽기 ─────────

        public static PropSpec ReadFile(string path) => FromJson(File.ReadAllText(path, Encoding.UTF8));

        public static PropSpec FromJson(string json)
        {
            if (!(PropJsonReader.Parse(json) is Dictionary<string, object> root)) throw new FormatException("[PropSpecJson] 최상위가 객체가 아님");
            var spec = new PropSpec { Id = root.TryGetValue("id", out object id) ? id as string : null };
            if (root.TryGetValue("frame", out object frame) && frame is string fs && fs != "unity")
            {
                Debug.LogWarning("[PropSpecJson] frame이 unity가 아님(" + fs + "). 좌표 변환 없이 읽는다.");
            }
            if (root.TryGetValue("parts", out object partsObj) && partsObj is List<object> parts)
            {
                foreach (object po in parts)
                {
                    if (!(po is Dictionary<string, object> p)) continue;
                    if (!p.TryGetValue("gen", out object genObj) || !(genObj is Dictionary<string, object> gen))
                    {
                        Debug.LogWarning("[PropSpecJson] gen 없는 부품은 건너뜀 (" + spec.Id + ")");
                        continue;
                    }
                    spec.Parts.Add(new PropPart
                    {
                        Mat = p.TryGetValue("mat", out object mat) ? mat as string : null,
                        Group = p.TryGetValue("group", out object grp) && grp != null ? Int(grp) : -1,
                        Matrix = p.TryGetValue("matrix", out object mo) ? Matrix(mo) : Matrix4x4.identity,
                        Gen = ReadGen(gen),
                    });
                }
            }
            if (root.TryGetValue("palette", out object palObj) && palObj is Dictionary<string, object> pal)
            {
                foreach (var kv in pal)
                {
                    if (!(kv.Value is Dictionary<string, object> e)) continue;
                    string hex = e.TryGetValue("hex", out object h) ? h as string : null;
                    float glow = e.TryGetValue("glow", out object g) && g != null ? Float(g) : 0f;
                    spec.Palette[kv.Key] = new PropPaletteEntry(PropPaletteEntry.Hex(hex), glow);
                }
            }
            if (root.TryGetValue("groups", out object grObj) && grObj is List<object> groups)
            {
                foreach (object go in groups)
                {
                    if (!(go is Dictionary<string, object> g)) continue;
                    spec.Groups.Add(new PropGroup
                    {
                        Name = g.TryGetValue("name", out object nm) ? nm as string : null,
                        Pivot = g.TryGetValue("pivot", out object pv) ? Vec3(pv) : Vector3.zero,
                    });
                }
            }
            if (root.TryGetValue("points", out object ptObj) && ptObj is List<object> points)
            {
                foreach (object po in points)
                {
                    if (!(po is Dictionary<string, object> p)) continue;
                    spec.Points.Add(new PropPoint
                    {
                        Name = p.TryGetValue("name", out object nm) ? nm as string : null,
                        Pos = p.TryGetValue("pos", out object ps) ? Vec3(ps) : Vector3.zero,
                        Yaw = p.TryGetValue("yaw", out object y) && y != null ? Float(y) : 0f,
                    });
                }
            }
            return spec;
        }

        /// <summary>"gen" 객체 하나 → PropGen. 모르는 type은 예외.</summary>
        public static PropGen ReadGen(Dictionary<string, object> g)
        {
            string type = g.TryGetValue("type", out object t) ? t as string : null;
            switch (type)
            {
                case "roundbox":
                    return new PropGenRoundBox(Vec3(Req(g, "size")), Float(Req(g, "radius")), Opt(g, "seg", 4));
                case "extrude":
                    return new PropGenExtrude(Points(Req(g, "outline")), Float(Req(g, "depth")), Float(Req(g, "bevel")), Opt(g, "arc", 3));
                case "utorus":
                    return new PropGenTorus(Float(Req(g, "major")), Float(Req(g, "minor")), Opt(g, "seg_major", 32), Opt(g, "seg_minor", 10),
                        g.TryGetValue("arc", out object arc) && arc != null ? Float(arc) : 360f);
                case "lathe":
                    return new PropGenLathe(Points(Req(g, "profile")), Opt(g, "segments", 32), Opt(g, "lobes", 0),
                        g.TryGetValue("amp", out object amp) && amp != null ? Float(amp) : 0f,
                        g.TryGetValue("sharp", out object sh) && sh is bool b && b);
                default:
                    throw new FormatException("[PropSpecJson] 모르는 gen type: " + type);
            }
        }

        private static object Req(Dictionary<string, object> g, string key)
        {
            if (!g.TryGetValue(key, out object v) || v == null) throw new FormatException("[PropSpecJson] gen에 " + key + " 없음");
            return v;
        }

        private static int Opt(Dictionary<string, object> g, string key, int fallback)
        {
            return g.TryGetValue(key, out object v) && v != null ? Int(v) : fallback;
        }

        private static float Float(object o)
        {
            if (o is PropJsonNumber n) return n.ToFloat();
            throw new FormatException("[PropSpecJson] 숫자가 아님: " + o);
        }

        private static int Int(object o)
        {
            if (o is PropJsonNumber n) return n.ToInt();
            throw new FormatException("[PropSpecJson] 정수가 아님: " + o);
        }

        private static Vector3 Vec3(object o)
        {
            if (!(o is List<object> l) || l.Count < 3) throw new FormatException("[PropSpecJson] [x,y,z] 아님");
            return new Vector3(Float(l[0]), Float(l[1]), Float(l[2]));
        }

        private static List<Vector2> Points(object o)
        {
            if (!(o is List<object> l)) throw new FormatException("[PropSpecJson] [[x,y],...] 아님");
            var pts = new List<Vector2>(l.Count);
            foreach (object e in l)
            {
                if (!(e is List<object> xy) || xy.Count < 2) throw new FormatException("[PropSpecJson] [x,y] 아님");
                pts.Add(new Vector2(Float(xy[0]), Float(xy[1])));
            }
            return pts;
        }

        private static Matrix4x4 Matrix(object o)
        {
            if (!(o is List<object> l) || l.Count != 16) throw new FormatException("[PropSpecJson] matrix는 16개 숫자여야 함");
            var m = new Matrix4x4();
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    m[r, c] = Float(l[r * 4 + c]);
            return m;
        }
    }
}
