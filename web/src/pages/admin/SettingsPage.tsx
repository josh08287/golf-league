import { PageHeader } from '../../components/ui/PageHeader';
import { Card } from '../../components/ui/Card';

export function SettingsPage() {
  return (
    <div className="space-y-6">
      <PageHeader title="Settings" subtitle="League configuration" />
      <Card className="p-6">
        <p className="text-sm text-gray-500">Settings coming soon.</p>
      </Card>
    </div>
  );
}
