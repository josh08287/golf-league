import { useState } from 'react';
import { PageHeader } from '../../components/ui/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '../../components/ui/Card';
import { Button } from '../../components/ui/Button';
import { Spinner } from '../../components/ui/Spinner';
import { ErrorMessage } from '../../components/ui/ErrorMessage';
import { useLeagueSettings, useUpdateLeagueSetting, settingErrorMessage } from '../../hooks/admin/useLeagueSettings';
import { useFeatureFlags, useUpdateFeatureFlag } from '../../hooks/admin/useFeatureFlags';
import { useAuthStore } from '../../store/authStore';
import { SETTING_KEYS, FEATURE_FLAG_KEYS, HANDICAP_CALC_MODES } from '../../types/api';
import { localTimeToEastern, easternTimeToLocal, localTimeZoneAbbreviation } from '../../lib/utils';

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
  {
    key: FEATURE_FLAG_KEYS.scorecardOcrEnabled,
    label: 'Scan scorecard (OCR)',
    description:
      'Lets a player photograph a completed paper scorecard on the group score entry screen to pre-fill hole scores via OCR, which they then confirm or edit before submitting. Requires Azure Document Intelligence to be configured — the option quietly does nothing if it is not. Applies to every league.',
  },
  {
    key: FEATURE_FLAG_KEYS.joesVsOthersEnabled,
    label: 'Joes vs Non-Joes statistics',
    description:
      'Adds a "Joes vs Non-Joes" page reachable from the Statistics page, comparing aggregate stats for every player named Joe or Joseph against everyone else. Applies to every league.',
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

  const TIME_PATTERN = /^([01]\d|2[0-3]):([0-5]\d)$/;
  function getTimeSetting(key: string, fallback: string): string {
    const s = settings?.find((s) => s.key === key);
    return s?.value && TIME_PATTERN.test(s.value) ? s.value : fallback;
  }

  // Stored/interpreted by the backend as US/Eastern; displayed and edited
  // here in the admin's own browser time zone for clarity.
  const cutoffTimeEastern = getTimeSetting(SETTING_KEYS.teeTimeCutoffTime, '18:00');
  const cutoffTimeLocalValue = easternTimeToLocal(cutoffTimeEastern);
  const [cutoffTimeInput, setCutoffTimeInput] = useState<string>('');
  const [cutoffTimeInitialized, setCutoffTimeInitialized] = useState(false);

  if (settings && !cutoffTimeInitialized) {
    setCutoffTimeInput(cutoffTimeLocalValue);
    setCutoffTimeInitialized(true);
  }

  const cutoffTimeValid = TIME_PATTERN.test(cutoffTimeInput);
  const cutoffTimeDirty = cutoffTimeValid && cutoffTimeInput !== cutoffTimeLocalValue;

  function handleSaveCutoffTime() {
    if (cutoffTimeValid) {
      update.mutate({ key: SETTING_KEYS.teeTimeCutoffTime, value: localTimeToEastern(cutoffTimeInput) });
    }
  }

  const roundCostValue = getNumericSetting(SETTING_KEYS.roundCost, 20);
  const [roundCostInput, setRoundCostInput] = useState<string>('');
  const [roundCostInitialized, setRoundCostInitialized] = useState(false);

  if (settings && !roundCostInitialized) {
    setRoundCostInput(String(roundCostValue));
    setRoundCostInitialized(true);
  }

  const roundCostParsed = parseInt(roundCostInput, 10);
  const roundCostValid = !isNaN(roundCostParsed) && roundCostParsed >= 0;
  const roundCostDirty = roundCostValid && roundCostParsed !== roundCostValue;

  function handleSaveRoundCost() {
    if (roundCostValid) {
      update.mutate({ key: SETTING_KEYS.roundCost, value: String(roundCostParsed) });
    }
  }

  const whatsAppLinkValue = settings?.find((s) => s.key === SETTING_KEYS.whatsAppGroupLink)?.value ?? '';
  const [whatsAppLinkInput, setWhatsAppLinkInput] = useState<string>('');
  const [whatsAppLinkInitialized, setWhatsAppLinkInitialized] = useState(false);

  if (settings && !whatsAppLinkInitialized) {
    setWhatsAppLinkInput(whatsAppLinkValue);
    setWhatsAppLinkInitialized(true);
  }

  const whatsAppLinkDirty = whatsAppLinkInput.trim() !== whatsAppLinkValue;

  function handleSaveWhatsAppLink() {
    update.mutate({ key: SETTING_KEYS.whatsAppGroupLink, value: whatsAppLinkInput.trim() });
  }

  // ── Handicap calculation ──────────────────────────────────────────────────

  const handicapModeValue = settings?.find((s) => s.key === SETTING_KEYS.handicapCalcMode)?.value ?? HANDICAP_CALC_MODES.usga;
  const [handicapModeInput, setHandicapModeInput] = useState<string>(HANDICAP_CALC_MODES.usga);
  const [handicapModeInitialized, setHandicapModeInitialized] = useState(false);

  if (settings && !handicapModeInitialized) {
    setHandicapModeInput(handicapModeValue);
    setHandicapModeInitialized(true);
  }

  const windowXValue = getNumericSetting(SETTING_KEYS.handicapWindowX, 5);
  const windowYValue = getNumericSetting(SETTING_KEYS.handicapWindowY, 5);
  const [windowXInput, setWindowXInput] = useState<string>('');
  const [windowYInput, setWindowYInput] = useState<string>('');
  const [windowInitialized, setWindowInitialized] = useState(false);

  if (settings && !windowInitialized) {
    setWindowXInput(String(windowXValue));
    setWindowYInput(String(windowYValue));
    setWindowInitialized(true);
  }

  const formulaValue = settings?.find((s) => s.key === SETTING_KEYS.handicapCustomFormula)?.value ?? '';
  const [formulaInput, setFormulaInput] = useState<string>('');
  const [formulaInitialized, setFormulaInitialized] = useState(false);

  if (settings && !formulaInitialized) {
    setFormulaInput(formulaValue);
    setFormulaInitialized(true);
  }

  const windowXParsed = parseInt(windowXInput, 10);
  const windowYParsed = parseInt(windowYInput, 10);
  const windowValid = !isNaN(windowXParsed) && windowXParsed >= 1 && !isNaN(windowYParsed) && windowYParsed >= 1;
  const windowXExceedsY = windowValid && windowXParsed > windowYParsed;

  const windowDirty = windowValid && (windowXParsed !== windowXValue || windowYParsed !== windowYValue);
  const formulaDirty = formulaInput.trim() !== formulaValue;

  function handleSaveHandicapMode(mode: string) {
    setHandicapModeInput(mode);
    update.mutate({ key: SETTING_KEYS.handicapCalcMode, value: mode });
  }

  function handleSaveWindow() {
    if (!windowValid) return;
    update.mutate({ key: SETTING_KEYS.handicapWindowX, value: String(windowXParsed) });
    update.mutate({ key: SETTING_KEYS.handicapWindowY, value: String(windowYParsed) });
  }

  function handleSaveFormula() {
    update.mutate({ key: SETTING_KEYS.handicapCustomFormula, value: formulaInput.trim() });
  }

  const formulaError =
    update.isError && update.variables?.key === SETTING_KEYS.handicapCustomFormula
      ? settingErrorMessage(update.error)
      : undefined;

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
              <CardTitle>Handicap Calculation</CardTitle>
            </CardHeader>
            <CardContent className="divide-y divide-gray-100">
              <div className="py-4">
                <p className="text-sm font-medium text-gray-900">Differential formula</p>
                <p className="mt-0.5 text-sm text-gray-500">
                  How each round's 9-hole score differential is computed before it's averaged into a player's handicap.
                </p>
                <div className="mt-3 space-y-2">
                  <label className="flex items-start gap-2 text-sm">
                    <input
                      type="radio"
                      name="handicap-mode"
                      className="mt-0.5"
                      checked={handicapModeInput === HANDICAP_CALC_MODES.usga}
                      onChange={() => handleSaveHandicapMode(HANDICAP_CALC_MODES.usga)}
                      disabled={update.isPending}
                    />
                    <span>
                      <span className="font-medium text-gray-900">USGA formula</span>{' '}
                      <span className="text-gray-500">(gross strokes − course rating) × 113 / slope rating</span>
                    </span>
                  </label>
                  <label className="flex items-start gap-2 text-sm">
                    <input
                      type="radio"
                      name="handicap-mode"
                      className="mt-0.5"
                      checked={handicapModeInput === HANDICAP_CALC_MODES.straightStrokes}
                      onChange={() => handleSaveHandicapMode(HANDICAP_CALC_MODES.straightStrokes)}
                      disabled={update.isPending}
                    />
                    <span>
                      <span className="font-medium text-gray-900">Straight strokes</span>{' '}
                      <span className="text-gray-500">gross strokes − course rating, ignoring slope</span>
                    </span>
                  </label>
                  <label className="flex items-start gap-2 text-sm">
                    <input
                      type="radio"
                      name="handicap-mode"
                      className="mt-0.5"
                      checked={handicapModeInput === HANDICAP_CALC_MODES.custom}
                      onChange={() => handleSaveHandicapMode(HANDICAP_CALC_MODES.custom)}
                      disabled={update.isPending}
                    />
                    <span>
                      <span className="font-medium text-gray-900">Custom formula</span>{' '}
                      <span className="text-gray-500">enter your own formula below</span>
                    </span>
                  </label>
                </div>

                {handicapModeInput === HANDICAP_CALC_MODES.custom && (
                  <div className="mt-3 pl-6">
                    <textarea
                      value={formulaInput}
                      onChange={(e) => setFormulaInput(e.target.value)}
                      placeholder="grossStrokes - courseRating / 2"
                      rows={2}
                      disabled={update.isPending}
                      className="w-full max-w-md rounded-md border border-gray-300 px-3 py-1.5 font-mono text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                    />
                    <p className="mt-1 text-xs text-gray-500">
                      Available variables: <code>grossStrokes</code>, <code>courseRating</code>, <code>slopeRating</code>, <code>par</code>. Must evaluate to a number.
                    </p>
                    <div className="mt-2">
                      <Button size="sm" onClick={handleSaveFormula} disabled={!formulaDirty || update.isPending}>
                        Save formula
                      </Button>
                    </div>
                    {formulaError && <p className="mt-1 text-sm text-red-600">{formulaError}</p>}
                  </div>
                )}
              </div>

              <div className="flex items-start justify-between gap-6 py-4">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900">Rolling window</p>
                  <p className="mt-0.5 text-sm text-gray-500">
                    Average the best (lowest) X differentials out of a player's last Y rounds played — WHS-style. Set both to the same value for a simple rolling average.
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <span className="text-sm text-gray-500">Best</span>
                  <input
                    type="number"
                    min={1}
                    value={windowXInput}
                    onChange={(e) => setWindowXInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') handleSaveWindow(); }}
                    disabled={update.isPending}
                    className="w-16 rounded-md border border-gray-300 px-2 py-1.5 text-sm text-center focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                  />
                  <span className="text-sm text-gray-500">of last</span>
                  <input
                    type="number"
                    min={1}
                    value={windowYInput}
                    onChange={(e) => setWindowYInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') handleSaveWindow(); }}
                    disabled={update.isPending}
                    className="w-16 rounded-md border border-gray-300 px-2 py-1.5 text-sm text-center focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                  />
                  <span className="text-sm text-gray-500">rounds</span>
                  <Button
                    size="sm"
                    onClick={handleSaveWindow}
                    disabled={!windowDirty || windowXExceedsY || update.isPending}
                  >
                    Save
                  </Button>
                </div>
              </div>
              {windowXExceedsY && (
                <p className="pb-2 text-sm text-red-600">"Best" cannot be greater than "of last".</p>
              )}
              <p className="pt-4 text-xs text-gray-500">
                Changes here only affect handicaps calculated from this point on. To apply them retroactively, use "Recalculate All Handicaps" on the admin dashboard.
              </p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Tee Times</CardTitle>
            </CardHeader>
            <CardContent className="divide-y divide-gray-100">
              <div className="flex items-start justify-between gap-6 py-4">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900">Sign-up cutoff time</p>
                  <p className="mt-0.5 text-sm text-gray-500">
                    Time, in your local time zone ({localTimeZoneAbbreviation()}), on the day before each round when tee-time sign-ups close and auto-fill assigns the remaining players.
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <input
                    type="time"
                    value={cutoffTimeInput}
                    onChange={(e) => setCutoffTimeInput(e.target.value)}
                    disabled={update.isPending}
                    className="rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                  />
                  <Button
                    size="sm"
                    onClick={handleSaveCutoffTime}
                    disabled={!cutoffTimeDirty || update.isPending}
                  >
                    Save
                  </Button>
                </div>
              </div>
              <Toggle
                label="Substitutes"
                description="Lets a player add a substitute to their tee time when players have skipped the round, up to the number of skips. Manage the substitute pool on the Players page."
                checked={getSetting(SETTING_KEYS.substitutesEnabled)}
                onChange={(v) => handleToggle(SETTING_KEYS.substitutesEnabled, v)}
                disabled={update.isPending}
              />
              <div className="flex items-start justify-between gap-6 py-4">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900">Round cost</p>
                  <p className="mt-0.5 text-sm text-gray-500">
                    Dollar amount charged per round, shown to substitutes in the "spots available" email as the amount payable to any league officer.
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <span className="text-sm text-gray-500">$</span>
                  <input
                    type="number"
                    min={0}
                    value={roundCostInput}
                    onChange={(e) => setRoundCostInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') handleSaveRoundCost(); }}
                    disabled={update.isPending}
                    className="w-20 rounded-md border border-gray-300 px-3 py-1.5 text-sm text-center focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                  />
                  <Button
                    size="sm"
                    onClick={handleSaveRoundCost}
                    disabled={!roundCostDirty || update.isPending}
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
                description="After auto-fill runs (at the sign-up cutoff time, the day before each round), send every player in the current half an email with the full tee sheet for the upcoming round."
                checked={getSetting(SETTING_KEYS.teeTimeEmailEnabled)}
                onChange={(v) => handleToggle(SETTING_KEYS.teeTimeEmailEnabled, v)}
                disabled={update.isPending}
              />
              <Toggle
                label="Sign-up reminder emails"
                description="A few hours before sign-ups close, remind every active player in the half who doesn't yet have a tee time that they can pick one, or they'll be auto-assigned by their time preference."
                checked={getSetting(SETTING_KEYS.signUpReminderEmailEnabled)}
                onChange={(v) => handleToggle(SETTING_KEYS.signUpReminderEmailEnabled, v)}
                disabled={update.isPending}
              />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Community</CardTitle>
            </CardHeader>
            <CardContent className="divide-y divide-gray-100">
              <div className="flex items-start justify-between gap-6 py-4">
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900">WhatsApp group link</p>
                  <p className="mt-0.5 text-sm text-gray-500">
                    Invite link to the league's WhatsApp group. When set, a "Join our WhatsApp group" link is shown in the site footer. Leave blank to hide it.
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <input
                    type="url"
                    placeholder="https://chat.whatsapp.com/..."
                    value={whatsAppLinkInput}
                    onChange={(e) => setWhatsAppLinkInput(e.target.value)}
                    onKeyDown={(e) => { if (e.key === 'Enter') handleSaveWhatsAppLink(); }}
                    disabled={update.isPending}
                    className="w-64 rounded-md border border-gray-300 px-3 py-1.5 text-sm focus:border-[#1B5E20] focus:outline-none focus:ring-1 focus:ring-[#1B5E20] disabled:opacity-50"
                  />
                  <Button
                    size="sm"
                    onClick={handleSaveWhatsAppLink}
                    disabled={!whatsAppLinkDirty || update.isPending}
                  >
                    Save
                  </Button>
                </div>
              </div>
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
