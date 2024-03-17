using System.Text.Json;

namespace MockApi.Util
{
    public static class Utility
    {
        public static bool IsJsonValid(this string txt)
        {
            try { return JsonDocument.Parse(txt) != null; } catch { return false; }
        }
    }
}
