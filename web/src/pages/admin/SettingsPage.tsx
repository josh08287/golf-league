import { PageHeader } from '../../components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/Card';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { useLeagueSettings, useUpdateLeagueSetting } from '../../hooks/admin/useLeagueSettings';
import { SETTING_KEYS } from '../../types/api';

function Toggle({
  label,
  description,
  checked,
  onChange,
  disabled,
}: {
  label: string;
  description: string;
  checked: boolean;
  onChange: (value: boolean) => void;
  disabled?: boolean;
}) {
  return (
    <div className="flex items-start justify-between gap-6 py-4">
      <div className="min-w-0">
        <p className="text-sm font-medium text-gray-900">{label}</p>
        <p className="mt-0.5 text-sm text-gray-500">{description}</p>
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        disabled={disabled}
        onClick={() => onChange(!checked)}
        className={[
          'relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent',
          'transition-colors duration-200 ease-in-out focus:outline-none focus-visible:ring-2',
          'focus-visible:ring-[#1B5E20] focus-visible:ring-offset-2',
          checked ? 'bg-[#1B5E20]' : 'bg-gray-200',
          disabled ? 'opacity-50 cursor-not-allowed' : '',
        ].join(' ')}
      >
        <span
          className={[
            'pointer-events-none inline-block h-5 w-5 rounded-full bg-white shadow',
            'transform transition duration-200 ease-in-out',
            checked ? 'translate-x-5' : 'translate-x-0',
          ].join(' ')}
        />
      </button>
    </div>
  );
}

export function SettingsPage() {
  const { data: settings, isLoading, isError } = useLeagueSettings();
  const update = useUpdateLeagueSetting();

  function getSetting(key: string): boolean {
    const s = settings?.find((s) => s.key === key);
    return s?.value === 'true';
  }

  function handleToggle(key: string, value: boolean) {
    update.mutate({ key, value: String(value) });
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Settings" subtitle="League configuration" />

      {isLoading && (
        <div className="flex justify-center py-8">
          <Spinner />
        </div>
      )}

      {isError && <ErrorMessage message="Could not load settings." />}

      {settings && (
        <Card>
          <CardHeader>
            <CardTitle>Email Notifications</CardTitle>
          </CardHeader>
          <CardContent className="divide-y divide-gray-100">
            <Toggle
              label="Weekly tee time emails"
              description="After auto-fill runs each Sunday, send every player in the current half an email with the full tee sheet for the upcoming round."
              checked={getSetting(SETTING_KEYS.teeTimeEmailEnabled)}
              onChange={(v) => handleToggle(SETTING_KEYS.teeTimeEmailEnabled, v)}
              disabled={update.isPending}
            />
          </CardContent>
        </Card>
      )}

      {update.isError && (
        <p className="text-sm text-red-600 text-center">Failed to save setting. Please try again.</p>
      )}
    </div>
  );
}
