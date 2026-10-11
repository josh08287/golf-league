namespace GolfLeague.Application.DTOs;

public sealed record GeoPointDto(double Lat, double Lng);

public sealed record CourseHoleGeoHoleDto(
    int HoleNumber,
    GeoPointDto Tee,
    GeoPointDto Green,
    GeoPointDto? GreenFront,
    GeoPointDto? GreenBack,
    string Source,
    long? OsmWayId = null);

public sealed record CourseHoleGeoDto(
    int CourseId,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<CourseHoleGeoHoleDto> Holes);

/// <summary>Proposed hole coordinates from OpenStreetMap, for admin review before saving.</summary>
public sealed record OsmHoleGeoPreviewDto(
    IReadOnlyList<CourseHoleGeoHoleDto> Holes,
    IReadOnlyList<string> Warnings);
