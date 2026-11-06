using System.Text.Json;
using System.Text.Json.Serialization;

namespace SantaShift.Core.Content;

/// <summary>One set of JSON rules for every data and save file: camelCase names, comments and trailing commas allowed, enums as text.</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
