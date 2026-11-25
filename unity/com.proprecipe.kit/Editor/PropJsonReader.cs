using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PropRecipe
{
    /// <summary>
    /// 의존성 없는 작은 JSON 파서. 객체 = Dictionary&lt;string, object&gt;, 배열 = List&lt;object&gt;,
    /// 숫자 = PropJsonNumber(원문 문자열 그대로, 읽는 쪽이 float/int로 바꾼다), 문자열 = string, true/false = bool, null = null.
    /// </summary>
    public sealed class PropJsonReader
    {
        private readonly string _text;
        private int _pos;

        private PropJsonReader(string text)
        {
            _text = text;
            _pos = 0;
        }

        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var reader = new PropJsonReader(text);
            reader.SkipWs();
            object value = reader.ReadValue();
            reader.SkipWs();
            if (reader._pos != text.Length) throw reader.Error("JSON 뒤에 남은 문자");
            return value;
        }

        private object ReadValue()
        {
            SkipWs();
            if (_pos >= _text.Length) throw Error("값이 없음");
            char ch = _text[_pos];
            switch (ch)
            {
                case '{': return ReadObject();
                case '[': return ReadArray();
                case '"': return ReadString();
                case 't': Expect("true"); return true;
                case 'f': Expect("false"); return false;
                case 'n': Expect("null"); return null;
                default:
                    if (ch == '-' || (ch >= '0' && ch <= '9')) return ReadNumber();
                    throw Error("알 수 없는 문자 '" + ch + "'");
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            var obj = new Dictionary<string, object>();
            _pos++; // {
            SkipWs();
            if (Peek() == '}') { _pos++; return obj; }
            while (true)
            {
                SkipWs();
                if (Peek() != '"') throw Error("객체 키는 문자열이어야 함");
                string key = ReadString();
                SkipWs();
                if (Peek() != ':') throw Error("':' 필요");
                _pos++;
                obj[key] = ReadValue();
                SkipWs();
                char ch = Peek();
                _pos++;
                if (ch == ',') continue;
                if (ch == '}') return obj;
                throw Error("',' 또는 '}' 필요");
            }
        }

        private List<object> ReadArray()
        {
            var list = new List<object>();
            _pos++; // [
            SkipWs();
            if (Peek() == ']') { _pos++; return list; }
            while (true)
            {
                list.Add(ReadValue());
                SkipWs();
                char ch = Peek();
                _pos++;
                if (ch == ',') continue;
                if (ch == ']') return list;
                throw Error("',' 또는 ']' 필요");
            }
        }

        private string ReadString()
        {
            _pos++; // "
            var sb = new StringBuilder();
            while (true)
            {
                if (_pos >= _text.Length) throw Error("문자열이 닫히지 않음");
                char ch = _text[_pos++];
                if (ch == '"') return sb.ToString();
                if (ch != '\\')
                {
                    sb.Append(ch);
                    continue;
                }
                if (_pos >= _text.Length) throw Error("잘린 이스케이프");
                char esc = _text[_pos++];
                switch (esc)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_pos + 4 > _text.Length) throw Error("잘린 \\u 이스케이프");
                        sb.Append((char)int.Parse(_text.Substring(_pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        _pos += 4;
                        break;
                    default: throw Error("알 수 없는 이스케이프 \\" + esc);
                }
            }
        }

        private PropJsonNumber ReadNumber()
        {
            int start = _pos;
            if (Peek() == '-') _pos++;
            while (_pos < _text.Length)
            {
                char ch = _text[_pos];
                if ((ch >= '0' && ch <= '9') || ch == '.' || ch == 'e' || ch == 'E' || ch == '+' || ch == '-') _pos++;
                else break;
            }
            if (_pos == start) throw Error("숫자 아님");
            return new PropJsonNumber(_text.Substring(start, _pos - start));
        }

        private void Expect(string word)
        {
            if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0) throw Error("'" + word + "' 필요");
            _pos += word.Length;
        }

        private char Peek() => _pos < _text.Length ? _text[_pos] : '\0';

        private void SkipWs()
        {
            while (_pos < _text.Length)
            {
                char ch = _text[_pos];
                if (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == '﻿') _pos++;
                else break;
            }
        }

        private FormatException Error(string message) => new FormatException("[PropJsonReader] " + message + " (위치 " + _pos + ")");
    }
}
