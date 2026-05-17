import { Link } from 'react-router-dom';
import { useLeagueName } from '@/context/LeagueContext';
import { Users, CalendarDays, Clock, CheckCircle, Plus, ArrowRight, Calculator, AlertTriangle } from 'lucide-react';
import { usePlayers } from '@/hooks/usePlayers';
import { useRounds } from '@/hooks/useRounds';
import { useRecalculateRounds } from '@/hooks/admin/useRecalculateRounds';
import { Spinner } from '@/components/ui/Spinner';
import { PageHeader } from '@/components/ui/PageHeader';
import { Card } from '@/components/ui/Card';
import { Button } from '@/components/ui/Button';
import { isRoundInProgress, isRoundScheduled, isRoundFinalized } from '@/lib/enumUtils';

interface StatCardProps {
  label: string;
  value: string | number;
  icon: React.ReactNode;
  colorClass: string;
}

function StatCard({ label, value, icon, colorClass }: StatCardProps) {
  return (
    <Card className="flex items-center gap-4 p-6">
      <div className={`rounded-xl p-3 ${colorClass}`}>{icon}</div>
      <div>
        <p className="text-sm text-gray-500">{label}</p>
        <p className="text-2xl font-bold text-gray-900">{value}</p>
      </div>
    </Card>
  );
}

interface QuickLinkProps {
  to: string;
  label: string;
  description: string;
  icon: React.ReactNode;
}

function QuickLink({ to, label, description, icon }: QuickLinkProps) {
  return (
    <Link
      to={to}
      className="flex items-center gap-4 rounded-lg border border-gray-200 bg-white p-4 transition-shadow hover:shadow-md"
    >
      <div className="rounded-lg bg-green-50 p-2 text-[#1B5E20]">{icon}</div>
      <div className="flex-1">
        <p className="font-medium text-gray-900">{label}</p>
        <p className="text-sm text-gray-500">{description}</p>
      </div>
      <ArrowRight className="h-4 w-4 text-gray-400" />
    </Link>
  );
}

export function AdminDashboardPage() {
  const leagueName = useLeagueName();
  const { data: playersPage, isLoading: playersLoading } = usePlayers();
  const { data: roundsPage, isLoading: roundsLoading } = useRounds();
  const recalculateRounds = useRecalculateRounds();

  const isLoading = playersLoading || roundsLoading || recalculateRounds.isPending;

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Spinner />
      </div>
    );
  }

  const players = playersPage?.data ?? [];
  const rounds = roundsPage?.data ?? [];

  const activeRounds = rounds.filter((r) => isRoundInProgress(r.status)).length;
  const upcomingRounds = rounds.filter((r) => isRoundScheduled(r.status)).length;
  const lastFinalized = rounds
    .filter((r) => isRoundFinalized(r.status))
    .sort((a, b) => b.scheduledDate.localeCompare(a.scheduledDate))[0];

  return (
    <div className="space-y-8">
      <PageHeader title="Dashboard" subtitle={`${leagueName} Admin`} />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Total Players"
          value={playersPage?.meta.totalCount ?? players.length}
          icon={<Users className="h-6 w-6 text-blue-600" />}
          colorClass="bg-blue-50"
        />
        <StatCard
          label="Active Rounds"
          value={activeRounds}
          icon={<Clock className="h-6 w-6 text-amber-600" />}
          colorClass="bg-amber-50"
        />
        <StatCard
          label="Upcoming Rounds"
          value={upcomingRounds}
          icon={<CalendarDays className="h-6 w-6 text-purple-600" />}
          colorClass="bg-purple-50"
        />
        <StatCard
          label="Last Finalized"
          value={lastFinalized ? new Date(lastFinalized.scheduledDate).toLocaleDateString() : 'None'}
          icon={<CheckCircle className="h-6 w-6 text-green-700" />}
          colorClass="bg-green-50"
        />
      </div>

      <div>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-gray-500">
          Quick Actions
        </h2>
        <div className="grid gap-3 sm:grid-cols-2">
          <QuickLink
            to="/admin/players"
            label="Add Player"
            description="Register a new league member"
            icon={<Plus className="h-5 w-5" />}
          />
          <QuickLink
            to="/admin/rounds"
            label="Create Round"
            description="Schedule a new round"
            icon={<Plus className="h-5 w-5" />}
          />
          <QuickLink
            to="/admin/rounds"
            label="Enter Scores"
            description="Record scores for an active round"
            icon={<CalendarDays className="h-5 w-5" />}
          />
          <QuickLink
            to="/admin/flights"
            label="Manage Flights"
            description="Assign players to flights"
            icon={<Users className="h-5 w-5" />}
          />
        </div>
      </div>

      <div>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wider text-gray-500">
          Data Maintenance
        </h2>
        <Card className="p-6">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-start gap-3">
              <div className="rounded-lg bg-amber-50 p-2 text-amber-600">
                <AlertTriangle className="h-5 w-5" />
              </div>
              <div>
                <p className="font-medium text-gray-900">Recalculate All Rounds</p>
                <p className="text-sm text-gray-500">
                  Re-applies course handicaps and recalculates all net scores for finalized rounds.
                  Use this if handicap calculations have changed or historical data needs correction.
                </p>
              </div>
            </div>
            <Button
              variant="secondary"
              onClick={() => recalculateRounds.mutate()}
              disabled={recalculateRounds.isPending}
              className="shrink-0"
            >
              {recalculateRounds.isPending ? (
                <>
                  <Spinner className="mr-2 h-4 w-4" />
                  Processing...
                </>
              ) : (
                <>
                  <Calculator className="mr-2 h-4 w-4" />
                  Recalculate All
                </>
              )}
            </Button>
          </div>

          {recalculateRounds.isSuccess && (
            <div className="mt-4 rounded-lg bg-green-50 p-4 text-sm text-green-800">
              <p className="font-medium">Recalculation complete!</p>
              <ul className="mt-1 list-inside list-disc">
                <li>{recalculateRounds.data.roundsProcessed} rounds processed</li>
                <li>{recalculateRounds.data.participantsProcessed} participants updated</li>
                <li>{recalculateRounds.data.holeScoresUpdated} hole scores recalculated</li>
              </ul>
            </div>
          )}

          {recalculateRounds.isError && (
            <div className="mt-4 rounded-lg bg-red-50 p-4 text-sm text-red-800">
              <p className="font-medium">Error recalculating rounds</p>
              <p className="mt-1">
                {recalculateRounds.error instanceof Error
                  ? recalculateRounds.error.message
                  : 'An unexpected error occurred'}
              </p>
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}
