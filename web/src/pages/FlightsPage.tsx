import { Link } from 'react-router-dom';
import { Users } from 'lucide-react';
import { useFlights } from '@/hooks/useFlights';
import { useSeasons } from '@/hooks/useSeasons';
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
} from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import { FullPageSpinner } from '@/components/ui/Spinner';
import { ErrorMessage } from '@/components/ui/ErrorMessage';
import { PageHeader } from '@/components/ui/PageHeader';

export function FlightsPage() {
  const { data, isPending, isError } = useFlights();
  const { data: seasons } = useSeasons();
  const activeSeason = seasons?.find((s) => s.isActive);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Flights"
        description="Competition groups organised by handicap range."
      />

      {isPending && <FullPageSpinner />}
      {isError && (
        <ErrorMessage message="Could not load flights. Please try again." />
      )}

      {data && (
        <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
          {data.data.length === 0 && (
            <p className="col-span-full text-gray-500 text-sm">
              No flights have been created for this season yet.
            </p>
          )}
          {data.data.map((flight) => (
            <Card
              key={flight.id}
              className="flex flex-col hover:shadow-md transition-shadow"
            >
              <CardHeader>
                <div className="flex items-start justify-between gap-2">
                  <CardTitle>{flight.name}</CardTitle>
                  <Badge variant="secondary" className="shrink-0">
                    <Users className="mr-1 h-3 w-3" />
                    {flight.playerCount}
                  </Badge>
                </div>
                <CardDescription>
                  Handicap range: {flight.minHandicap} &ndash; {flight.maxHandicap}
                </CardDescription>
              </CardHeader>
              <CardContent className="mt-auto pt-0">
                <Button variant="outline" size="sm" className="w-full" asChild>
                  <Link to={`/flights/${flight.id}${activeSeason ? `?seasonId=${activeSeason.id}` : ''}`}>
                    View Leaderboard
                  </Link>
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
