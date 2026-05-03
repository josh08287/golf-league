import { Link } from 'react-router-dom';
import { ArrowRight, Trophy, Calendar } from 'lucide-react';
import { useFlights } from '@/hooks/useFlights';
import { useRounds } from '@/hooks/useRounds';
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  CardDescription,
} from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { Spinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { formatShortDate } from '@/lib/utils';
import { normalizeRoundStatus } from '@/lib/enumUtils';
import type { RoundStatus } from '@/types/api';

function statusVariant(status: RoundStatus) {
  const normalized = normalizeRoundStatus(status);
  switch (normalized) {
    case 'Finalized': return 'green' as const;
    case 'InProgress': return 'amber' as const;
    case 'Scheduled': return 'blue' as const;
  }
}

export function HomePage() {
  const flights = useFlights();
  const rounds = useRounds(1);

  const latestRounds = rounds.data?.data.slice(0, 3) ?? [];

  return (
    <div className="space-y-10">
      {/* Hero */}
      <section className="rounded-2xl bg-primary-900 px-8 py-12 text-white text-center">
        <h1 className="text-4xl font-extrabold tracking-tight sm:text-5xl">
          ⛳ Golf League
        </h1>
        <p className="mt-4 text-primary-100 text-lg max-w-xl mx-auto">
          Track standings, scores, and handicaps all season long.
        </p>
        <div className="mt-6 flex flex-wrap justify-center gap-3">
          <Button variant="secondary" asChild>
            <Link to="/flights">View Flights</Link>
          </Button>
          <Button
            className="bg-white text-primary-900 hover:bg-primary-50"
            asChild
          >
            <Link to="/rounds">Latest Rounds</Link>
          </Button>
        </div>
      </section>

      {/* Flights overview */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-xl font-bold text-gray-900">
            <Trophy className="h-5 w-5 text-primary-700" />
            Flights &amp; Standings
          </h2>
          <Button variant="ghost" size="sm" asChild>
            <Link to="/flights" className="flex items-center gap-1">
              All flights <ArrowRight className="h-4 w-4" />
            </Link>
          </Button>
        </div>

        {flights.isPending && (
          <div className="flex justify-center py-12">
            <Spinner />
          </div>
        )}
        {flights.isError && (
          <ErrorMessage message="Could not load flights. Please try again." />
        )}
        {flights.data && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {flights.data.data.map((flight) => (
              <Card key={flight.id} className="hover:shadow-md transition-shadow">
                <CardHeader className="pb-3">
                  <div className="flex items-start justify-between">
                    <CardTitle className="text-base">{flight.name}</CardTitle>
                    <Badge variant="secondary">
                      {flight.playerCount} players
                    </Badge>
                  </div>
                  <CardDescription>
                    Handicap {flight.minHandicap} – {flight.maxHandicap}
                  </CardDescription>
                </CardHeader>
                <CardContent>
                  <Button variant="outline" size="sm" asChild className="w-full">
                    <Link to={`/flights/${flight.id}`}>
                      View Leaderboard
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </section>

      {/* Latest rounds */}
      <section>
        <div className="flex items-center justify-between mb-4">
          <h2 className="flex items-center gap-2 text-xl font-bold text-gray-900">
            <Calendar className="h-5 w-5 text-primary-700" />
            Recent Rounds
          </h2>
          <Button variant="ghost" size="sm" asChild>
            <Link to="/rounds" className="flex items-center gap-1">
              All rounds <ArrowRight className="h-4 w-4" />
            </Link>
          </Button>
        </div>

        {rounds.isPending && (
          <div className="flex justify-center py-12">
            <Spinner />
          </div>
        )}
        {rounds.isError && (
          <ErrorMessage message="Could not load rounds. Please try again." />
        )}
        {rounds.data && (
          <div className="space-y-3">
            {latestRounds.length === 0 && (
              <p className="text-gray-500 text-sm">No rounds scheduled yet.</p>
            )}
            {latestRounds.map((round) => (
              <Link
                key={round.id}
                to={`/rounds/${round.id}`}
                className="flex items-center justify-between rounded-lg border border-gray-200 bg-white px-5 py-4 hover:shadow-sm hover:border-primary-300 transition-all"
              >
                <div>
                  <p className="font-medium text-gray-900">{round.courseName}</p>
                  <p className="text-sm text-gray-500">
                    {round.flightName} &middot; {formatShortDate(round.scheduledDate)}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <Badge variant={statusVariant(round.status)}>{round.status}</Badge>
                  <ArrowRight className="h-4 w-4 text-gray-400" />
                </div>
              </Link>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
