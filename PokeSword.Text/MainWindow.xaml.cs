using JetBrains.Annotations;
using PokeSword.Text.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PokeSword.Text
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : INotifyPropertyChanged
    {
        public MainWindow() => InitializeComponent();

        public Entry[] Entries { get; set; } = Array.Empty<Entry>();

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = false,
                CheckFileExists = true,
                ValidateNames = true,
                DereferenceLinks = true,
                Filter = "Binary Text files (*.bin;*.dat;*.btxt)|*.bin;*.dat;*.btxt|Syntax Files (*.yml;*.yaml)|*.yaml|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            switch (ext)
            {
                case ".yml":
                case ".yaml":
                {
                    var builder = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                    Entries = builder.Deserialize<Entry[]>(File.ReadAllText(dialog.FileName));
                    break;
                }
                case ".txt":
                    // TODO: Process text files (loses data!)
                    break;
                default:
                    Entries = TextBlob.DecodeStrings(File.ReadAllBytes(dialog.FileName));
                    break;
            }

            OnPropertyChanged(nameof(Entries));
        }

        private void OpenFolder(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                AutoUpgradeEnabled = true,
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath) || !Directory.Exists(dialog.SelectedPath)) return;


            var files = new List<string>();
            files.AddRange(Directory.GetFiles(dialog.SelectedPath, "*.bin", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(dialog.SelectedPath, "*.dat", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(dialog.SelectedPath, "*.btxt", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(dialog.SelectedPath, "*.yaml", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(dialog.SelectedPath, "*.yml", SearchOption.AllDirectories));

            var entries = new List<Entry>();
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                switch (ext)
                {
                    case ".yml":
                    case ".yaml":
                    {
                        var builder = new DeserializerBuilder().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                        entries.AddRange(builder.Deserialize<Entry[]>(File.ReadAllText(file)));
                        break;
                    }
                    case ".txt":
                        // TODO: Process .TXT files (loses data!)
                        break;
                    default:
                        entries.AddRange(TextBlob.DecodeStrings(File.ReadAllBytes(file)));
                        break;
                }
            }

            Entries = entries.ToArray();
            OnPropertyChanged(nameof(Entries));
        }

        private void SaveFile(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                ValidateNames = true,
                DereferenceLinks = true,
                Filter = "NX files (*.dat)|*.dat|NDS files (*.bin)|*.bin|PokeSword files (*.btxt)|*.btxt|Syntax Files (*.yaml)|*.yaml|Text Files (*.txt)|*.txt|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return;

            switch (Path.GetExtension(dialog.FileName).ToLower())
            {
                case ".yml":
                case ".yaml":
                    var builder = new SerializerBuilder().DisableAliases().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                    File.WriteAllText(Path.ChangeExtension(dialog.FileName, ".yaml"), builder.Serialize(Entries));
                    break;
                case ".txt":
                    File.WriteAllLines(dialog.FileName, Entries.Select((x, y) => $"{y}, {x.Text.Replace("\n", "\\n")}"));
                    break;
                default:
                    var blob = TextBlob.EncodeStrings(Entries);
                    File.WriteAllBytes(dialog.FileName, blob.ToArray());
                    break;
            }
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName]
            string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
