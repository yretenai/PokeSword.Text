using PokeSword.Text.Core;
using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PokeSword.Text.CLI
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("Usage: PokeSword.Text.CLI.exe yaml/bin/directory...");
                return;
            }

            var files = new List<string>();
            foreach (var arg in args)
            {
                if (Directory.Exists(arg))
                {
                    files.AddRange(Directory.GetFiles(arg, "*.bin", SearchOption.AllDirectories));
                    files.AddRange(Directory.GetFiles(arg, "*.dat", SearchOption.AllDirectories));
                    files.AddRange(Directory.GetFiles(arg, "*.yaml", SearchOption.AllDirectories));
                }
                else
                {
                    files.Add(arg);
                }
            }

            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine($"Can't open file {file}, it does not exist.");
                    continue;
                }

                Console.WriteLine($"Processing {file}");

                if (Path.GetExtension(file) == ".yaml")
                {
                    var builder = new DeserializerBuilder().IgnoreUnmatchedProperties().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                    var blob = TextBlob.EncodeStrings(builder.Deserialize<Entry[]>(File.ReadAllText(file)));
                    File.WriteAllBytes(Path.ChangeExtension(file, ".dat"), blob.ToArray());
                }
                else
                {
                    var blob = TextBlob.DecodeStrings(File.ReadAllBytes(file));
                    var builder = new SerializerBuilder().DisableAliases().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                    File.WriteAllText(Path.ChangeExtension(file, ".yaml"), builder.Serialize(blob));
                }
            }
        }
    }
}
