import React from 'react';
import ReactDOM from 'react-dom/client';
import { MsalProvider } from '@azure/msal-react';
import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { msalInstance } from '@/lib/msalConfig';
import { queryClient } from '@/lib/queryClient';
import App from './App';
import './index.css';

// Initialise MSAL before rendering so handleRedirectPromise runs first.
msalInstance.initialize().then(() => {
  const root = document.getElementById('root');
  if (!root) throw new Error('Root element not found');

  ReactDOM.createRoot(root).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <QueryClientProvider client={queryClient}>
          <App />
          {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
        </QueryClientProvider>
      </MsalProvider>
    </React.StrictMode>,
  );
}).catch(console.error);
