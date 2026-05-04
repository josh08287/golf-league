using GolfLeague.Application.Common;
using GolfLeague.Application.DTOs;
using GolfLeague.Domain.Enums;
using GolfLeague.Domain.Interfaces;
using MediatR;

namespace GolfLeague.Application.Registrations.Queries;

public sealed record GetPendingRegistrationsQuery : IRequest<Result<List<RegistrationDto>>>;

public sealed class GetPendingRegistrationsQueryHandler
    : IRequestHandler<GetPendingRegistrationsQuery, Result<List<RegistrationDto>>>
{
    private readonly IRegistrationRepository _repo;

    public GetPendingRegistrationsQueryHandler(IRegistrationRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<List<RegistrationDto>>> Handle(
        GetPendingRegistrationsQuery request,
        CancellationToken cancellationToken)
    {
        var registrations = await _repo.GetByStatusAsync(RegistrationStatus.Pending, cancellationToken);
        var dtos = registrations.Select(ToDto).ToList();
        return Result<List<RegistrationDto>>.Ok(dtos);
    }

    internal static RegistrationDto ToDto(Domain.Entities.PlayerRegistration r) =>
        new(r.Id, r.EntraObjectId, r.FirstName, r.LastName, r.Email,
            r.Phone, r.Status.ToString(), r.RequestedAt, r.ReviewedAt,
            r.RejectionReason, r.PlayerId);
}
