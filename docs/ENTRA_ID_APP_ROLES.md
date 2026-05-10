# Entra ID App Roles Configuration

This document explains how to configure Entra ID (Azure AD) app roles for the Golf League application.

## Overview

The application now uses Entra ID app roles as the source of truth for authorization. Roles are:
- **Assigned in Entra ID** via Microsoft Graph API when admins update player roles or invites are accepted
- **Read from the JWT token** (`roles` claim) when users authenticate
- **Stored in the database** for reference only (not used for authorization)

## Required Configuration

### 1. App Registration Setup

In your Entra ID (Azure AD) app registration, you must define the app roles:

1. Go to **Azure Portal** → **Entra ID** → **App registrations** → Your app
2. Click **App roles** in the left menu
3. Click **Create app role** and create three roles:

| Display Name | Value | Description | Allowed member types |
|--------------|-------|-------------|---------------------|
| Admin | admin | Full administrative access | Users/Groups |
| Scorer | scorer | Can enter scores | Users/Groups |
| Player | player | Regular player access | Users/Groups |

### 2. Get the Role IDs

After creating the roles, note the **Object ID** for each role. You'll need to update the `EntraRoleService.cs` file with these IDs:

```csharp
// In: src/GolfLeague.Infrastructure/Services/EntraRoleService.cs
private static readonly Dictionary<string, Guid> RoleIds = new(StringComparer.OrdinalIgnoreCase)
{
    ["admin"] = Guid.Parse("YOUR-ADMIN-ROLE-OBJECT-ID"),
    ["scorer"] = Guid.Parse("YOUR-SCORER-ROLE-OBJECT-ID"),
    ["player"] = Guid.Parse("YOUR-PLAYER-ROLE-OBJECT-ID"),
};
```

To find the role IDs:
1. Go to your app registration
2. Click **Manifest**
3. Look for the `appRoles` array - each role has an `id` field (GUID format)

### 3. Service Principal Permissions

The application uses Microsoft Graph API to assign roles. The service principal (managed identity or app registration) needs permissions:

**Required Microsoft Graph API Permissions:**
- `AppRoleAssignment.ReadWrite.All` (to assign app roles to users)
- `User.Read.All` (to read user information)

**For local development:**
Use Azure CLI login:
```bash
az login
az account set --subscription YOUR_SUBSCRIPTION_ID
```

**For Azure deployment:**
Assign a **Managed Identity** to your Azure Function and grant it the Microsoft Graph API permissions via **Microsoft Entra admin center**.

### 4. Environment Variables

Ensure these are set (they were already required for authentication):

```
ENTRA_TENANT_ID=your-tenant-id
ENTRA_CLIENT_ID=your-app-registration-client-id
```

## How It Works

### Authentication Flow

1. User signs in via MSAL (web) or MSAL Flutter (mobile)
2. Entra ID issues a JWT token containing the user's app roles in the `roles` claim
3. The `AuthMiddleware` reads the `roles` claim from the token (no database lookup for roles)
4. `RequireRole("admin")` checks `user.IsInRole("admin")` which uses the token's roles

### Role Assignment Flow

1. Admin updates a player's role via `PUT /v1/players/{id}` with `role` field
2. `UpdatePlayerCommand` calls `IEntraRoleService.AssignRoleAsync()`
3. `EntraRoleService` uses Microsoft Graph API to assign the app role in Entra ID
4. On success, the database is updated for reference
5. On next sign-in, the user receives a new token with the updated roles

### Invite Acceptance Flow

1. User accepts invite via `POST /v1/invites/{token}/accept`
2. `AcceptInviteCommand` creates the player record
3. It attempts to assign the role in Entra ID (logs warning if it fails)
4. The user may need to sign out and back in to get the new role in their token

## Troubleshooting

### "Service principal not found for the application"

This means the service principal hasn't been created in your tenant. Create it by:
1. Going to **Enterprise applications** in Azure Portal
2. If your app isn't listed, a Global Admin needs to consent to the app first

### "Insufficient privileges" when assigning roles

The application (managed identity or service principal) needs the Microsoft Graph permissions granted and consented by an admin.

### Admin can't access admin endpoints after changes

1. Check the token contains the `roles` claim using [jwt.ms](https://jwt.ms)
2. Ensure `RoleClaimType = "roles"` is set in `Program.cs` (done in this PR)
3. Ensure `AuthMiddleware` is not overriding roles from database (fixed in this PR)

## Manual Role Assignment (Fallback)

If the Graph API integration isn't working, admins can manually assign roles:

1. Go to **Entra ID** → **Enterprise applications** → Your app
2. Click **Users and groups**
3. Click **Add user/group**
4. Select the user and assign the appropriate role

## Migration from Database Roles

If you have existing players with database roles:

1. The database role will be used as a reference (display purposes)
2. You need to manually assign the corresponding Entra ID app role to each user
3. After all users have Entra ID roles assigned, you can optionally remove the `Role` column from the database
