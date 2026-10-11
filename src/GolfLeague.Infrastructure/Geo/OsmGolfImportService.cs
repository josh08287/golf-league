using System.Globalization;
using System.Text.Json;
using GolfLeague.Application.Common;
using Microsoft.Extensions.Logging;

namespace GolfLeague.Infrastructure.Geo;

/// <summary>
/// Queries the public Overpass API for golf=hole / golf=green ways in a box
/// ~1.5 km around the course centre. A plain bounding-box query is used
/// (rather than an is_in area lookup) because it's the cheapest query shape
/// and the one the overloaded public mirrors answer most reliably; holes
/// from neighbouring courses are resolved by the parser keeping the hole
/// nearest the centre for each number.
/// </summary>
public sealed class OsmGolfImportService : IOsmGolfImportService
{
    public const string HttpClientName = "overpass";

    // Public Overpass instances are often overloaded (504s, 500s, or a 200
    // carrying a "runtime error" remark), so try mirrors in turn.
    private static readonly string[] Endpoints =
    [
        "https://overpass-api.de/api/interpreter",
        "https://overpass.private.coffee/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OsmGolfImportService> _logger;

    public OsmGolfImportService(IHttpClientFactory httpClientFactory, ILogger<OsmGolfImportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OsmGolfImportResult> FetchHolesAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        const double radiusM = 1500;
        var dLat = radiusM / 111_320;
        var dLng = dLat / Math.Cos(latitude * Math.PI / 180);
        string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        var bbox = $"{F(latitude - dLat)},{F(longitude - dLng)},{F(latitude + dLat)},{F(longitude + dLng)}";

        var json = await QueryAsync($"""
            [out:json][timeout:25];
            (way[golf=hole]({bbox}); way[golf=green]({bbox}););
            out geom;
            """, cancellationToken);
        return OverpassGolfParser.Parse(json, latitude, longitude);
    }

    private async Task<string> QueryAsync(string query, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        Exception? last = null;

        // Two passes over the mirrors: overload on the public instances
        // usually clears within a few seconds.
        foreach (var (endpoint, pass) in Endpoints.Select(e => (e, 0)).Concat(Endpoints.Select(e => (e, 1))))
        {
            if (pass == 1 && endpoint == Endpoints[0])
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

            try
            {
                using var content = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);
                using var response = await client.PostAsync(endpoint, content, cancellationToken);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (IsServerError(body))
                    throw new HttpRequestException($"Overpass at {endpoint} reported a runtime error.");
                return body;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                _logger.LogWarning("Overpass query to {Endpoint} failed: {Message}", endpoint, ex.Message);
                last = ex;
            }
        }

        throw new HttpRequestException("No Overpass server could answer the query.", last);
    }

    /// <summary>Overpass reports query timeouts / overload as a 200 with a "remark" and no elements.</summary>
    internal static bool IsServerError(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("remark", out var remark)
            && (remark.GetString() ?? string.Empty).Contains("error", StringComparison.OrdinalIgnoreCase);
    }
}
