# Mission Data Parsing — Cliffs Campaign Board

This document describes how `src/App.jsx` reads, normalises, and displays
campaign/mission data loaded from `campaign-data.json`.  Pass it to another
chat to give that session full context.

---

## 1. Campaign JSON structure

The app expects a single JSON file at `/campaign-data.json` (served from the
`public/` folder, or injected by the companion bridge).  The top-level shape:

```jsonc
{
  "title": "Battle of Britain",
  "period": "1940-07-04 to 1940-07-22",   // optional display string
  "map": {
    "image": "/strait_of_dover_map.jpg",   // path relative to public/
    "width": 2048,
    "height": 1798,
    "gameCalibration": [                   // optional per-map calibration
      { "name": "Hawkinge", "gameX": 234931.02, "gameY": 232454.51,
        "pixelX": 1260, "pixelY": 540 },
      ...
    ]
  },
  "days": [
    {
      "date": "1940-07-10",           // ISO date string, UTC midnight implied
      "title": "Kanalkampf Begins",
      "summary": "...",
      "weather": "Partly cloudy, 5-6 oktas cumulus ...",
      "missions": [ ... ]
    },
    ...
  ]
}
```

The `fallbackCampaign` constant in the code is used when the file cannot be
loaded; it has the same shape with a single placeholder day.

---

## 2. Campaign loading

`App` fetches `/campaign-data.json` inside a `useEffect` with a `no-store`
cache hint and an `AbortController` signal that is cancelled on unmount:

```js
const controller = new AbortController();
fetch("/campaign-data.json", { cache: "no-store", signal: controller.signal })
  .then(r => r.json())
  .then(data => setCampaign(data))
  .catch(err => { if (err.name !== "AbortError") setCampaign(fallbackCampaign); });
return () => controller.abort();
```

If the companion bridge is running it can also push updated campaign data via
`POST /api/set-campaign` (handled in `scripts/local-companion-server.mjs`).

---

## 3. Dates

### `getDayNumber(date, startDate = "1940-07-04")`

Converts an ISO date string to a 1-based campaign day number.

```js
const start = new Date(`${startDate}T00:00:00Z`);
const d     = new Date(`${date}T00:00:00Z`);
const result = Math.round((d - start) / 86400000) + 1;
return Number.isFinite(result) ? result : 1;   // guard against NaN
```

- Both dates are parsed as UTC midnight so timezone offsets never shift the day.
- The default start date is 4 July 1940 (historical start of the Battle of Britain).
- An invalid `date` value produces `NaN` from `new Date()`; the guard returns 1.

### Timeline display

`day.date` is also formatted for display via `Intl.DateTimeFormat` with
`timeZone: "UTC"` so the rendered date always matches the string in the JSON.

`campaignMonthLabel(campaign, days)` formats the first day's date as
`"July 1940"` (long month + year) for the timeline header.

---

## 4. Weather

### `weatherProfile(text, time)`

Classifies a free-text weather string into one of six buckets and returns
`{ label, Icon, tone }`:

| Bucket            | Keywords matched (regex on lowercased text)                        |
|-------------------|--------------------------------------------------------------------|
| Thunderstorms     | `thunder`, `cumulonimbus`, `storm`                                 |
| Rain showers      | `rain`, `shower`, `drizzle`, `wet`                                 |
| Overcast          | `overcast`, `cloudy`, `8 10ths`, `7-8`, `low cloud`, `large cumulus` |
| Partly cloudy     | `partly`, `broken`, `scattered`, `cumulus`                         |
| Night / low light | none of the above AND mission hour < 6 or ≥ 20                     |
| Clear             | fallback                                                           |

`time` is a 4-digit string like `"1400"`.  The hour is extracted by
`Number(String(time).slice(0, 2))`.

`day.weather` and `mission.weather` are both accepted; the component prefers
`mission.weather` when present:

```js
const weather = weatherProfile(
  mission?.weather || day?.weather || "",
  mission?.time
);
```

---

## 5. Map and coordinate system

### Reference image

`campaign.map.image` is a path to the map JPEG.  Default is
`/strait_of_dover_map.jpg` at 2048 × 1798 px.

### Hard-coded fallback calibration (`gameCalibration` constant)

Two anchor points derived from real IL-2 CloD coordinates:

```js
hawkinge: { gameX: 234931.02, gameY: 232454.51, pixelX: 1260, pixelY: 540 }
pihen:    { gameX: 277141.95, gameY: 206459.2,  pixelX: 1470, pixelY: 665 }
```

These define a simple linear scale used by `routeToMapPoint` (2-point linear
transform) which is the last-resort fallback in the display pipeline.

### Per-map calibration points (`map.gameCalibration`)

A JSON array of objects:
```jsonc
{ "name": "Hawkinge", "gameX": 234931.02, "gameY": 232454.51,
  "pixelX": 1260, "pixelY": 540 }
```

The `defaultCalibration` array (hard-coded) seeds known airfields for the
default Strait of Dover map.  `mergeCalibration(map)` merges these with any
`map.calibrationPoints` from the JSON, allowing missions to add or override
points without replacing the defaults.  For Tobruk maps, the default
calibration is skipped entirely (detected by `map.id`, `map.name`, or
`map.image` containing `"tobruk"`).

### `affineCoefficients(points, pixelKey)`

Fits a 3-parameter affine transform (a·gameX + b·gameY + c = pixel) using
least-squares via Gaussian elimination (`solve3x3`).  Requires ≥ 3 calibration
points.  Returns `[a, b, c]` or `null` if underdetermined.

### `mapGameToPixel(map, wp)`

Full conversion pipeline for a single waypoint:

1. Try full affine (needs ≥ 3 calibration points).
2. Fall back to 2-point linear scale if fewer points but ≥ 2 available.
3. Return `null` if no calibration points.

```js
pixelX = wp.x * cx[0] + wp.y * cx[1] + cx[2]
pixelY = wp.x * cy[0] + wp.y * cy[1] + cy[2]
```

---

## 6. Waypoints

### `getRouteWaypoints(route)`

Normalises the raw `route.waypoints` array.  Accepts multiple field name
conventions used by different campaign authors:

```js
x:        wp.x  ?? wp.X  ?? wp.gameX ?? wp.GameX
y:        wp.y  ?? wp.Y  ?? wp.gameY ?? wp.GameY
altitude: wp.altitude ?? wp.alt ?? wp.z ?? 0
speed:    wp.speed ?? wp.Speed ?? 0
```

Returns only waypoints where both `x` and `y` are finite numbers.

### Coordinate interpretation

The game uses a large flat-earth coordinate system.  Typical values for
Battle of Britain missions are:
- `gameX` ~ 150 000 – 350 000 (West–East across the map)
- `gameY` ~ 100 000 – 300 000 (South–North)

Some campaigns export waypoints already in pixel space (`route.coordinateSystem
= "pixel"` or `"map"`).  The display pipeline checks this first.

---

## 7. Route display pipeline

### `getRouteDisplayPoints(route, mapW, mapH, map)`

Four-stage waterfall; returns the first set of points that look visible on the
map canvas:

1. **Native pixel coords** — used if `route.coordinateSystem` is `"pixel"` or
   `"map"`, or if the raw `x`/`y` values already fall inside the map bounds
   (checked by `routeLooksVisible`).

2. **Per-map affine transform** — `mapGameToPixel` using the per-map
   calibration points.  Used if all waypoints converted successfully and the
   result is visible.

3. **Hard-coded 2-point scale** — `routeToMapPoint` using the Hawkinge/Pihen
   anchor.  Used if the result is visible.

4. **Schematic fit** — `fitWaypointsToMap` normalises the route into the map
   canvas using a uniform scale and centred offset, with a 16% padding margin.
   Each output point is tagged `{ ..., schematic: true }`.  This is the
   final fallback and always produces valid pixel coordinates.

### `routeLooksVisible(points, mapW, mapH)`

A point is "on-map" if it is within 18% of the canvas dimensions outside any
edge.  A route is visible if ≥ 45% of its points (minimum 2) are on-map.

### `dedupeRoutes(routes)`

Routes are deduplicated by a signature:
`id | aircraft | briefing | WP0_x:WP0_y ; WP1_x:WP1_y ; ...`

This prevents the same route appearing twice when `mission._allRoutes` and
`mission.routes` overlap.

---

## 8. Side inference

### `inferSide(entity)`

Joins the following fields (all optional): `id`, `unit`, `briefing`,
`airGroupId`, `aircraft`, `role`, `side`.  Lowercases and replaces non-alnum
with `_`, then tests regex chains:

```
Regia Aeronautica: ra, regia, aeronautica, italian, italia, squadriglia,
                   gruppo, cr42, g50, br20, cant, z1007, mc200, fiat, macchi

Luftwaffe: lw, luftwaffe, jg/kg/lg/stg/zg + digits, staffel, gruppe,
           aufkl, erprob, bf_N, me_N, he_N, do_N, ju_N

RAF: raf, raaf, sqn, squadron, spitfire, hurricane, blenheim,
     defiant, beaufighter, gladiator, wellington, kittyhawk, tomahawk, dh82
```

Falls back to `entity.side` or `"Unknown"`.

The regex deliberately avoids `\b` word boundaries because IL-2 CloD air-group
IDs use underscores (e.g. `Tobruk_RA_150Gruppo_363Sq`); the anchors `(^|_)` and
`(_|$)` are used instead.

---

## 9. Aircraft identification

### `prettyAircraft(value)`

Cosmetic cleanup: replaces underscores with spaces, normalises hyphenated
prefixes (`Bf-109` → `Bf 109`), expands known concatenated names
(`HurricaneMkI` → `Hurricane Mk I`), etc.

### `aircraftFamilyLabel(value)`

Groups a specific aircraft name into a broad family for filter UI:
`"Spitfire"`, `"Hurricane"`, `"Bf 109"`, `"Ju 88"`, etc.  Uses lowercased
regex chains similar to `inferSide`.  Falls back to the first segment of
`prettyAircraft(value)`.

### `aircraftFamilyToken(value)`

Produces a short normalised token for scoring route–role matches:
`"spitfire"`, `"hurricane"`, `"109"`, `"do17"`, etc.

---

## 10. Mission filtering

### `missionWithDisplayFilters(mission, q, sideFilter, groupFilter, missionTypeFilter, aircraftFilter)`

Returns `null` if the mission should be hidden, otherwise a shallow copy with
filtered `playable` / `routes` arrays and two extra fields:

- `_allPlayable` — unfiltered roles (used when a role is pre-selected)
- `_allRoutes` — deduplicated, unfiltered routes

Filter logic:

1. **Mission type** — `missionMatchesKind` checks `missionKindForMission`
   against the active filter.  Runs before any other filter to drop whole
   missions fast.

2. **Side** — `sideMatches` uses `inferSide`.

3. **Group** — `roleMatchesGroup` compares `role.unit` / `role.id` to the
   selected group key (case-insensitive).

4. **Aircraft** — `roleMatchesAircraft` uses `aircraftFamilyToken` to compare
   `role.aircraft` to the selected family.

5. **Query** — If the core mission blob (`time`, `title`, `type`, `location`)
   matches, all side/group/aircraft-filtered roles/routes are shown.  If it
   does not match, only roles/routes whose search blob contains the query are
   shown.

### `missionKindLabel(text)` / `missionKindForMission(mission)`

Classifies a mission's type using keyword regex on a concatenated blob of
`mission.type`, `mission.title`, `mission.chapterTitle`, role roles/aircraft,
and route briefings/aircraft.  Categories: `Circus`, `Rodeo`, `Rhubarb`,
`Ramrod`, `Noball`, `Search & Rescue`, `Reconnaissance`, `Escort / Cover`,
`Anti-shipping`, `Bomber / Strike`, `Fighter / Patrol`, `Training`,
`Other / Special`.

---

## 11. Route–role matching

### `routeScoreForRole(route, role)`

Scores how well a route matches a playable role:

- Returns `-999` if sides differ.
- Starts at base score 5.
- Exact unit/ID token match → score ≥ 100.
- Partial token containment (≥ 3 chars) → score ≥ 85.
- Aircraft family match → +10.

`routesForRole` prefers routes scoring ≥ 80 ("strong match"), then falls back
to same-side routes, then any route with ≥ 2 waypoints.

---

## 12. Mission JSON — full example shape

```jsonc
{
  "time": "1400",               // 4-digit 24h string, e.g. "0630"
  "title": "First Convoy Attack",
  "type": "Convoy Battle",      // free text; also inferred if absent
  "location": "Pas-de-Calais",  // optional
  "briefing": "...",
  "chapterTitle": "...",        // optional chapter grouping
  "weather": "Scattered cloud, 4-5 oktas",  // overrides day.weather
  "playable": [
    {
      "id": "raf_249sqn_a",
      "unit": "249 Sqn",
      "aircraft": "HurricaneMkI",
      "role": "Intercept",
      "side": "RAF",            // optional; inferred by inferSide if absent
      "briefing": "...",
      "waypoints": []           // unused in playable roles; see routes
    }
  ],
  "routes": [
    {
      "id": "raf_249sqn_route_a",
      "airGroupId": "raf_249sqn_a",
      "briefing": "249 Sqn",
      "aircraft": "HurricaneMkI",
      "formation": "Vic",
      "coordinateSystem": "game",  // "game" | "pixel" | "map"
      "waypoints": [
        { "x": 234931.02, "y": 232454.51, "altitude": 3000, "speed": 280 },
        { "x": 240000.0,  "y": 225000.0,  "altitude": 4000, "speed": 300 }
      ]
    }
  ],
  "slides": [
    {
      "src": "/slides/249sqn_briefing.jpg",
      "section": "249sqn",
      "title": "249 Sqn briefing",
      "caption": "..."
    }
  ],
  "images": ["/gallery/convoy_attack.jpg"]
}
```

---

## 13. Companion bridge integration

`companionRequest(path, options)` wraps `fetch` to the local Node server on
port `__COMPANION_PORT__` (build-time Vite define, defaults to `8765`).

- Default timeout: 8 000 ms via `AbortSignal.timeout(8000)`.
- `timeoutMs: 0` disables the timeout (used for folder/file pickers that open
  native Windows dialogs which can wait indefinitely for user interaction).

Relevant endpoints:

| Method | Path                    | Purpose                                      |
|--------|-------------------------|----------------------------------------------|
| GET    | `/api/health`           | Check if bridge is running                   |
| POST   | `/api/browse-folder`    | Open native folder picker dialog             |
| POST   | `/api/browse-file`      | Open native file picker dialog               |
| POST   | `/api/scan-campaign`    | Scan a folder and return parsed campaign JSON|
| POST   | `/api/launch-mission`   | Write selected-mission.cmd and launch game   |
| POST   | `/api/set-campaign`     | Push campaign JSON directly (bypass file)    |

The bridge runs as a local Node process started separately
(`npm run companion`) or bundled with the Electron release.
