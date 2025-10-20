using System.Text.RegularExpressions;

namespace RobloxGuard.Core;

/// <summary>
/// Parses placeId from Roblox protocol URIs and client command-line arguments.
/// </summary>
public static class PlaceIdParser
{
    // Regex patterns (case-insensitive)
    // Match placeId= with optional ? or & before it (or at start of string/after other chars)
    private static readonly Regex PlaceIdQueryPattern = new(
        @"(?:[?&/]|^)placeId=(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex PlaceLauncherPattern = new(
        @"PlaceLauncher\.ashx.*?[?&]placeId=(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex CommandLineIdPattern = new(
        @"--id\s+(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    /// <summary>
    /// Extracts the placeId from a URI or command-line string.
    /// </summary>
    /// <param name="input">The protocol URI or command line to parse.</param>
    /// <returns>The placeId if found, otherwise null.</returns>
    public static long? Extract(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // Try each pattern in order
        var match = PlaceIdQueryPattern.Match(input);
        if (match.Success)
            return ParseLong(match.Groups[1].Value);

        match = PlaceLauncherPattern.Match(input);
        if (match.Success)
            return ParseLong(match.Groups[1].Value);

        match = CommandLineIdPattern.Match(input);
        if (match.Success)
            return ParseLong(match.Groups[1].Value);

        return null;
    }

    /// <summary>
    /// Extracts all placeIds from a URI or command line (in case of multiple).
    /// Returns the first occurrence for consistency.
    /// </summary>
    /// <param name="input">The protocol URI or command line to parse.</param>
    /// <returns>List of all placeIds found.</returns>
    public static List<long> ExtractAll(string? input)
    {
        var results = new List<long>();
        if (string.IsNullOrWhiteSpace(input))
            return results;

        // Collect from all patterns
        AddMatches(results, PlaceIdQueryPattern.Matches(input));
        AddMatches(results, PlaceLauncherPattern.Matches(input));
        AddMatches(results, CommandLineIdPattern.Matches(input));

        return results;
    }

    private static void AddMatches(List<long> results, MatchCollection matches)
    {
        foreach (Match match in matches)
        {
            if (match.Success)
            {
                var value = ParseLong(match.Groups[1].Value);
                if (value.HasValue && !results.Contains(value.Value))
                    results.Add(value.Value);
            }
        }
    }

    private static long? ParseLong(string value)
    {
        return long.TryParse(value, out var result) ? result : null;
    }
}
