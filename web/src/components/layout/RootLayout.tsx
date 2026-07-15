import { Outlet } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { useAuth } from '@/hooks/useAuth';
import { useLeagueName } from '@/context/LeagueContext';
import { usePublicLeagueSettings } from '@/hooks/usePublicLeagueSettings';
import { NavBar } from './NavBar';

export function RootLayout() {
  const { bootstrapping } = useAuth();
  const leagueName = useLeagueName();
  const { data: publicSettings } = usePublicLeagueSettings();
  const whatsAppGroupLink = publicSettings?.whatsAppGroupLink;

  return (
    <div className="min-h-screen bg-gray-50">
      <NavBar />
      <main className="container mx-auto max-w-screen-xl px-4 py-8">
        {bootstrapping ? (
          <div className="flex min-h-[60vh] items-center justify-center">
            <Spinner />
          </div>
        ) : (
          <Outlet />
        )}
      </main>
      <footer className="border-t border-gray-200 bg-white mt-auto">
        <div className="container mx-auto max-w-screen-xl px-4 py-6 text-center text-sm text-gray-400 space-y-2">
          {whatsAppGroupLink && (
            <div>
              <a
                href={whatsAppGroupLink}
                target="_blank"
                rel="noopener noreferrer"
                className="text-[#1B5E20] hover:underline"
              >
                Join our WhatsApp group
              </a>
            </div>
          )}
          <div>
            &copy; {new Date().getFullYear()} {leagueName} &mdash; All rights reserved
          </div>
        </div>
      </footer>
    </div>
  );
}
