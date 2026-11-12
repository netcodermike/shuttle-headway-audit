using System.Text.Json;

try
{
    if (args.Length != 1) throw new ArgumentException("Pass one JSON input file");
    using var data = JsonDocument.Parse(File.ReadAllText(args[0]));
    Console.WriteLine(JsonSerializer.Serialize(Analyze(data.RootElement)));
    return 0;
}
catch (Exception error) when (error is IOException or JsonException or FormatException
                               or ArgumentException or InvalidOperationException
                               or KeyNotFoundException or OverflowException)
{
    Console.Error.WriteLine(error.Message);
    return 2;
}

static object Analyze(JsonElement input)
{
    var target = input.GetProperty("targetMinutes").GetDouble();
    if (!double.IsFinite(target) || target <= 0) throw new ArgumentException("Target must be positive");
    var moments = input.GetProperty("departures").EnumerateArray().Select(item =>
    {
        var stamp = item.GetString() ?? throw new ArgumentException("Missing timestamp");
        if (!System.Text.RegularExpressions.Regex.IsMatch(stamp, @"T.*(?:Z|[+-]\d{2}:\d{2})$"))
            throw new ArgumentException("Expected an ISO timestamp with explicit offset");
        return item.GetDateTimeOffset();
    }).Order().ToArray();
    if (moments.Length < 2) throw new ArgumentException("Provide two or more departures");
    var intervals = new List<object>();
    var minutes = new List<double>();
    for (var index = 1; index < moments.Length; index++)
    {
        var gap = (moments[index] - moments[index - 1]).TotalMinutes;
        minutes.Add(gap);
        intervals.Add(new { from = moments[index - 1], to = moments[index], minutes = gap });
    }
    return new {
        gaps = minutes.Count,
        largestGapMinutes = minutes.Max(),
        averageGapMinutes = Math.Round(minutes.Average(), 2),
        overTarget = minutes.Count(gap => gap > target),
        intervals
    };
}
