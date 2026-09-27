using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using TableSnap.Models;

namespace TableSnap.Recognition;

public sealed class OpenAiTableRecognizer : ITableRecognizer
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiTableRecognizer(string apiKey, string model = "gpt-4o")
    {
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<TableDocument> RecognizeAsync(byte[] pngBytes, CancellationToken cancellationToken = default)
    {
        var schema = new
        {
            type = "object",
            properties = new
            {
                rows = new { type = "integer" },
                columns = new { type = "integer" },
                cells = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            row = new { type = "integer" },
                            column = new { type = "integer" },
                            rowSpan = new { type = "integer" },
                            columnSpan = new { type = "integer" },
                            text = new { type = "string" },
                            bold = new { type = "boolean" },
                            align = new { type = "string", @enum = new[] { "left", "center", "right" } }
                        },
                        required = new[] { "row", "column", "rowSpan", "columnSpan", "text", "bold", "align" },
                        additionalProperties = false
                    }
                }
            },
            required = new[] { "rows", "columns", "cells" },
            additionalProperties = false
        };

        var requestBody = new
        {
            model = _model,
            store = false,
            input = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = "Read this image as one table. Reconstruct the visible row/column grid, merged cells and text exactly. Coordinates are zero-based. Preserve empty cells where needed. Do not flatten merged headers. Estimate bold and alignment. If no table is visible, return a 1x1 table with an empty cell." },
                        new { type = "input_image", image_url = "data:image/png;base64," + Convert.ToBase64String(pngBytes), detail = "high" }
                    }
                }
            },
            text = new
            {
                format = new { type = "json_schema", name = "table_structure", strict = true, schema }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        using var response = await Client.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Recognition service returned {(int)response.StatusCode}: {responseText[..Math.Min(responseText.Length, 500)]}");

        using var outer = JsonDocument.Parse(responseText);
        if (!outer.RootElement.TryGetProperty("output", out var output))
            throw new InvalidDataException("The recognition service did not return an output field.");

        string? json = null;
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content)) continue;
            foreach (var part in content.EnumerateArray())
                if (part.TryGetProperty("type", out var type) && type.GetString() == "output_text" &&
                    part.TryGetProperty("text", out var value))
                    json = value.GetString();
        }
        if (json is null) throw new InvalidDataException("The recognition service did not return table content.");

        var document = JsonSerializer.Deserialize<TableDocument>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Could not parse the recognized table.");
        document.Validate();
        return document;
    }
}
