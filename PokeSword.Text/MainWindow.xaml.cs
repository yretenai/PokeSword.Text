﻿using JetBrains.Annotations;
using PokeSword.Text.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using CheckBox = System.Windows.Controls.CheckBox;
using DragEventArgs = System.Windows.DragEventArgs;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

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
                Filter = Utility.CreateFilter(("Binary Text files", new[] { "bin", "dat" }), ("Syntax files", new[] { "yaml", "yml" }), ("Text files", new[] { "txt" }))
            };

            if (dialog.ShowDialog() != true) return;
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            switch (ext)
            {
                case ".yml":
                case ".yaml":
                {
                    var builder = new DeserializerBuilder().IgnoreUnmatchedProperties().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                    Entries = builder.Deserialize<Entry[]>(File.ReadAllText(dialog.FileName));
                    break;
                }
                case ".txt":
                    Entries = File.ReadAllLines(dialog.FileName).Select(TextBlob.ParseEntry).ToArray();
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
                        var builder = new DeserializerBuilder().IgnoreUnmatchedProperties().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                        entries.AddRange(builder.Deserialize<Entry[]>(File.ReadAllText(file)));
                        break;
                    }
                    case ".txt":
                        entries.AddRange(File.ReadAllLines(file).Select(TextBlob.ParseEntry).ToArray());
                        break;
                    default:
                        entries.AddRange(TextBlob.DecodeStrings(File.ReadAllBytes(file)));
                        break;
                }
            }

            Entries = entries.ToArray();
            OnPropertyChanged(nameof(Entries));
        }

        private void WriteFile(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                ValidateNames = true,
                DereferenceLinks = true,
                Filter = Utility.CreateFilter(("Binary Text files", new[] { "bin", "dat" }), ("Syntax files", new[] { "yaml", "yml" }), ("Text files", new[] { "txt" }))
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
                    File.WriteAllLines(dialog.FileName, Entries.Select((x, y) => $"{y}, {x.Text.Replace("\n", "\\n")}[EXTDATA {x.ExData}]"));
                    break;
                default:
                {
                    var blob = TextBlob.EncodeStrings(Entries, ShouldCrypt.IsChecked == true, ShouldWasteSpace.IsChecked == true);
                    File.WriteAllBytes(dialog.FileName, blob.ToArray());
                    break;
                }
            }
        }

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName]
            string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void UpdateTree(object? sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            var entry = (Entry) e.Row.Item;
            switch (e.EditingElement)
            {
                case TextBox textBox:
                {
                    var text = textBox.Text;
                    Entries[e.Row.GetIndex()] = TextBlob.ParseEntry(text, entry);
                    break;
                }
                case CheckBox checkBox:
                    Entries[e.Row.GetIndex()].ForceFullWidth = checkBox.IsChecked == true;
                    break;
            }
        }

        private void DropFile(object sender, DragEventArgs e)
        {
            var file = (e.Data.GetData("FileNameW") as string[] ?? Array.Empty<string>()).FirstOrDefault();
            if (string.IsNullOrEmpty(file)) return;

            var ext = Path.GetExtension(file).ToLowerInvariant();
            switch (ext)
            {
                case ".yml":
                case ".yaml":
                {
                    var builder = new DeserializerBuilder().IgnoreUnmatchedProperties().WithNamingConvention(HyphenatedNamingConvention.Instance).Build() ?? throw new Exception();
                    Entries = builder.Deserialize<Entry[]>(File.ReadAllText(file));
                    break;
                }
                case ".txt":
                    Entries = File.ReadAllLines(file).Select(TextBlob.ParseEntry).ToArray();
                    break;
                default:
                    Entries = TextBlob.DecodeStrings(File.ReadAllBytes(file));
                    break;
            }

            OnPropertyChanged(nameof(Entries));
        }
    }
}
