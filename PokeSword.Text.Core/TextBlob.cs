using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PokeSword.Text.Core
{
    public static class TextBlob
    {
        private const ushort Iv = 0x7C89;
        private const ushort Key = 0x2983;

        private static readonly Dictionary<ushort, char> SpecialChars = new Dictionary<ushort, char>
        {
            { 57471, (char) 0x202F }, // nbsp
            { 57485, '…' },
            { 57486, '♂' },
            { 57487, '♀' }
        };

        private static readonly Dictionary<ushort, string> Names = new Dictionary<ushort, string>
        {
            { 48639, "NULL" },
            { 48640, "SCROLL" },
            { 48641, "CLEAR" },
            { 48642, "WAIT" },
            { 65280, "COLOR" },
            { 256, "TRNAME" },
            { 257, "PKNAME" },
            { 258, "PKNICK" },
            { 259, "TYPE" },
            { 261, "LOCATION" },
            { 262, "ABILITY" },
            { 263, "MOVE" },
            { 264, "ITEM1" },
            { 265, "ITEM2" },
            { 266, "sTRBAG" },
            { 267, "BOX" },
            { 269, "EVSTAT" },
            { 272, "OPOWER" },
            { 295, "RIBBON" },
            { 308, "MIINAME" },
            { 318, "WEATHER" },
            { 393, "TRNICK" },
            { 394, "1stchrTR" },
            { 395, "SHOUTOUT" },
            { 398, "BERRY" },
            { 399, "REMFEEL" },
            { 400, "REMQUAL" },
            { 401, "WEBSITE" },
            { 412, "CHOICECOS" },
            { 417, "GSYNCID" },
            { 402, "PRVIDSAY" },
            { 403, "BTLTEST" },
            { 405, "GENLOC" },
            { 409, "CHOICEFOOD" },
            { 410, "HOTELITEM" },
            { 411, "TAXISTOP" },
            { 415, "MAISTITLE" },
            { 4096, "ITEMPLUR0" },
            { 4097, "ITEMPLUR1" },
            { 4352, "GENDBR" },
            { 4353, "NUMBRNCH" },
            { 4866, "iCOLOR2" },
            { 4867, "iCOLOR3" },
            { 512, "NUM1" },
            { 513, "NUM2" },
            { 514, "NUM3" },
            { 515, "NUM4" },
            { 516, "NUM5" },
            { 517, "NUM6" },
            { 518, "NUM7" },
            { 519, "NUM8" },
            { 520, "NUM9" }
        };

        public static unsafe Entry[] DecodeStrings(Span<byte> data, bool crypt = true)
        {
            if (data.Length < sizeof(Header)) return Array.Empty<Entry>();
            var header = MemoryMarshal.Read<Header>(data);

            if (header.Vector != 0) throw new InvalidDataException("Initial Vector is non-zero");

            if (header.SectionDataOffset + header.TotalLength != data.Length || header.TextSections != 1)
                throw new InvalidDataException("Not a text file");

            if (MemoryMarshal.Read<uint>(data.Slice(header.SectionDataOffset)) != header.TotalLength)
                throw new InvalidDataException("Text section size and file size do not match");

            var strings = new Entry[header.LineCount];

            for (var lineIndex = 0; lineIndex < header.LineCount; ++lineIndex)
            {
                var offset = MemoryMarshal.Read<int>(data.Slice(lineIndex * 8 + header.SectionDataOffset + 4)) + header.SectionDataOffset;
                var length = MemoryMarshal.Read<ushort>(data.Slice(lineIndex * 8 + header.SectionDataOffset + 8));
                var entry = strings[lineIndex];
                entry.ExData = MemoryMarshal.Read<short>(data.Slice(lineIndex * 8 + header.SectionDataOffset + 10));
                var line = MemoryMarshal.Cast<byte, ushort>(data.Slice(offset, length * 2));
                if (crypt) Crypt(ref line, (ushort) (Iv + (ushort) (Key * lineIndex)));

                var totalString = "";
                var s = "";
                for (var index = 0; index < line.Length;)
                {
                    var c = line[index++];
                    if (c == 0) break;
                    switch (c)
                    {
                        case 0x10:
                        {
                            var varLength = line[index++];
                            Debug.Assert(varLength > 0);
                            var vars = line.Slice(index, varLength).ToArray();
                            var varType = vars[0];
                            index += varLength;

                            if (s.Length > 0)
                            {
                                entry.SyntaxTree ??= new List<Syntax>();
                                entry.SyntaxTree.Add(new Syntax
                                {
                                    Hint = s
                                });
                                totalString += s;
                            }

                            if (!Names.TryGetValue(varType, out var varName)) varName = varType.ToString("X4");

                            s = $"[COMMAND {varName}{(vars.Length > 1 ? $" {string.Join(", ", vars.Skip(1))}" : "")}]";
                            entry.SyntaxTree ??= new List<Syntax>();
                            entry.SyntaxTree.Add(new Syntax
                            {
                                Hint = s,
                                IsCommand = true,
                                Value = vars
                            });
                            totalString += s;
                            s = "";
                            break;
                        }
                        default:
                            if (c > 0xE07F)
                            {
                                if (SpecialChars.TryGetValue(c, out var translatedChar))
                                {
                                    s += translatedChar;
                                    break;
                                }

                                if (s.Length > 0)
                                {
                                    entry.SyntaxTree ??= new List<Syntax>();
                                    entry.SyntaxTree.Add(new Syntax
                                    {
                                        Hint = s
                                    });
                                    totalString += s;
                                }

                                s = $"[SPECIAL {c:X8}]";
                                entry.SyntaxTree ??= new List<Syntax>();
                                entry.SyntaxTree.Add(new Syntax
                                {
                                    Hint = s,
                                    IsSpecial = true,
                                    Value = new[] { c }
                                });
                                totalString += s;
                                s = "";
                            }
                            else
                            {
                                s += (char) c;
                            }

                            break;
                    }
                }

                if (s.Length > 0)
                {
                    if (entry.SyntaxTree?.Count > 0)
                        entry.SyntaxTree.Add(new Syntax
                        {
                            Hint = s
                        });

                    totalString += s;
                }

                entry.Text = totalString;
                strings[lineIndex] = entry;
            }

            return strings;
        }

        private static void GrowSpan<T>(ref Span<T> buffer, int size)
        {
            var tmp = new Span<T>(new T[buffer.Length + size]);
            buffer.CopyTo(tmp);
            buffer = tmp;
        }

        public static unsafe Span<byte> EncodeStrings(Entry[] entries, bool crypt = true)
        {
            var header = new Header
            {
                TextSections = 1,
                LineCount = (ushort) entries.Length,
                TotalLength = 0,
                Vector = 0,
                SectionDataOffset = sizeof(Header)
            };
            var data = new Span<byte>(new byte[sizeof(Header) + 4 + entries.Length * 8]);

            for (var lineIndex = 0; lineIndex < entries.Length; lineIndex++)
            {
                var line = entries[lineIndex];
                var offset = data.Length;
                BinaryPrimitives.WriteInt32LittleEndian(data.Slice(lineIndex * 8 + header.SectionDataOffset + 4), offset - header.SectionDataOffset);
                BinaryPrimitives.WriteInt16LittleEndian(data.Slice(lineIndex * 8 + header.SectionDataOffset + 10), line.ExData);
                line.SyntaxTree ??= new List<Syntax>
                {
                    new Syntax
                    {
                        Hint = line.Text
                    }
                };

                var lineData = Span<ushort>.Empty;
                foreach (var syntax in line.SyntaxTree)
                {
                    var start = lineData.Length;
                    if (syntax.Value == null)
                    {
                        if (syntax.IsSpecial || syntax.IsCommand) throw new InvalidDataException("is-special or is-command is set but no value data is passed?");
                        GrowSpan(ref lineData, syntax.Hint.Length);
                        Encoding.Unicode.GetBytes(syntax.Hint).CopyTo(MemoryMarshal.Cast<ushort, byte>(lineData.Slice(start)));
                    }
                    else if (syntax.IsSpecial)
                    {
                        GrowSpan(ref lineData, 1);
                        lineData[start] = syntax.Value[0];
                    }
                    else if (syntax.IsCommand)
                    {
                        GrowSpan(ref lineData, 2);
                        lineData[start] = 0x10;
                        lineData[start + 1] = (ushort) syntax.Value.Length;

                        GrowSpan(ref lineData, syntax.Value.Length);
                        syntax.Value.CopyTo(lineData.Slice(start + 2));
                    }
                    else
                    {
                        throw new InvalidOperationException("empty line? use [COMMAND NULL] for null lines.");
                    }
                }

                GrowSpan(ref lineData, 1);
                if (crypt) Crypt(ref lineData, (ushort) (Iv + (ushort) (Key * lineIndex)));
                BinaryPrimitives.WriteUInt16LittleEndian(data.Slice(lineIndex * 8 + header.SectionDataOffset + 8), (ushort) lineData.Length);
                GrowSpan(ref data, lineData.Length * 2);
                MemoryMarshal.Cast<ushort, byte>(lineData).CopyTo(data.Slice(offset));
                if (data.Length % 4 != 0) GrowSpan(ref data, 4 - data.Length % 4);
            }

            header.TotalLength = data.Length - header.SectionDataOffset;
            MemoryMarshal.Write(data, ref header);
            BinaryPrimitives.WriteInt32LittleEndian(data.Slice(header.SectionDataOffset), header.TotalLength);
            return data;
        }

        private static void Crypt(ref Span<ushort> value, ushort key)
        {
            for (var index = 0; index < value.Length; index++)
            {
                value[index] = (ushort) (key ^ value[index]);
                key = (ushort) ((key << 3 | key >> 13) & 0xffff);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Header
        {
            public ushort TextSections { get; set; }
            public ushort LineCount { get; set; }
            public int TotalLength { get; set; }
            public uint Vector { get; set; }
            public int SectionDataOffset { get; set; }
        }
    }
}
