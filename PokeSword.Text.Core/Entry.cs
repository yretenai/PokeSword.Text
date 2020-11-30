using System.Collections.Generic;

namespace PokeSword.Text.Core
{
    public struct Entry
    {
        public string Text { get; set; }
        public List<Syntax>? SyntaxTree { get; set; }
        public bool ForceFullWidth { get; set; }
        public short ExData { get; set; }
    }
}
