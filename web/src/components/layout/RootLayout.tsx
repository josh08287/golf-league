import { Outlet } from 'react-router-dom';
import { Spinner } from '@/components/ui/Spinner';
import { useAuth } from '@/hooks/useAuth';
import { NavBar } from './NavBar';

export function RootLayout() {
  const { bootstrapping } = useAuth();

  if (bootstrapping) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Spinner />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50">
      <NavBar />
      <main className="container mx-auto max-w-screen-xl px-4 py-8">
        <Outlet />
      </main>
      <footer className="border-t border-gray-200 bg-white mt-auto">
        <div className="container mx-auto max-w-screen-xl px-4 py-6 text-center text-sm text-gray-400">
          &copy; {new Date().getFullYear()} Golf League &mdash; All rights reserved
        </div>
      </footer>
    </div>
  );
}
