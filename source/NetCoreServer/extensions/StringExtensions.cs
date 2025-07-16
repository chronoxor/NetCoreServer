using System.Linq;

namespace NetCoreServer.extensions;

public static class StringExtensions
{
    // todo optimize
    public static string RemoveWhiteSpace(this string self) => string.IsNullOrEmpty(self)
        ? self
        : new string(self.Where(c => !char.IsWhiteSpace(c)).ToArray());
}