import { useState } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { useLeagueSettings, useUpdateLeagueSetting } from '../../hooks/admin/useLeagueSettings';
import { useFeatureFlags, useUpdateFeatureFlag } from '../../hooks/admin/useFeatureFlags';
import { useAuthStore } from '../../store/authStore';
import { SETTING_KEYS, FEATURE_FLAG_KEYS } from '../../types/api';

/** Global feature flags surfaced to super-admins, in display order. */
const FEATURE_FLAG_DEFS: { key: string; label: string; description: string }[] = [
  {
    key: FEATURE_FLAG_KEYS.selfSkipRoundsEnabled,
    label: 'Player self-skip on profile page',
    description:
      'Lets players skip or unskip their own upcoming rounds directly from their player profile page. Applies to every league.',
  },
  {
    key: FEATURE_FLAG_KEYS.closestToPinEnabled,
    label: 'Closest-to-the-pin tracking',
    description:
      'Lets scorers and admins record which player was closest to the pin on each par 3 from the score entry screen, with wins shown on the statistics page. Applies to every league.',
  },
  {
    key: FEATURE_FLAG_KEYS.resendTeeTimeEmailEnabled,
    label: 'Re-send tee time email button',
    description:
      'Adds a button on the admin tee-times page to manually re-send the weekly tee time schedule email for a round. Sends still respect each league’s email setting and player opt-outs. Applies to every league.',
  },
  {
    key: FEATURE_FLAG_KEYS.activeRoundLeaderboardEnabled,
    label: 'Active round leaderboard',
    description:
      'Shows a live "Leaderboard" nav link and page, grouped by flight, while a round is currently in progress. Applies to every league.',
  },
  {
    key: FEATURE_FLAG_KEYS.roundDayTeeTimeSwitchEnabled,
    label: 'Round-day tee-time group switch',
    description:
      'Lets an already-assigned player move themselves to a different, open tee-time group on the day of their round, bypassing the normal sign-up cutoff for that one action. Applies to every league.',
  },
];

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
  const isSuperAdmin = useAuthStore((s) => s.user?.isSuperAdmin ?? false);
  const { data: featureFlags, isLoading: flagsLoading, isError: flagsError } = useFeatureFlags(isSuperAdmin);
  const updateFlag = useUpdateFeatureFlag();

  function getSetting(key: string): boolean {
    const s = settings?.find((s) => s.key === key);
    return s?.value === 'true';
  }

  function getNumericSetting(key: string, fallback: number): number {
    const s = settings?.find((s) => s.key === key);
    const parsed = parseInt(s?.value ?? '', 10);
    return isNaN(parsed) ? fallback : parsed;
  }

  function handleToggle(key: string, value: boolean) {
    update.mutate({ key, value: String(value) });
  }

  const dropCountValue = getNumericSetting(SETTING_KEYS.standingsDropCount, 1);
  const [dropCountInput, setDropCountInput] = useState<string>('');
  const [dropCountInitialized, setDropCountInitialized] = useState(false);

  if (settings && !dropCountInitialized) {
    setDropCountInput(String(dropCountValue));
    setDropCountInitialized(true);
  }

  const dropCountParsed = parseInt(dropCountInput, 10);
  const dropCountValid = !isNaN(dropCountParsed) && dropCountParsed >= 0;
  const dropCountDirty = dropCountValid && dropCountParsed !== dropCountValue;

  function handleSaveDropCount() {
    if (dropCountValid) {
      update.mutate({ key: SETTING_KEYS.standingsDropCount, value: String(dropCountParsed) });
    }
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
        <>
          <Card>
            <CardHeader>
              <CardTitle>Standings</CardTitle>
            </CardHeader>
            <CardContent className="divide-y divide-gray-100">
              <div className="flex items-start justify-between gap-6 py-4">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900">Rounds to drop</p>
                  <p className="mt-0.5 text-sm text-gray-500">
                    Number of each player's lowest-scoring rounds excluded from their standings total and average. Set to 0 to count all rounds.
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <input
                    type="number"
                    min={0}
                    value={dropCountInput}
                    onChange={(e) => setDropCountInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') handleSaveDropCount(); }}
                    disabled={update.isPending}
                    className="w-20 rounded-md border border-gray-300 px-3 py-1.5 text-sm text-center focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                  />
                  <Button
                    size="sm"
                    onClick={handleSaveDropCount}
                    disabled={!dropCountDirty || update.isPending}
                  >
                    Save
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>

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

          {isSuperAdmin && (
            <Card>
              <CardHeader>
                <CardTitle>Feature Flags</CardTitle>
              </CardHeader>
              <CardContent className="divide-y divide-gray-100">
                {flagsLoading && (
                  <div className="flex justify-center py-4">
                    <Spinner />
                  </div>
                )}
                {flagsError && <ErrorMessage message="Could not load feature flags." />}
                {featureFlags &&
                  FEATURE_FLAG_DEFS.map((def) => (
                    <Toggle
                      key={def.key}
                      label={def.label}
                      description={def.description}
                      checked={featureFlags.find((f) => f.key === def.key)?.enabled ?? false}
                      onChange={(v) => updateFlag.mutate({ key: def.key, enabled: v })}
                      disabled={updateFlag.isPending}
                    />
                  ))}
              </CardContent>
            </Card>
          )}
        </>
      )}

      {update.isError && (
        <p className="text-sm text-red-600 text-center">Failed to save setting. Please try again.</p>
      )}
      {updateFlag.isError && (
        <p className="text-sm text-red-600 text-center">Failed to save feature flag. Please try again.</p>
      )}
    </div>
  );
}
