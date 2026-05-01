import {
  Configuration,
  PublicClientApplication,
  LogLevel,
  BrowserCacheLocation,
} from '@azure/msal-browser';

const TENANT_ID = '8299a09c-bf4e-4d14-aa8c-13afa3c58965';
const CLIENT_ID = import.meta.env.VITE_ENTRA_CLIENT_ID as string;
const REDIRECT_URI = (import.meta.env.VITE_REDIRECT_URI as string | undefined) ?? 'http://localhost:5173';

// The API scope is on the mobile client registration — used for acquiring tokens
// that the Azure Functions API will validate.
const API_SCOPE = 'api://39dca729-4792-4830-8b72-5441fbe31c2b/access';

export const msalConfig: Configuration = {
  auth: {
    clientId: CLIENT_ID,
    authority: `https://login.microsoftonline.com/${TENANT_ID}/v2.0`,
    redirectUri: REDIRECT_URI,
    postLogoutRedirectUri: REDIRECT_URI,
    navigateToLoginRequestUrl: true,
  },
  cache: {
    cacheLocation: BrowserCacheLocation.LocalStorage,
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      logLevel: import.meta.env.DEV ? LogLevel.Info : LogLevel.Error,
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        switch (level) {
          case LogLevel.Error:
            console.error('[MSAL]', message);
            break;
          case LogLevel.Warning:
            console.warn('[MSAL]', message);
            break;
          case LogLevel.Info:
            console.info('[MSAL]', message);
            break;
          case LogLevel.Verbose:
            console.debug('[MSAL]', message);
            break;
        }
      },
    },
  },
};

export const loginRequest = {
  scopes: ['openid', 'offline_access', API_SCOPE],
};

export const tokenRequest = {
  scopes: [API_SCOPE],
};

export const msalInstance = new PublicClientApplication(msalConfig);
