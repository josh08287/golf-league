import { Outlet } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { GolfLogo } from '@/components/ui/GolfLogo';
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
    <div className="min-h-screen flex flex-col bg-[#F7F6F2] text-gray-900">
      <NavBar />
      <main className="container mx-auto max-w-screen-xl flex-1 px-4 sm:px-6 py-7">
        {bootstrapping ? (
          <div className="flex min-h-[60vh] items-center justify-center">
            <Spinner />
          </div>
        ) : (
          <Outlet />
        )}
      </main>
      <footer className="border-t border-gray-200/80 bg-[#F2EFE9]/70 mt-16">
        <div className="container mx-auto max-w-screen-xl px-4 sm:px-6 py-8 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-gray-500">
          <div className="flex items-center gap-2.5">
            <span className="inline-flex h-6 w-6 items-center justify-center rounded-sm bg-primary-900">
              <GolfLogo size="xs" />
            </span>
            <span className="font-serif font-semibold text-gray-800">{leagueName}</span>
            <span>&middot;</span>
            <span>&copy; {new Date().getFullYear()} All rights reserved</span>
          </div>
          {whatsAppGroupLink && (
            <a
              href={whatsAppGroupLink}
              target="_blank"
              rel="noopener noreferrer"
              className="inline-flex items-center gap-1.5 rounded border border-gray-200 bg-white px-3 py-1.5 font-medium text-primary-900 hover:border-primary-700 transition-colors"
            >
              Join our WhatsApp Clubhouse &rarr;
            </a>
          )}
        </div>
      </footer>
    </div>
  );
}
