using Azure.Identity;
using GolfLeague.Application.Common;
using GolfLeague.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

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

            _logger.LogInformation("Assigning role {RoleName} (ID: {AppRoleId}) to user {UserId}. App ID: {AppId}",
                roleName, appRoleId, userObjectId, _clientId);

            // Get the service principal for this application
            var servicePrincipals = await _graphClient.ServicePrincipals
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = $"appId eq '{_clientId}'";
                }, cancellationToken);

            var servicePrincipal = servicePrincipals?.Value?.FirstOrDefault();
            if (servicePrincipal?.Id is null)
            {
                _logger.LogError("Service principal not found for application {AppId}. " +
                    "Ensure the managed identity has Directory.Read.All permission.", _clientId);
                return Result<bool>.Fail("Service principal not found for the application. Check Graph API permissions.");
            }

            _logger.LogDebug("Found service principal {SpId} for app {AppId}", servicePrincipal.Id, _clientId);

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

            // For external users (Google, etc.), the user may not be immediately available
            // in the directory after sign-in due to eventual consistency.
            // Retry with a small delay to allow the user to be synced.
            const int maxRetries = 3;
            const int delayMs = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // First verify the user exists in the tenant
                    var user = await _graphClient.Users[userObjectId]
                        .GetAsync(cancellationToken: cancellationToken);

                    if (user?.Id is null)
                    {
                        _logger.LogWarning("User {UserId} not found in tenant on attempt {Attempt}", userObjectId, attempt);
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(delayMs, cancellationToken);
                            continue;
                        }
                        return Result<bool>.Fail("User not found in Entra ID tenant.");
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

                    _logger.LogInformation("Assigned role {RoleName} to user {UserId} on attempt {Attempt}",
                        roleName, userObjectId, attempt);
                    return Result<bool>.Ok(true);
                }
                catch (ODataError ex) when (ex.ResponseStatusCode == 404)
                {
                    _logger.LogWarning("User {UserId} not found on attempt {Attempt} (404), retrying...",
                        userObjectId, attempt);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    else
                    {
                        return Result<bool>.Fail($"User not found in Entra ID after {maxRetries} attempts.");
                    }
                }
                catch (ODataError ex) when (ex.ResponseStatusCode == 403)
                {
                    _logger.LogError(ex, "Permission denied when assigning role. " +
                        "Ensure the managed identity has AppRoleAssignment.ReadWrite.All permission. " +
                        "Error: {Error}", ex.Error?.Message);
                    return Result<bool>.Fail(
                        "Permission denied: The application does not have permission to assign app roles. " +
                        "Grant AppRoleAssignment.ReadWrite.All to the managed identity.");
                }
            }

            return Result<bool>.Fail("Failed to assign role after multiple attempts.");
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
        string? entraObjectId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If we have the Entra Object ID (from JWT token), try to get the user directly
            // This is the most reliable way for users who just signed in via Google federation
            if (!string.IsNullOrEmpty(entraObjectId))
            {
                try
                {
                    var userById = await _graphClient.Users[entraObjectId]
                        .GetAsync(config =>
                        {
                            config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName"];
                        }, cancellationToken);

                    if (userById?.Id is not null)
                    {
                        _logger.LogDebug("Found existing user by Object ID {UserId}", entraObjectId);
                        return Result<string>.Ok(userById.Id);
                    }
                }
                catch (ODataError ex) when (ex.ResponseStatusCode == 404)
                {
                    _logger.LogDebug("User with Object ID {UserId} not found directly, trying email lookup", entraObjectId);
                }
            }

            // Try to find the user by email (with retries for eventual consistency)
            const int maxRetries = 3;
            const int delayMs = 2000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                // Try by mail or userPrincipalName
                var users = await _graphClient.Users
                    .GetAsync(config =>
                    {
                        config.QueryParameters.Filter = $"mail eq '{email}' or userPrincipalName eq '{email}'";
                        config.QueryParameters.Select = ["id", "displayName", "mail", "userPrincipalName"];
                    }, cancellationToken);

                var existingUser = users?.Value?.FirstOrDefault();
                if (existingUser?.Id is not null)
                {
                    _logger.LogDebug("Found existing user {Email} with ID {UserId} on attempt {Attempt}",
                        email, existingUser.Id, attempt);
                    return Result<string>.Ok(existingUser.Id);
                }

                // If we have the entraObjectId, the user should exist but might not be queryable yet
                // Wait and retry for eventual consistency
                if (!string.IsNullOrEmpty(entraObjectId) && attempt < maxRetries)
                {
                    _logger.LogDebug("User not found by email on attempt {Attempt}, waiting for eventual consistency...", attempt);
                    await Task.Delay(delayMs, cancellationToken);
                }
                else if (attempt < maxRetries)
                {
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            // User doesn't exist - this shouldn't happen for Google federation users
            // but if it does, log an error rather than creating an invitation
            _logger.LogError(
                "User {Email} with Object ID {UserId} not found in tenant after {MaxRetries} attempts. " +
                "This may indicate the external identity provider (Google) federation is not properly configured.",
                email,
                entraObjectId ?? "unknown",
                maxRetries);

            return Result<string>.Fail(
                $"User not found in Entra ID tenant. If using Google sign-in, ensure the external identity provider is properly configured.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure user exists for {Email}", email);
            return Result<string>.Fail($"Failed to ensure user exists: {ex.Message}");
        }
    }
}
