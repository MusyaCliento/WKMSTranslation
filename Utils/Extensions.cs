namespace WKMSTranslation.Utils
{
    public static class Extensions
    {
        public static string GetExactKey(this string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            return str.Replace("\r\n", "\\n")
                      .Replace("\n", "\\n")
                      .Replace("\r", "")
                      .Replace("\0", "")
                      .Replace("\u200b", "")
                      .Replace("\u00a0", "")
                      .Replace("<noparse></noparse>", "")
                      .Trim();

        }
    }
}