# Entra External ID — Social Identity Provider Setup

## Overview

The app uses Microsoft Entra External ID as its identity platform. Social sign-in (Google and Apple)
is configured at the Entra tenant level — no code changes are needed once these steps are complete.

---

## Google Sign-In

### 1. Create a Google OAuth Client

1. Go to [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials
2. Click **Create Credentials** → **OAuth Client ID**
3. Application type: **Web application**
4. Authorised redirect URIs — add:
   ```
   https://login.microsoftonline.com/te/{YOUR_TENANT_ID}/oauth2/authresp
   ```
   Replace `{YOUR_TENANT_ID}` with your Entra External ID tenant ID (`8299a09c-...`).
5. Save. Copy the **Client ID** and **Client Secret**.

### 2. Add Google as an Identity Provider in Entra

1. Sign in to the [Azure Portal](https://portal.azure.com)
2. Navigate to **Entra External ID** → your tenant → **External Identities** → **All identity providers**
3. Select **Google**
4. Enter the **Client ID** and **Client Secret** from step 1
5. Click **Save**

### 3. Enable Google in the User Flow

1. In Entra External ID → **User flows** → select your sign-in flow
2. Click **Identity providers**
3. Check **Google** and save

---

## Apple Sign-In

### 1. Create an Apple App ID and Services ID

1. Sign in to [Apple Developer](https://developer.apple.com) → Certificates, Identifiers & Profiles
2. Create a new **App ID** (or use the existing `com.golfleague.app`):
   - Enable **Sign In with Apple** capability
3. Create a new **Services ID**:
   - Identifier: e.g. `com.golfleague.app.web`
   - Enable **Sign In with Apple** → Configure
   - Domains: `login.microsoftonline.com`
   - Return URLs:
     ```
     https://login.microsoftonline.com/te/{YOUR_TENANT_ID}/oauth2/authresp
     ```
4. Create a **Key** with **Sign In with Apple** enabled → download the `.p8` file
   - Note the **Key ID** and your **Team ID**

### 2. Add Apple as an Identity Provider in Entra

1. Azure Portal → **Entra External ID** → **All identity providers**
2. Select **Apple**
3. Fill in:
   - **Client ID**: the Services ID (e.g. `com.golfleague.app.web`)
   - **Team ID**: from Apple Developer
   - **Key ID**: from the key you created
   - **Private key**: contents of the `.p8` file
4. Click **Save**

### 3. Enable Apple in the User Flow

Same as Google step 3 — check **Apple** in the user flow's identity providers.

---

## After Setup

Once both providers are configured and enabled in the user flow, the Entra sign-in page will show
"Sign in with Google" and "Sign in with Apple" buttons alongside the default email/password option.
No changes are needed to the app code — `flutter_appauth` and MSAL.js both discover available
providers from the Entra discovery endpoint automatically.

---

## Invite Link Flow

The invite link (`/accept-invite?token=...`) works as follows:
1. Admin sends invite via `/admin/invites` — generates a unique token, stores it with the invited email and an expiry date
2. Invited person clicks the link → lands on `AcceptInvitePage`
3. If not signed in, they are prompted to sign in with Google/Apple
4. After signing in, they confirm their name/phone and click **Join**
5. The API validates the token, creates their `Player` record, marks the invite accepted
6. Admin can then set their handicap and flight assignment in `/admin/players/:id`
