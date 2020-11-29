using System.Collections.Generic;
using System.Linq;

namespace PokeSword.Text
{
    internal static class Utility
    {
        public static string CreateFilter(params (string name, IEnumerable<string> types)[] filters) =>
            string.Join("|", filters.Select(x => (x.name, types: x.types.Select(y => $"*.{y}").ToArray())).Select(x => $"{x.name} ({string.Join(";", x.types)})|{string.Join(";", x.types)}")) + "|All files (*.*)|*.*";
    }
}
