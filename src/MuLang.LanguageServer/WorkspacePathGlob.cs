using System.Text;
using System.Text.RegularExpressions;

namespace MuLang.LanguageServer;

internal sealed class WorkspacePathGlob
{
    private readonly Regex regex;

    public WorkspacePathGlob(string pattern)
    {
        string normalizedPattern = NormalizeAndValidate(pattern);
        RegexOptions options =
            RegexOptions.CultureInvariant |
            RegexOptions.NonBacktracking;

        if (OperatingSystem.IsWindows())
        {
            options |= RegexOptions.IgnoreCase;
        }

        regex = new Regex(
            $"^{ConvertToRegex(normalizedPattern)}$",
            options,
            TimeSpan.FromSeconds(1)
        );
    }

    public bool IsMatch(string path)
    {
        return regex.IsMatch(path.Replace('\\', '/'));
    }

    private static string NormalizeAndValidate(string pattern)
    {
        string normalizedPattern = pattern.Replace('\\', '/');

        if (
            normalizedPattern.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(pattern) ||
            normalizedPattern
                .Split('/')
                .Any(static segment => segment == "..")
        )
        {
            throw new FormatException(
                $"Glob '{pattern}' must be relative to the workspace root."
            );
        }

        return normalizedPattern;
    }

    private static string ConvertToRegex(string pattern)
    {
        StringBuilder builder = new ();

        for (int index = 0; index < pattern.Length; index++)
        {
            char character = pattern[index];

            switch (character)
            {
                case '*':
                {
                    if (
                        index + 1 < pattern.Length &&
                        pattern[index + 1] == '*'
                    )
                    {
                        index++;

                        if (
                            index + 1 < pattern.Length &&
                            pattern[index + 1] == '/'
                        )
                        {
                            index++;
                            builder.Append("(?:.*/)?");
                        }
                        else
                        {
                            builder.Append(".*");
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }

                    break;
                }

                case '?':
                {
                    builder.Append("[^/]");
                    break;
                }

                default:
                {
                    builder.Append(Regex.Escape(character.ToString()));
                    break;
                }
            }
        }

        return builder.ToString();
    }
}
