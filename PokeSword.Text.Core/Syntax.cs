namespace PokeSword.Text.Core
{
    public struct Syntax
    {
        public string Hint { get; set; }
        public bool IsCommand { get; set; }
        public bool IsSpecial { get; set; }
        public ushort[]? Value { get; set; }
    }
}
