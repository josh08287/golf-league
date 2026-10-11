import { useEffect, useMemo, useRef, useState } from 'react';
import maplibregl, { type GeoJSONSource } from 'maplibre-gl';
import { ChevronLeft, ChevronRight, Navigation2, X } from 'lucide-react';
import type { CourseHoleGeoHole, GeoPoint } from '@/types/api';
import { useGeolocation } from '@/hooks/useGeolocation';
import { bearing, haversineMeters, metersToYards, midpoint, yardsBetween } from '@/lib/geo';
import { circlePolygon, passiveMarker, toLngLat, useSatelliteMap } from '@/components/map/useSatelliteMap';

/** Beyond this distance from the green the player is treated as off this hole (measure from the tee instead). */
const ON_HOLE_MAX_M = 800;
const FALLBACK_CENTER: GeoPoint = { lat: 39.5, lng: -98.35 };

export interface HoleMapSheetProps {
  holeNumber: number;
  par: number;
  geo: CourseHoleGeoHole | undefined;
  /** Saved course centre, used to place the map before any GPS fix when a hole isn't mapped. */
  courseCentre: GeoPoint | null;
  onPrevHole: () => void;
  onNextHole: () => void;
  onClose: () => void;
}

type Origin = { point: GeoPoint; label: 'you' | 'tee' | 'map center' };

function markerEl(className: string, html = ''): HTMLDivElement {
  const el = document.createElement('div');
  el.className = className;
  el.innerHTML = html;
  return el;
}

const EMPTY_FC: GeoJSON.FeatureCollection = { type: 'FeatureCollection', features: [] };

/**
 * Full-screen satellite view of the current hole for on-course yardages.
 * Rotated so the tee is at the bottom and the green at the top, framed on
 * the player's live GPS position when they're on the hole, and any tap on
 * the map drops a target showing the distance to it (and from it to the
 * green).
 */
export function HoleMapSheet({ holeNumber, par, geo, courseCentre, onPrevHole, onNextHole, onClose }: HoleMapSheetProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const { position, accuracyM, status } = useGeolocation(true);
  const [target, setTarget] = useState<GeoPoint | null>(null);
  const [northUp, setNorthUp] = useState(false);
  const [mapBearing, setMapBearing] = useState(0);

  const initialCenter = geo ? midpoint(geo.tee, geo.green) : courseCentre ?? FALLBACK_CENTER;
  const map = useSatelliteMap(containerRef, {
    center: initialCenter,
    zoom: geo || courseCentre ? 16 : 4,
  });

  const holeBearing = geo ? bearing(geo.tee, geo.green) : 0;
  const onHole = !!position && (!geo || haversineMeters(position, geo.green) <= ON_HOLE_MAX_M);

  // Where yardages are measured from: the player when they're on this hole,
  // otherwise the tee, otherwise (no hole data, no GPS) the map centre.
  const [mapCentre, setMapCentre] = useState<GeoPoint>(initialCenter);
  const fallbackCentre = !geo && !position ? mapCentre : null;
  const origin: Origin = useMemo(() => {
    if (position && onHole) return { point: position, label: 'you' };
    if (geo) return { point: geo.tee, label: 'tee' };
    return fallbackCentre ? { point: fallbackCentre, label: 'map center' } : { point: position!, label: 'you' };
  }, [position, onHole, geo, fallbackCentre]);

  // New hole → clear the target.
  useEffect(() => setTarget(null), [holeNumber]);

  // Tap/click anywhere → target.
  useEffect(() => {
    if (!map) return;
    const onClick = (e: maplibregl.MapMouseEvent) => setTarget({ lat: e.lngLat.lat, lng: e.lngLat.lng });
    const onRotate = () => setMapBearing(map.getBearing());
    const onMoveEnd = () => {
      const c = map.getCenter();
      setMapCentre({ lat: c.lat, lng: c.lng });
    };
    map.on('click', onClick);
    map.on('rotate', onRotate);
    map.on('moveend', onMoveEnd);
    return () => {
      map.off('click', onClick);
      map.off('rotate', onRotate);
      map.off('moveend', onMoveEnd);
    };
  }, [map]);

  // Framing: hole-up (tee at bottom, green at top). MapLibre's bearing is the
  // compass direction that points up the screen, so the tee→green bearing
  // puts the green at the top. Re-framed only when the hole, orientation, or
  // on-hole state changes — not on every GPS tick, so the player can pan.
  const frameKey = `${holeNumber}|${northUp}|${onHole}|${!!geo}|${!!position}`;
  useEffect(() => {
    if (!map) return;
    const b = northUp ? 0 : holeBearing;
    if (geo) {
      const from = position && onHole ? position : geo.tee;
      const bounds = new maplibregl.LngLatBounds(toLngLat(from), toLngLat(from))
        .extend(toLngLat(geo.green))
        .extend(toLngLat(geo.greenBack ?? geo.green));
      map.fitBounds(bounds, {
        bearing: b,
        padding: { top: 96, bottom: 190, left: 48, right: 48 },
        maxZoom: 19,
        duration: 600,
      });
    } else if (position) {
      map.easeTo({ center: toLngLat(position), zoom: 17.5, bearing: 0, duration: 600 });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps -- deliberately keyed on frameKey only
  }, [map, frameKey]);

  // Static hole markers (tee + green).
  useEffect(() => {
    if (!map || !geo) return;
    const markers = [
      passiveMarker(markerEl('hole-map-tee', 'T'), geo.tee, map),
      passiveMarker(markerEl('hole-map-flag', '<span></span>'), geo.green, map, 'bottom'),
    ];
    return () => markers.forEach((m) => m.remove());
  }, [map, geo]);

  // Player dot + accuracy ring.
  const playerMarker = useRef<maplibregl.Marker | null>(null);
  useEffect(() => {
    if (!map) return;
    if (!map.getSource('accuracy')) {
      map.addSource('accuracy', { type: 'geojson', data: EMPTY_FC });
      map.addLayer({ id: 'accuracy', type: 'fill', source: 'accuracy', paint: { 'fill-color': '#3b82f6', 'fill-opacity': 0.18 } });
    }
    const source = map.getSource('accuracy') as GeoJSONSource;
    if (!position) {
      source.setData(EMPTY_FC);
      playerMarker.current?.remove();
      playerMarker.current = null;
      return;
    }
    source.setData({ type: 'FeatureCollection', features: [circlePolygon(position, accuracyM ?? 0)] });
    if (playerMarker.current) playerMarker.current.setLngLat(toLngLat(position));
    else playerMarker.current = passiveMarker(markerEl('hole-map-player'), position, map);
  }, [map, position, accuracyM]);
  useEffect(() => () => {
    playerMarker.current?.remove();
  }, []);

  // Target marker, measuring lines and their yardage labels.
  useEffect(() => {
    if (!map) return;
    if (!map.getSource('measure')) {
      map.addSource('measure', { type: 'geojson', data: EMPTY_FC });
      map.addLayer({
        id: 'measure',
        type: 'line',
        source: 'measure',
        paint: { 'line-color': '#ffffff', 'line-width': 2.5, 'line-dasharray': [2, 1.5] },
      });
    }
    const source = map.getSource('measure') as GeoJSONSource;
    if (!target) {
      source.setData(EMPTY_FC);
      return;
    }

    const segments: [GeoPoint, GeoPoint, string][] = [[origin.point, target, `${yardsBetween(origin.point, target)} yds`]];
    if (geo) segments.push([target, geo.green, `${yardsBetween(target, geo.green)} to pin`]);

    source.setData({
      type: 'FeatureCollection',
      features: segments.map(([a, b]) => ({
        type: 'Feature',
        properties: {},
        geometry: { type: 'LineString', coordinates: [toLngLat(a), toLngLat(b)] },
      })),
    });
    const markers = [
      passiveMarker(markerEl('hole-map-target'), target, map),
      ...segments.map(([a, b, text]) => passiveMarker(markerEl('hole-map-label', text), midpoint(a, b), map)),
    ];
    return () => markers.forEach((m) => m.remove());
  }, [map, target, origin, geo]);

  const yardsTo = (p: GeoPoint | null | undefined) => (p ? yardsBetween(origin.point, p) : null);
  const accuracyYds = accuracyM != null ? Math.round(metersToYards(accuracyM)) : null;

  return (
    <div className="fixed inset-0 z-[1000] flex flex-col bg-black" role="dialog" aria-label={`Hole ${holeNumber} map`}>
      <style>{HOLE_MAP_CSS}</style>
      {/* maplibre-gl.css forces position:relative on the container itself, so wrap it. */}
      <div className="absolute inset-0">
        <div ref={containerRef} className="h-full w-full" />
      </div>

      {/* Header */}
      <div className="relative z-10 flex items-center justify-between gap-2 bg-gradient-to-b from-black/70 to-transparent px-3 pb-6 pt-3 text-white">
        <button type="button" onClick={onPrevHole} className="rounded-full bg-black/40 p-2" aria-label="Previous hole">
          <ChevronLeft className="h-5 w-5" />
        </button>
        <div className="text-center">
          <div className="text-lg font-semibold leading-tight">Hole {holeNumber}</div>
          <div className="text-xs text-white/80">Par {par}</div>
        </div>
        <div className="flex items-center gap-2">
          <button type="button" onClick={onNextHole} className="rounded-full bg-black/40 p-2" aria-label="Next hole">
            <ChevronRight className="h-5 w-5" />
          </button>
          <button type="button" onClick={onClose} className="rounded-full bg-black/40 p-2" aria-label="Close map">
            <X className="h-5 w-5" />
          </button>
        </div>
      </div>

      {/* Orientation toggle: the arrow always points north. */}
      {geo && (
        <button
          type="button"
          onClick={() => setNorthUp((v) => !v)}
          className="absolute right-3 top-20 z-10 flex h-11 w-11 flex-col items-center justify-center rounded-full bg-black/50 text-white"
          aria-label={northUp ? 'Rotate to hole direction' : 'Rotate to north up'}
          title={northUp ? 'North up' : 'Hole up'}
        >
          <Navigation2 className="h-4 w-4" style={{ transform: `rotate(${-mapBearing}deg)` }} />
          <span className="text-[9px] font-semibold leading-none">N</span>
        </button>
      )}

      {/* Readout */}
      <div className="absolute inset-x-0 bottom-0 z-10 space-y-2 bg-gradient-to-t from-black/80 to-transparent px-4 pb-6 pt-10 text-white">
        {geo ? (
          <div className="flex justify-center gap-6 text-center">
            {geo.greenFront && <Yardage label="Front" yards={yardsTo(geo.greenFront)} />}
            <Yardage label="Center" yards={yardsTo(geo.green)} big />
            {geo.greenBack && <Yardage label="Back" yards={yardsTo(geo.greenBack)} />}
          </div>
        ) : (
          <p className="text-center text-sm text-white/80">This hole isn't mapped yet. Tap the map to measure.</p>
        )}

        <div className="flex flex-wrap items-center justify-center gap-2 text-xs">
          <span className="rounded-full bg-white/15 px-2.5 py-1">
            From {origin.label}
            {origin.label === 'you' && accuracyYds != null && ` · ±${accuracyYds} yds`}
          </span>
          {status === 'pending' && <span className="rounded-full bg-white/15 px-2.5 py-1">Finding your location…</span>}
          {status === 'denied' && (
            <span className="rounded-full bg-amber-500/80 px-2.5 py-1">Location is blocked. Allow it in your browser for yardages from you.</span>
          )}
          {status === 'unavailable' && <span className="rounded-full bg-amber-500/80 px-2.5 py-1">Location unavailable</span>}
          {position && !onHole && geo && <span className="rounded-full bg-white/15 px-2.5 py-1">You're not on this hole</span>}
          {target ? (
            <button type="button" onClick={() => setTarget(null)} className="rounded-full bg-white px-3 py-1 font-medium text-gray-900">
              Clear
            </button>
          ) : (
            <span className="rounded-full bg-white/15 px-2.5 py-1">Tap anywhere for a yardage</span>
          )}
        </div>
      </div>
    </div>
  );
}

function Yardage({ label, yards, big }: { label: string; yards: number | null; big?: boolean }) {
  return (
    <div>
      <div className={big ? 'text-4xl font-bold leading-none' : 'text-2xl font-semibold leading-none'}>{yards ?? '—'}</div>
      <div className="mt-1 text-[11px] uppercase tracking-wide text-white/70">{label}</div>
    </div>
  );
}

// Marker styles. Markers stay upright when the map is rotated.
const HOLE_MAP_CSS = `
.hole-map-player { width: 18px; height: 18px; border-radius: 9999px; background: #3b82f6; border: 3px solid #fff;
  box-shadow: 0 0 0 0 rgba(59,130,246,.6); animation: hole-map-pulse 2s infinite; }
@keyframes hole-map-pulse { 0% { box-shadow: 0 0 0 0 rgba(59,130,246,.6); } 70% { box-shadow: 0 0 0 14px rgba(59,130,246,0); } 100% { box-shadow: 0 0 0 0 rgba(59,130,246,0); } }
.hole-map-tee { width: 22px; height: 22px; border-radius: 9999px; background: #fff; color: #1B5E20; font: 700 12px/22px system-ui, sans-serif;
  text-align: center; box-shadow: 0 1px 3px rgba(0,0,0,.5); }
.hole-map-flag { width: 4px; height: 28px; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,.5); }
.hole-map-flag span { position: absolute; left: 4px; top: 0; width: 0; height: 0; border-top: 7px solid transparent; border-bottom: 7px solid transparent; border-left: 14px solid #ef4444; }
.hole-map-target { width: 26px; height: 26px; border-radius: 9999px; border: 3px solid #facc15; background: rgba(250,204,21,.25); }
.hole-map-label { padding: 2px 8px; border-radius: 9999px; background: rgba(0,0,0,.75); color: #fff; font: 600 13px/18px system-ui, sans-serif; white-space: nowrap; }
`;
