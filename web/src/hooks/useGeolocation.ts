import { useEffect, useState } from 'react';
import type { GeoPoint } from '@/types/api';

export type GeolocationStatus = 'pending' | 'ok' | 'denied' | 'unavailable';

export interface GeolocationState {
  position: GeoPoint | null;
  /** Reported accuracy radius in metres. */
  accuracyM: number | null;
  status: GeolocationStatus;
}

/**
 * Live device position via watchPosition (high accuracy, for on-course
 * yardages). The watch only runs while `active` is true and is cleared on
 * unmount. Requires HTTPS (or localhost).
 */
export function useGeolocation(active: boolean): GeolocationState {
  const [state, setState] = useState<GeolocationState>({ position: null, accuracyM: null, status: 'pending' });

  useEffect(() => {
    if (!active) return;
    if (!('geolocation' in navigator)) {
      setState({ position: null, accuracyM: null, status: 'unavailable' });
      return;
    }

    const id = navigator.geolocation.watchPosition(
      (pos) =>
        setState({
          position: { lat: pos.coords.latitude, lng: pos.coords.longitude },
          accuracyM: pos.coords.accuracy,
          status: 'ok',
        }),
      (err) =>
        setState((prev) => ({
          ...prev,
          // Keep the last good fix through a transient timeout.
          status: err.code === err.PERMISSION_DENIED ? 'denied' : prev.position ? prev.status : 'unavailable',
        })),
      { enableHighAccuracy: true, maximumAge: 5_000, timeout: 20_000 },
    );
    return () => navigator.geolocation.clearWatch(id);
  }, [active]);

  return state;
}
