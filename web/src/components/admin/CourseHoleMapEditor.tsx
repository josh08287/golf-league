import { useEffect, useRef, useState } from 'react';
import maplibregl, { type GeoJSONSource } from 'maplibre-gl';
import { Download, MapPin, Search, Trash2 } from 'lucide-react';
import { Button } from '../ui/Button';
import { Spinner } from '../ui/Spinner';
import { inputClass } from './FormField';
import { useCourseHoleGeo, usePreviewOsmHoleGeo, useSaveCourseHoleGeo } from '../../hooks/useCourseHoleGeo';
import { toLngLat, useSatelliteMap } from '../map/useSatelliteMap';
import type { CourseHole, CourseHoleGeoHole, GeoPoint } from '../../types/api';

interface CourseHoleMapEditorProps {
  courseId: number;
  courseName: string;
  courseHoles: CourseHole[];
}

type Placing = { holeNumber: number; tee: GeoPoint | null };

const EMPTY_FC: GeoJSON.FeatureCollection = { type: 'FeatureCollection', features: [] };

function pinEl(className: string, text: string): HTMLDivElement {
  const el = document.createElement('div');
  el.className = className;
  el.textContent = text;
  return el;
}

function errorMessage(err: unknown): string {
  const e = err as { response?: { data?: { error?: string } } } | null;
  return e?.response?.data?.error ?? 'Something went wrong. Please try again.';
}

/**
 * Admin editor for a course's hole map: find the course on satellite
 * imagery, import hole lines from OpenStreetMap, drag tee/green pins to fix
 * them, place unmapped holes by hand, then save.
 */
export function CourseHoleMapEditor({ courseId, courseName, courseHoles }: CourseHoleMapEditorProps) {
  const saved = useCourseHoleGeo(courseId);
  const preview = usePreviewOsmHoleGeo(courseId);
  const save = useSaveCourseHoleGeo(courseId);

  const [draft, setDraft] = useState<Record<number, CourseHoleGeoHole>>({});
  const [centre, setCentre] = useState<GeoPoint | null>(null);
  const [dirty, setDirty] = useState(false);
  const [warnings, setWarnings] = useState<string[]>([]);
  const [placing, setPlacing] = useState<Placing | null>(null);
  const [search, setSearch] = useState(courseName);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [searching, setSearching] = useState(false);

  const containerRef = useRef<HTMLDivElement>(null);
  const map = useSatelliteMap(containerRef, { center: { lat: 39.5, lng: -98.35 }, zoom: 3 });

  // Seed the draft from what's saved (once loaded, and after each save).
  useEffect(() => {
    if (!saved.data || dirty) return;
    setDraft(Object.fromEntries(saved.data.holes.map((h) => [h.holeNumber, h])));
    if (saved.data.latitude != null && saved.data.longitude != null)
      setCentre({ lat: saved.data.latitude, lng: saved.data.longitude });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reseed only when the saved data changes
  }, [saved.data]);

  // Frame the saved holes (or the saved centre) when the map first has data.
  const framed = useRef(false);
  useEffect(() => {
    if (!map || framed.current || !saved.data) return;
    framed.current = true;
    const pts = saved.data.holes.flatMap((h) => [h.tee, h.green]);
    if (pts.length > 0) {
      const bounds = pts.reduce((b, p) => b.extend(toLngLat(p)), new maplibregl.LngLatBounds(toLngLat(pts[0]), toLngLat(pts[0])));
      map.fitBounds(bounds, { padding: 40, duration: 0 });
    } else if (saved.data.latitude != null && saved.data.longitude != null) {
      map.jumpTo({ center: [saved.data.longitude, saved.data.latitude], zoom: 15 });
    }
  }, [map, saved.data]);

  // Hole lines.
  useEffect(() => {
    if (!map) return;
    if (!map.getSource('holes')) {
      map.addSource('holes', { type: 'geojson', data: EMPTY_FC });
      map.addLayer({ id: 'holes', type: 'line', source: 'holes', paint: { 'line-color': '#facc15', 'line-width': 2 } });
    }
    (map.getSource('holes') as GeoJSONSource).setData({
      type: 'FeatureCollection',
      features: Object.values(draft).map((h) => ({
        type: 'Feature',
        properties: {},
        geometry: { type: 'LineString', coordinates: [toLngLat(h.tee), toLngLat(h.green)] },
      })),
    });
  }, [map, draft]);

  // Draggable tee/green pins.
  useEffect(() => {
    if (!map) return;
    const markers: maplibregl.Marker[] = [];
    for (const hole of Object.values(draft)) {
      for (const which of ['tee', 'green'] as const) {
        const marker = new maplibregl.Marker({
          element: pinEl(which === 'tee' ? 'hole-edit-tee' : 'hole-edit-green', String(hole.holeNumber)),
          draggable: true,
        })
          .setLngLat(toLngLat(hole[which]))
          .addTo(map);
        marker.on('dragend', () => {
          const { lat, lng } = marker.getLngLat();
          moveHolePoint(hole.holeNumber, which, { lat, lng });
        });
        markers.push(marker);
      }
    }
    if (placing?.tee) markers.push(new maplibregl.Marker({ element: pinEl('hole-edit-tee', String(placing.holeNumber)) }).setLngLat(toLngLat(placing.tee)).addTo(map));
    return () => markers.forEach((m) => m.remove());
  }, [map, draft, placing]);

  // Placing an unmapped hole by hand: first tap = tee, second tap = green.
  useEffect(() => {
    if (!map || !placing) return;
    const onClick = (e: maplibregl.MapMouseEvent) => {
      const point = { lat: e.lngLat.lat, lng: e.lngLat.lng };
      if (!placing.tee) {
        setPlacing({ ...placing, tee: point });
        return;
      }
      setDraft((d) => ({
        ...d,
        [placing.holeNumber]: { holeNumber: placing.holeNumber, tee: placing.tee!, green: point, greenFront: null, greenBack: null, source: 'manual' },
      }));
      setDirty(true);
      setPlacing(null);
    };
    map.getCanvas().style.cursor = 'crosshair';
    map.on('click', onClick);
    return () => {
      map.off('click', onClick);
      map.getCanvas().style.cursor = '';
    };
  }, [map, placing]);

  function moveHolePoint(holeNumber: number, which: 'tee' | 'green', point: GeoPoint) {
    setDraft((d) => {
      const hole = d[holeNumber];
      if (!hole) return d;
      const next: CourseHoleGeoHole = { ...hole, [which]: point, source: 'manual', osmWayId: null };
      if (which === 'green') {
        // Keep front/back in the same place relative to the moved centre.
        const dLat = point.lat - hole.green.lat;
        const dLng = point.lng - hole.green.lng;
        const shift = (p: GeoPoint | null) => (p ? { lat: p.lat + dLat, lng: p.lng + dLng } : null);
        next.greenFront = shift(hole.greenFront);
        next.greenBack = shift(hole.greenBack);
      }
      return { ...d, [holeNumber]: next };
    });
    setDirty(true);
  }

  async function handleSearch() {
    if (!map || !search.trim()) return;
    setSearching(true);
    setSearchError(null);
    try {
      const res = await fetch(
        `https://nominatim.openstreetmap.org/search?format=json&limit=1&q=${encodeURIComponent(search.trim())}`,
        { headers: { Accept: 'application/json' } },
      );
      const results = (await res.json()) as { lat: string; lon: string }[];
      if (results.length === 0) {
        setSearchError('No match found. Try adding the town or state, or pan the map to the course yourself.');
        return;
      }
      map.flyTo({ center: [parseFloat(results[0].lon), parseFloat(results[0].lat)], zoom: 15 });
    } catch {
      setSearchError('Search failed. Pan the map to the course yourself.');
    } finally {
      setSearching(false);
    }
  }

  async function handleImport() {
    if (!map) return;
    const c = map.getCenter();
    const point = { lat: c.lat, lng: c.lng };
    const result = await preview.mutateAsync(point).catch(() => null);
    if (!result) return;
    setCentre(point);
    setDraft(Object.fromEntries(result.holes.map((h) => [h.holeNumber, h])));
    setWarnings(result.warnings);
    setDirty(true);
    if (result.holes.length > 0) {
      const pts = result.holes.flatMap((h) => [h.tee, h.green]);
      const bounds = pts.reduce((b, p) => b.extend(toLngLat(p)), new maplibregl.LngLatBounds(toLngLat(pts[0]), toLngLat(pts[0])));
      map.fitBounds(bounds, { padding: 40 });
    }
  }

  async function handleSave() {
    const c = centre ?? (map ? { lat: map.getCenter().lat, lng: map.getCenter().lng } : null);
    await save
      .mutateAsync({
        latitude: c?.lat ?? null,
        longitude: c?.lng ?? null,
        holes: Object.values(draft).sort((a, b) => a.holeNumber - b.holeNumber),
      })
      .then(() => setDirty(false))
      .catch(() => undefined);
  }

  function removeHole(holeNumber: number) {
    setDraft((d) => {
      const next = { ...d };
      delete next[holeNumber];
      return next;
    });
    setDirty(true);
  }

  function flyToHole(hole: CourseHoleGeoHole) {
    if (!map) return;
    const bounds = new maplibregl.LngLatBounds(toLngLat(hole.tee), toLngLat(hole.tee)).extend(toLngLat(hole.green));
    map.fitBounds(bounds, { padding: 60, maxZoom: 18 });
  }

  const sortedHoles = [...courseHoles].sort((a, b) => a.holeNumber - b.holeNumber);
  const mappedCount = sortedHoles.filter((h) => draft[h.holeNumber]).length;

  return (
    <div className="space-y-3">
      <style>{EDITOR_CSS}</style>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-lg font-semibold text-gray-900">Hole map</h3>
        <span className="text-sm text-gray-500">
          {mappedCount > 0 ? `${mappedCount}/${sortedHoles.length} holes mapped` : 'Not mapped'}
        </span>
      </div>
      <p className="text-sm text-gray-500">
        Players see this on the score entry screen. Find the course, import the holes from OpenStreetMap, then drag any
        pin that's off. Yellow numbers are tees and white numbers are greens.
      </p>

      <div className="flex flex-wrap gap-2">
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              void handleSearch();
            }
          }}
          className={`${inputClass} min-w-0 flex-1`}
          placeholder="Course name and town"
        />
        <Button type="button" variant="outline" size="sm" onClick={handleSearch} disabled={!map || searching}>
          {searching ? <Spinner className="h-4 w-4" /> : <Search className="mr-1 h-4 w-4" />}
          Find
        </Button>
        <Button type="button" variant="outline" size="sm" onClick={handleImport} disabled={!map || preview.isPending}>
          {preview.isPending ? <Spinner className="h-4 w-4" /> : <Download className="mr-1 h-4 w-4" />}
          Import from OpenStreetMap
        </Button>
      </div>
      {searchError && <p className="text-sm text-amber-700">{searchError}</p>}
      {preview.isError && <p className="text-sm text-red-600">{errorMessage(preview.error)}</p>}
      <p className="text-xs text-gray-500">The import looks for holes around the centre of the map, so centre it on the course first.</p>

      <div className="relative h-[420px] overflow-hidden rounded-lg border border-gray-200">
        {/* maplibre-gl.css forces position:relative on the container, so size it explicitly. */}
        <div ref={containerRef} className="h-full w-full" />
        {placing && (
          <div className="absolute inset-x-0 top-0 z-10 flex items-center justify-between gap-2 bg-black/70 px-3 py-2 text-sm text-white">
            <span>
              Hole {placing.holeNumber}: tap the {placing.tee ? 'centre of the green' : 'tee'}
            </span>
            <button type="button" className="underline" onClick={() => setPlacing(null)}>
              Cancel
            </button>
          </div>
        )}
      </div>

      {warnings.length > 0 && (
        <ul className="space-y-1 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          {warnings.map((w) => (
            <li key={w}>{w}</li>
          ))}
        </ul>
      )}

      <div className="flex flex-wrap gap-2">
        {sortedHoles.map((h) => {
          const mapped = draft[h.holeNumber];
          return (
            <div key={h.holeNumber} className="flex items-center rounded-full border border-gray-200 text-sm">
              {mapped ? (
                <>
                  <button type="button" className="px-3 py-1 font-medium text-gray-800" onClick={() => flyToHole(mapped)}>
                    {h.holeNumber}
                  </button>
                  <button
                    type="button"
                    className="border-l border-gray-200 px-2 py-1 text-gray-400 hover:text-red-600"
                    onClick={() => removeHole(h.holeNumber)}
                    title={`Remove hole ${h.holeNumber} from the map`}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  className="flex items-center gap-1 px-3 py-1 text-amber-700"
                  onClick={() => setPlacing({ holeNumber: h.holeNumber, tee: null })}
                  title={`Place hole ${h.holeNumber} by hand`}
                >
                  <MapPin className="h-3.5 w-3.5" />
                  {h.holeNumber}
                </button>
              )}
            </div>
          );
        })}
      </div>

      <div className="flex items-center justify-end gap-3">
        {save.isError && <p className="text-sm text-red-600">{errorMessage(save.error)}</p>}
        {save.isSuccess && !dirty && <p className="text-sm text-green-700">Saved</p>}
        <Button type="button" variant="primary" onClick={handleSave} disabled={!dirty || save.isPending}>
          {save.isPending && <Spinner className="mr-2 h-4 w-4" />}
          Save hole map
        </Button>
      </div>
    </div>
  );
}

const EDITOR_CSS = `
.hole-edit-tee, .hole-edit-green { min-width: 22px; height: 22px; padding: 0 4px; border-radius: 9999px; font: 700 11px/22px system-ui, sans-serif;
  text-align: center; cursor: grab; box-shadow: 0 1px 3px rgba(0,0,0,.6); }
.hole-edit-tee { background: #facc15; color: #111827; }
.hole-edit-green { background: #fff; color: #1B5E20; border: 2px solid #1B5E20; line-height: 18px; }
`;
