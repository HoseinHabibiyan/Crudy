using System.Text.Json;

namespace Crudy.Util
{
    public static class Utility
    {
        public static bool IsJsonValid(this string txt)
        {
            try { return JsonDocument.Parse(txt) != null; } catch { return false; }
        }
    }
}
