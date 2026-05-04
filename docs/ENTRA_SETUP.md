# Entra External ID Setup Guide

This document provides step-by-step instructions for setting up **Entra External ID** (Microsoft's Customer Identity and Access Management platform) for the Golf League application.

## Overview

The Golf League uses **Entra External ID** for:
- **Consumer identity**: Authentication for players and admins
- **CIAM (Customer Identity and Access Management)**: Free tier (<50K Monthly Active Users)
- **Standards-based OAuth 2.0 + OIDC**: No proprietary B2C policies or custom domains
- **Multi-platform support**: Web (React/MSAL), mobile (Flutter/flutter_appauth)

## Prerequisites

- Azure subscription with permissions to create Entra External ID resources
- Access to the Azure Portal
- (Optional) A custom domain for your Entra tenant (e.g., `golflms.onmicrosoft.com`)

---

## 1. Create an Entra External ID Tenant

1. **Go to Azure Portal** → [portal.azure.com](https://portal.azure.com)
2. **Search** for "Entra External ID" or navigate to **Azure Active Directory**
3. **Create a new external tenant**:
   - **Tenant name**: `Golf League` (or similar)
   - **Tenant region**: Choose your region
   - **Initial domain name**: `golflms.onmicrosoft.com` (auto-generated; can customize)
4. **Click "Create"** and wait for provisioning (~5 minutes)
5. **Switch to the new tenant** using the directory picker in the top-right corner

---

## 2. Create App Registrations

You need **two app registrations**: one for the **web app** (React + MSAL) and one for the **mobile app** (Flutter + flutter_appauth).

### 2.1 Web App Registration

1. **Navigate** to **App registrations** in the Entra tenant
2. **Select "New registration"**
3. **Configure the web app**:
   - **Name**: `Golf League Web`
   - **Supported account types**: `Accounts in any organizational directory (Any Azure AD directory – Multitenant)`
   - **Redirect URI**: 
     - **Platform**: Web
     - **URI**: `http://localhost:5173/auth/callback` (local dev)
     - Also add: `https://<your-static-web-app-url>/auth/callback` (production)
4. **Register** the application
5. **Note the following from the Overview page**:
   - `Application (client) ID` — save as `VITE_ENTRA_CLIENT_ID`
   - `Directory (tenant) ID` — save as `ENTRA_TENANT_ID`

#### Configure API Scope (Optional but Recommended)

1. **Expose an API**:
   - **Set Application ID URI**: `api://<application-id>` (auto-populated)
   - Click **Save**
2. **Add scope**:
   - **Scope name**: `api`
   - **Admin consent display name**: `Access Golf League API`
   - **Scope description**: `Allows access to Golf League API`
   - **Consent options**: Both `Admin and users` and `Just admins` are fine
3. **Save**

#### Configure Authentication (Optional)

1. **Authentication** → **Advanced settings**
   - Enable **Allow public client flows**: `Yes` (required for mobile/flutter_appauth)

### 2.2 Mobile App Registration

1. **New registration** in the same Entra External ID tenant
2. **Configure the mobile app**:
   - **Name**: `Golf League Mobile`
   - **Supported account types**: Same as web app (multitenant)
   - **Redirect URI**:
     - **Platform**: Mobile and desktop applications
     - **URI**: `com.golfleague.app://auth` (Android & iOS)
3. **Register** the application
4. **Note the Application (client) ID** — use as `clientId` in `mobile/lib/core/config.dart`

#### Configure Authentication for Mobile

1. **Authentication** → **Advanced settings**
   - Enable **Allow public client flows**: `Yes` (required for AppAuth/PKCE)

#### Add API Permission for Mobile

1. **API permissions**
   - **Add a permission** → **My APIs** → Select the **Golf League Web** app
   - **Select scope**: `api`
   - Grant consent (or ask tenant admin to grant tenant-wide consent)

---

## 3. Configure Social Providers (Optional)

To allow users to sign up with social accounts (Google, Microsoft, etc.):

### 3.1 Google Provider

1. **Go to Google Cloud Console** → [console.cloud.google.com](https://console.cloud.google.com)
2. **Create a new project** named `Golf League`
3. **Enable OAuth 2.0**:
   - Search for **"OAuth consent screen"** in APIs & Services
   - Configure external user type
   - Add required scopes: `email`, `profile`, `openid`
4. **Create OAuth 2.0 credentials**:
   - **Application type**: Web application
   - **Name**: `Golf League Entra`
   - **Authorized redirect URIs**:
     - `https://<your-entra-tenant>.ciamlogin.com/te/common/oauth2/authresp`
     - Replace `<your-entra-tenant>` with your tenant subdomain (e.g., `golflms`)
5. **Download JSON** credentials; note the **Client ID** and **Client Secret**

### 3.2 Add Google to Entra External ID

1. **In Entra External ID tenant** → **Settings** → **External Identity Providers** (or **Identity Providers** under **Authentication methods**)
2. **Add Google**:
   - **Display name**: `Google`
   - **Client ID**: From Google Cloud (step 4.5 above)
   - **Client secret**: From Google Cloud credentials
   - **Scope**: `openid profile email`
3. **Save** and note the **provider name** (e.g., `google-oauth2`)

### 3.3 Microsoft Provider (Optional)

1. Similar process: create an Azure App Registration for OpenID Connect
2. Redirect URI: `https://<your-entra-tenant>.ciamlogin.com/te/common/oauth2/authresp`
3. Scopes: `openid profile email`
4. Add to Entra External ID as above

---

## 4. Configure Conditional Access & Policies

### 4.1 Enable Sign-Up and Sign-In User Flow

1. **User flows** (or **Authentication** > **User flows**)
2. **Create new** → **Sign up and sign in**
3. **Name**: `susi` (common naming convention)
4. **Identity providers**: Select **Email** (or **Email + Google** if configured above)
5. **User attributes to collect**:
   - `Email Address` (required)
   - `Display Name`
   - `Given Name`
   - `Surname`
   - `Phone Number` (optional)
6. **Page layout**: Default or customize as needed
7. **Create** the flow

### 4.2 Create Custom Attribute for PlayerId (Optional)

If you want to pre-link Entra users to player records:

1. **Custom attributes** under **User attributes**
2. **Create custom attribute**:
   - **Name**: `playerId` (or `extension_PlayerId`)
   - **Type**: Integer
   - **Description**: `Golf League Player ID`
3. This can be set programmatically after approval; see the Golf League backend for integration points

---

## 5. Deploy Configuration to Azure

### 5.1 Azure Functions Environment Variables

Set in your **Azure Functions** app settings:

```
ENTRA_TENANT_ID = <Directory (tenant) ID from app registration>
ENTRA_CLIENT_ID = <Application (client) ID from app registration>
```

These are read by `Program.cs` in `GolfLeague.Functions` to configure JWT bearer validation.

### 5.2 Static Web App (SWA) Configuration

1. **Navigate to Static Web App** → **Configuration** → **Settings**
2. Set environment variables (if using CI/CD):
   ```
   VITE_ENTRA_CLIENT_ID = <Application (client) ID>
   VITE_ENTRA_TENANT_ID = <Directory (tenant) ID>
   VITE_REDIRECT_URI = https://<your-swa-url>/auth/callback
   ```
3. Or hard-code in `web/src/lib/msalConfig.ts` during build

### 5.3 Mobile App Configuration

Update `mobile/lib/core/config.dart`:

```dart
const _tenantId = '<Directory (tenant) ID>';
const _clientId = '<Application (client) ID from mobile registration>';
const _redirectUri = 'com.golfleague.app://auth';
const _discoveryUrl =
    'https://login.microsoftonline.com/$_tenantId/v2.0/.well-known/openid-configuration';
```

---

## 6. Testing

### 6.1 Test Web App Locally

1. Start the React dev server:
   ```bash
   cd web
   npm install
   npm run dev
   ```
2. Navigate to `http://localhost:5173`
3. Click "Login" → redirect to Entra
4. Sign in (create test account if needed)
5. Should redirect to **RegisterPage** (pending approval)

### 6.2 Test Mobile App

1. Build and deploy the Flutter app to an emulator or device
2. Tap the login button
3. flutter_appauth should launch the system browser → Entra login
4. After login, should show **PendingApprovalScreen**

### 6.3 Admin Approval Flow

1. **Log in as admin** in the web app (use an account with `admin` role)
2. **Navigate to Admin** → **Players** → **Join Requests**
3. **Click Approve** on a pending request
4. New player record is created; admin can now assign handicap and flight
5. **Refresh the public app** (as the approved user) — should now see the dashboard

---

## 7. Security Best Practices

1. **Never commit secrets** to version control
   - Use `.env.local` for local dev
   - Use Azure Key Vault or GitHub Secrets for CI/CD

2. **Enable Multi-Factor Authentication (MFA)**
   - In Entra External ID → **Authentication methods** → enable SMS, TOTP, etc.

3. **Use PKCE** (already configured in flutter_appauth and MSAL)
   - Prevents authorization code interception on mobile

4. **Validate JWT tokens** in backend (already done in `Program.cs`)

5. **Use HTTPS everywhere** in production

6. **Rotate secrets regularly**
   - Google Client Secret, app registration credentials, etc.

7. **Review audit logs** periodically
   - **Entra External ID** → **Audit logs**

---

## 8. Troubleshooting

### "Invalid scope" Error

- Ensure the **API scope** is configured in the web app registration
- Verify the `scopes` in `web/src/lib/msalConfig.ts` match the exposed scope

### Redirect URI Mismatch

- Check that the redirect URI in Entra matches the one in your app config
- Local: `http://localhost:5173/auth/callback` vs Production: `https://<swa-url>/auth/callback`

### Token Validation Fails

- Verify `ENTRA_TENANT_ID` and `ENTRA_CLIENT_ID` are set correctly in Azure Functions
- Check JWT bearer configuration in `Program.cs`

### Social Provider Not Showing

- Ensure the user flow includes the social provider
- Verify the social provider credentials (Google Client ID/Secret, etc.)

---

## 9. References

- **Entra External ID Documentation**: https://learn.microsoft.com/en-us/entra/external-id/
- **MSAL.js Configuration**: https://learn.microsoft.com/en-us/entra/identity-platform/msal-js-initializing-client-applications
- **flutter_appauth**: https://pub.dev/packages/flutter_appauth
- **OAuth 2.0 / OIDC Specs**: https://openid.net/specs/

---

**Last Updated**: May 2026  
**Maintained By**: Golf League Development Team
