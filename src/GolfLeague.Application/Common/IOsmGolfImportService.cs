using GolfLeague.Application.DTOs;

namespace GolfLeague.Application.Common;

public sealed record OsmGolfHole(
    int HoleNumber,
    GeoPointDto Tee,
    GeoPointDto Green,
    GeoPointDto? GreenFront,
    GeoPointDto? GreenBack,
    int? Par,
    long WayId);

public sealed record OsmGolfImportResult(
    IReadOnlyList<OsmGolfHole> Holes,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Looks up mapped golf holes (golf=hole ways and golf=green areas) around a
/// course's centre point on OpenStreetMap via the Overpass API.
/// </summary>
public interface IOsmGolfImportService
{
    /// <exception cref="HttpRequestException">Overpass could not be reached or returned an error.</exception>
    Task<OsmGolfImportResult> FetchHolesAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
