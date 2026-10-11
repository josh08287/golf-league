import { useEffect, useRef, useState, type RefObject } from 'react';
import maplibregl, { type Map as MaplibreMap, type StyleSpecification } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import type { GeoPoint } from '@/types/api';

// Esri World Imagery: free satellite tiles, no key; attribution is required.
const SATELLITE_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    satellite: {
      type: 'raster',
      tiles: ['https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}'],
      tileSize: 256,
      maxzoom: 19,
      attribution: 'Tiles © Esri — Maxar, Earthstar Geographics | Hole data © OpenStreetMap contributors',
    },
  },
  layers: [{ id: 'satellite', type: 'raster', source: 'satellite' }],
};

interface Options {
  center: GeoPoint;
  zoom: number;
  /** Allow two-finger / right-drag rotation. Off for players so the hole-up view can't be spun by accident. */
  allowRotate?: boolean;
}

/**
 * Creates a MapLibre satellite map in the given container and returns it
 * once its style has loaded (null until then; tiles may still be loading). The map is created once per
 * mount; later option changes are ignored — drive the camera through the
 * returned map instead.
 */
export function useSatelliteMap(containerRef: RefObject<HTMLDivElement>, options: Options): MaplibreMap | null {
  const [map, setMap] = useState<MaplibreMap | null>(null);
  const initial = useRef(options);

  useEffect(() => {
    if (!containerRef.current) return;
    const { center, zoom, allowRotate = false } = initial.current;
    const instance = new maplibregl.Map({
      container: containerRef.current,
      style: SATELLITE_STYLE,
      center: [center.lng, center.lat],
      zoom,
      maxZoom: 20,
      dragRotate: allowRotate,
      pitchWithRotate: false,
      touchPitch: false,
      attributionControl: { compact: true },
    });
    if (!allowRotate) instance.touchZoomRotate.disableRotation();
    // 'style.load', not 'load': 'load' waits for the first tiles, so a slow
    // or failing tile request would leave the map (markers, taps) dead.
    instance.once('style.load', () => setMap(instance));
    return () => {
      setMap(null);
      instance.remove();
    };
  }, [containerRef]);

  return map;
}

/** A marker whose element never intercepts taps, so a tap on it still reaches the map (tap-to-measure). */
export function passiveMarker(
  el: HTMLElement,
  point: GeoPoint,
  map: MaplibreMap,
  anchor: maplibregl.PositionAnchor = 'center',
): maplibregl.Marker {
  el.style.pointerEvents = 'none';
  return new maplibregl.Marker({ element: el, anchor }).setLngLat([point.lng, point.lat]).addTo(map);
}

export function toLngLat(p: GeoPoint): [number, number] {
  return [p.lng, p.lat];
}

/** Approximate circle polygon (for the GPS accuracy ring). */
export function circlePolygon(center: GeoPoint, radiusM: number, steps = 48): GeoJSON.Feature<GeoJSON.Polygon> {
  const coords: [number, number][] = [];
  const dLat = radiusM / 111_320;
  const dLng = radiusM / (111_320 * Math.cos((center.lat * Math.PI) / 180));
  for (let i = 0; i <= steps; i++) {
    const t = (i / steps) * 2 * Math.PI;
    coords.push([center.lng + dLng * Math.cos(t), center.lat + dLat * Math.sin(t)]);
  }
  return { type: 'Feature', properties: {}, geometry: { type: 'Polygon', coordinates: [coords] } };
}
