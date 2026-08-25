using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using GolfLeague.Domain.Services;
using MediatR;

namespace GolfLeague.Application.Seasons.Commands;

public sealed record UpdateSeasonHalfCommand(
    int HalfId,
    DateOnly StartDate,
    DateOnly EndDate,
    string UserId,
    string? ScoringFormat = null,
    string? MatchPlayCustomFormula = null) : IRequest<Result<SeasonHalfDto>>, IAmAuditableCommand
{
    public string AuditEntityType => "SeasonHalf";
    public string AuditEntityId => HalfId.ToString();
}

public sealed class UpdateSeasonHalfCommandHandler : IRequestHandler<UpdateSeasonHalfCommand, Result<SeasonHalfDto>>
{
    private readonly IFlightRepository _flightRepository;
    private readonly IMatchPlayFormulaEvaluator _matchPlayFormulaEvaluator;

    public UpdateSeasonHalfCommandHandler(IFlightRepository flightRepository, IMatchPlayFormulaEvaluator matchPlayFormulaEvaluator)
    {
        _flightRepository = flightRepository;
        _matchPlayFormulaEvaluator = matchPlayFormulaEvaluator;
    }

    public async Task<Result<SeasonHalfDto>> Handle(UpdateSeasonHalfCommand request, CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
            return Result<SeasonHalfDto>.Fail("Half end date must be after start date.");

        if (!ScoringFormatExtensions.TryParse(request.ScoringFormat, out var scoringFormat))
            return Result<SeasonHalfDto>.Fail($"Unknown scoring format '{request.ScoringFormat}'.");

        var customFormula = string.IsNullOrWhiteSpace(request.MatchPlayCustomFormula) ? null : request.MatchPlayCustomFormula.Trim();
        if (customFormula is not null && !_matchPlayFormulaEvaluator.TryValidate(customFormula, out var formulaError))
            return Result<SeasonHalfDto>.Fail($"Invalid match play formula: {formulaError}");

        var half = await _flightRepository.GetHalfByIdAsync(request.HalfId, cancellationToken);
        if (half is null)
            return Result<SeasonHalfDto>.Fail($"Season half with ID {request.HalfId} not found.");

        half.StartDate = request.StartDate;
        half.EndDate = request.EndDate;
        half.ScoringFormat = scoringFormat;
        half.MatchPlayCustomFormula = scoringFormat == ScoringFormat.MatchPlay ? customFormula : null;

        await _flightRepository.UpdateHalfAsync(half, cancellationToken);

        return Result<SeasonHalfDto>.Ok(new SeasonHalfDto(
            half.Id,
            half.SeasonId,
            half.HalfNumber,
            half.Name,
            half.StartDate.ToString("yyyy-MM-dd"),
            half.EndDate.ToString("yyyy-MM-dd"),
            ScoringFormat: half.ScoringFormat.ToWireString(),
            MatchPlayCustomFormula: half.MatchPlayCustomFormula));
    }
}
