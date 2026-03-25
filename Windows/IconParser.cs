using System.Text.RegularExpressions;

namespace CreateXIV;

public static class IconParser
{
    private static readonly Regex regex = new(@"\{(.*?)\}");

    public static string Parse(string input)
    {
        return regex.Replace(input, match =>
        {
            var key = match.Groups[1].Value.ToLower();

            if (FfxivIcons.Map.TryGetValue(key, out var value))
                return value;

            return match.Value; // mantém se não existir
        });
    }
}