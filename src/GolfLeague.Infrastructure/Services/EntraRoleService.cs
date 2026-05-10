using Azure.Identity;
using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace GolfLeague.Infrastructure.Services;

/// <summary>
/// Implementation of IEntraRoleService that uses Microsoft Graph API to manage app role assignments.
/// </summary>
public sealed class EntraRoleService : IEntraRoleService
{
    private readonly GraphServiceClient _graphClient;
    private readonly string _clientId;
    private readonly ILogger<EntraRoleService> _logger;

    // App role IDs from the app registration
    // These must match the role IDs defined in the Entra ID app registration
    private static readonly Dictionary<string, Guid> RoleIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["admin"] = Guid.Parse("c347a97d-4c6e-43c6-a2f0-fdcd63109b8b"),
        ["scorer"] = Guid.Parse("5c4d68ab-dd63-40cf-a7e4-8bce4eceaed4"),
        ["player"] = Guid.Parse("a12c2a23-36b7-4cd3-822e-340535334ace"),
    };

    public EntraRoleService(IConfiguration configuration, ILogger<EntraRoleService> logger)
    {
        _logger = logger;
        _clientId = configuration["ENTRA_CLIENT_ID"]
            ?? throw new InvalidOperationException("ENTRA_CLIENT_ID is not configured.");
        var tenantId = configuration["ENTRA_TENANT_ID"]
            ?? throw new InvalidOperationException("ENTRA_TENANT_ID is not configured.");

        // Use DefaultAzureCredential which supports managed identity in Azure
        // and Azure CLI / Visual Studio credentials for local dev
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            TenantId = tenantId,
        });

        _graphClient = new GraphServiceClient(credential);
    }

    public async Task<Result<bool>> AssignRoleAsync(
        string userObjectId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!RoleIds.TryGetValue(roleName, out var appRoleId))
            {
                return Result<bool>.Fail($"Unknown role: {roleName}");
            }

            // Get the service principal for this application
            var servicePrincipals = await _graphClient.ServicePrincipals
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{_clientId}'";
                }, cancellationToken);

            var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
            if (servicePrincipal?.Id is null)
            {
                return Result<bool>.Fail("Service principal not found for the application.");
            }

            // Check if the role is already assigned
            var existingAssignments = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                .AppRoleAssignedTo
                .GetAsync(cancellationToken: cancellationToken);

            var alreadyAssigned = existingAssignments?.Value?.Any(a =>
                a.PrincipalId == Guid.Parse(userObjectId) &&
                a.AppRoleId == appRoleId) ?? false;

            if (alreadyAssigned)
            {
                _logger.LogDebug("Role {RoleName} is already assigned to user {UserId}", roleName, userObjectId);
                return Result<bool>.Ok(true);
            }

            // Assign the app role to the user
            var assignment = new AppRoleAssignment
            {
                PrincipalId = Guid.Parse(userObjectId),
                ResourceId = Guid.Parse(servicePrincipal.Id),
                AppRoleId = appRoleId,
            };

            await _graphClient.Users[userObjectId]
                .AppRoleAssignments
                .PostAsync(assignment, cancellationToken: cancellationToken);

            _logger.LogInformation("Assigned role {RoleName} to user {UserId}", roleName, userObjectId);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to assign role {RoleName} to user {UserId}", roleName, userObjectId);
            return Result<bool>.Fail($"Failed to assign role: {ex.Message}");
        }
    }

    public async Task<Result<bool>> RemoveRoleAsync(
        string userObjectId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!RoleIds.TryGetValue(roleName, out var appRoleId))
            {
                return Result<bool>.Fail($"Unknown role: {roleName}");
            }

            // Get the service principal for this application
            var servicePrincipals = await _graphClient.ServicePrincipals
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{_clientId}'";
                }, cancellationToken);

            var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
            if (servicePrincipal?.Id is null)
            {
                return Result<bool>.Fail("Service principal not found for the application.");
            }

            // Find the existing assignment
            var assignments = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                .AppRoleAssignedTo
                .GetAsync(cancellationToken: cancellationToken);

            var assignment = assignments?.Value?.FirstOrDefault(a =>
                a.PrincipalId == Guid.Parse(userObjectId) &&
                a.AppRoleId == appRoleId);

            if (assignment?.Id is null)
            {
                _logger.LogDebug("Role {RoleName} is not assigned to user {UserId}", roleName, userObjectId);
                return Result<bool>.Ok(true);
            }

            // Remove the assignment
            await _graphClient.ServicePrincipals[servicePrincipal.Id]
                .AppRoleAssignedTo[assignment.Id]
                .DeleteAsync(cancellationToken: cancellationToken);

            _logger.LogInformation("Removed role {RoleName} from user {UserId}", roleName, userObjectId);
            return Result<bool>.Ok(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove role {RoleName} from user {UserId}", roleName, userObjectId);
            return Result<bool>.Fail($"Failed to remove role: {ex.Message}");
        }
    }

    public async Task<Result<List<string>>> GetUserRolesAsync(
        string userObjectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the service principal for this application
            var servicePrincipals = await _graphClient.ServicePrincipals
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{_clientId}'";
                }, cancellationToken);

            var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
            if (servicePrincipal?.Id is null)
            {
                return Result<List<string>>.Fail("Service principal not found for the application.");
            }

            // Get all role assignments for this user
            var assignments = await _graphClient.ServicePrincipals[servicePrincipal.Id]
                .AppRoleAssignedTo
                .GetAsync(cancellationToken: cancellationToken);

            var userAssignments = assignments?.Value
                ?.Where(a => a.PrincipalId == Guid.Parse(userObjectId))
                ?.Select(a => a.AppRoleId)
                ?.ToList() ?? new List<Guid?>();

            // Map app role IDs to role names
            var roles = RoleIds
                .Where(r => userAssignments.Contains(r.Value))
                .Select(r => r.Key)
                .ToList();

            return Result<List<string>>.Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get roles for user {UserId}", userObjectId);
            return Result<List<string>>.Fail($"Failed to get roles: {ex.Message}");
        }
    }

    public async Task<Result<string>> EnsureUserExistsAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // First, try to find the user by email
            var users = await _graphClient.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"mail eq '{email}' or userPrincipalName eq '{email}'";
                    config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName"];
                }, cancellationToken);

            var existingUser = users?.Value?.FirstOrDefault();
            if (existingUser?.Id is not null)
            {
                _logger.LogDebug("Found existing user {Email} with ID {UserId}", email, existingUser.Id);
                return Result<string>.Ok(existingUser.Id);
            }

            // If not found by email, try to find by userPrincipalName with common domains
            var upn = email.ToLowerInvariant();
            users = await _graphClient.Users
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"userPrincipalName eq '{upn}'";
                    config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName"];
                }, cancellationToken);

            existingUser = users?.Value?.FirstOrDefault();
            if (existingUser?.Id is not null)
            {
                _logger.LogDebug("Found existing user by UPN {Email} with ID {UserId}", email, existingUser.Id);
                return Result<string>.Ok(existingUser.Id);
            }

            // User doesn't exist - create an invitation
            _logger.LogInformation("User {Email} not found in tenant, creating invitation", email);

            var invitation = new Invitation
            {
                InvitedUserEmailAddress = email,
                InvitedUserDisplayName = displayName,
                InviteRedirectUrl = "https://golf-league.azurewebsites.net", // Main app URL
                SendInvitationMessage = false, // Don't send email, user is already signing up
            };

            var invitedUser = await _graphClient.Invitations
                .PostAsync(invitation, cancellationToken: cancellationToken);

            if (invitedUser?.InvitedUser?.Id is null)
            {
                return Result<string>.Fail("Failed to create invitation for user.");
            }

            _logger.LogInformation(
                "Created invitation for user {Email} with ID {UserId}",
                email,
                invitedUser.InvitedUser.Id);

            return Result<string>.Ok(invitedUser.InvitedUser.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure user exists for {Email}", email);
            return Result<string>.Fail($"Failed to ensure user exists: {ex.Message}");
        }
    }
}
