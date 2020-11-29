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
            { 0xE07F, (char) 0x202F }, // nbsp
            { 0xE08D, '…' },
            { 0xE08E, '♂' },
            { 0xE08F, '♀' }
        };

        private static readonly Dictionary<ushort, string> Names = new Dictionary<ushort, string>
        {
            { 0xBDFF, "NULL" },
            { 0xBE00, "SCROLL" },
            { 0xBE01, "CLEAR" },
            { 0xBE02, "WAIT" },
            { 0xFF00, "COLOR" },
            { 0x0100, "TRNAME" },
            { 0x0101, "PKNAME" },
            { 0x0102, "PKNICK" },
            { 0x0103, "TYPE" },
            { 0x0105, "LOCATION" },
            { 0x0106, "ABILITY" },
            { 0x0107, "MOVE" },
            { 0x0108, "ITEM1" },
            { 0x0109, "ITEM2" },
            { 0x010A, "sTRBAG" },
            { 0x010B, "BOX" },
            { 0x010D, "EVSTAT" },
            { 0x0110, "OPOWER" },
            { 0x0127, "RIBBON" },
            { 0x0134, "MIINAME" },
            { 0x013E, "WEATHER" },
            { 0x0189, "TRNICK" },
            { 0x018A, "1stchrTR" },
            { 0x018B, "SHOUTOUT" },
            { 0x018E, "BERRY" },
            { 0x018F, "REMFEEL" },
            { 0x0190, "REMQUAL" },
            { 0x0191, "WEBSITE" },
            { 0x019C, "CHOICECOS" },
            { 0x01A1, "GSYNCID" },
            { 0x0192, "PRVIDSAY" },
            { 0x0193, "BTLTEST" },
            { 0x0195, "GENLOC" },
            { 0x0199, "CHOICEFOOD" },
            { 0x019A, "HOTELITEM" },
            { 0x019B, "TAXISTOP" },
            { 0x019F, "MAISTITLE" },
            { 0x1000, "ITEMPLUR0" },
            { 0x1001, "ITEMPLUR1" },
            { 0x1100, "GENDBR" },
            { 0x1101, "NUMBRNCH" },
            { 0x1302, "iCOLOR2" },
            { 0x1303, "iCOLOR3" },
            { 0x0200, "NUM1" },
            { 0x0201, "NUM2" },
            { 0x0202, "NUM3" },
            { 0x0203, "NUM4" },
            { 0x0204, "NUM5" },
            { 0x0205, "NUM6" },
            { 0x0206, "NUM7" },
            { 0x0207, "NUM8" },
            { 0x0208, "NUM9" }
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
