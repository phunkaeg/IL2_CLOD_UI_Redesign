# Cliffs Campaign Board — Technical Specification
> Written for Claude. Dense shorthand used throughout. Implement in any language/framework.
> All regex patterns are case-insensitive unless stated. JS regex syntax used for clarity.

---

## 1. OVERVIEW

A campaign board that:
1. Scans a campaign folder tree on disk
2. Parses `.mis` + `.briefing` + `campaigns.ini` files into a structured data model
3. Displays missions with filtering, tabbed detail view, interactive map, image gallery, pilot log
4. Has no backend at runtime — all parsing happens at scan time; result is a single JSON blob

Campaign supports three theatres: **Battle of Britain** (Strait of Dover map) and **Tobruk** (North Africa map). Theatre is auto-detected from folder/content.

---

## 2. DATA MODEL (output shapes)

All parsing produces this tree. Implement as classes/records in target language.

```
Campaign {
  title: string
  period: string            // "YYYY-MM-DD to YYYY-MM-DD"
  sourceRoot: string        // absolute path of scanned folder
  map: MapInfo
  days: Day[]
}

MapInfo {
  image: string             // URL or path to map image
  width: number             // px
  height: number            // px
  calibrationPoints: CalibPoint[]   // named reference airfields
  gameCalibration: GameCalibPoint[] // for affine coord transform
}

CalibPoint {
  name: string
  pixelX: number
  pixelY: number
  side: "RAF" | "Luftwaffe" | "Regia Aeronautica"
}

GameCalibPoint extends CalibPoint {
  gameX: number   // game world coordinate
  gameY: number
}

Day {
  date: string      // "YYYY-MM-DD"
  title: string
  summary: string
  weather: string
  missions: Mission[]
}

Mission {
  time: string          // "HHMM" zero-padded, e.g. "0730"
  title: string
  type: string          // inferred mission kind label
  briefing: string      // cleaned prose summary
  weather: string       // mission-level weather if present
  playable: Role[]      // flyable sections
  routes: Route[]       // all air groups (flyable + AI)
  slides: Slide[]       // briefing images
  images: string[]      // legacy image paths
  sourceFiles: string[] // absolute paths of .mis files used
  chapterTitle: string  // optional campaign chapter name
}

Role {
  id: string            // briefing section key, e.g. "54Sqn"
  unit: string          // display name, e.g. "No. 54 Squadron"
  side: "RAF" | "Luftwaffe" | "Regia Aeronautica" | "Unknown"
  aircraft: string      // pretty-printed name, e.g. "Spitfire Mk Ia"
  role: string          // e.g. "Fighter Sweep", "Bomber Escort"
  description: string   // full briefing text for this section
  objective: string     // short objective line
  airGroupId: string    // raw .mis section id, e.g. "RAF_54Sqn_SpitfireI.00"
}

Route {
  id: string
  airGroupId: string
  briefing: string      // display name
  unit: string
  side: string
  aircraft: string
  rawAircraft: string   // un-prettified
  formation: string
  skill: string
  waypoints: Waypoint[]
  coordinateSystem: "game" | "pixel" | "map"  // default "game"
}

Waypoint {
  action: string    // "NORMFLY", "TAKEOFF", "LANDING", etc.
  x: number         // game world X
  y: number         // game world Y
  alt: number       // altitude in metres
  speed: number
}

Slide {
  section: string       // briefing section key
  sectionName: string   // human name of section
  filename: string      // image filename
  src: string           // resolved URL/path
  caption: string
  title: string
}
```

---

## 3. DIRECTORY SCANNING

### 3a. Entry point
Scan starts from a user-specified `campaignRoot`. Two layouts are supported:

**Layout A — Single campaign (no subdirectories of missions)**
```
campaignRoot/
  campaigns.ini          ← optional
  Mission1/
    mission.mis
    mission.briefing
    slide01.jpg
  Mission2/
    ...
```

**Layout B — Multiple campaigns**
```
campaignRoot/
  MyCampaign/
    campaigns.ini
    01 - First Day/
      ...
    02 - Second Day/
      ...
```

### 3b. campaigns.ini format
```ini
[MyCampaign]
Name = Battle of Britain 1940
Date = 1940-07-04
...

[MyCampaign\Mission1]
Date = 1940-07-04
Name = Convoy Attack
```
- Use `Date` field for `Day.date`; fall back to alphabetical folder ordering + start date
- Use `Name` for `Day.title` or `Mission.title`
- Ignore localised duplicates (keys ending `_pl`, `_ru`, `_de`, `_fr`, `_es`, `_it`, `_cs`)

### 3c. Folder walk algorithm
```
for each subfolder in campaignRoot (sorted numerically/alphabetically):
  files = listFiles(subfolder)
  misFiles = files where ext == ".mis" and NOT isMissionFragment(file)
  if misFiles.empty: skip
  briefingFile = first .briefing file not matching /_(?:pl|ru|cs|de|es|fr|it)\.briefing$/i
  process misFiles → one or more Mission objects
  collect images (.jpg .png .webp) in same folder
```

**isMissionFragment**: returns true if filename matches `/_\d+\.mis$/i` (e.g. `mission_1.mis`).
When non-fragment .mis files exist, fragments are ignored.

### 3d. Selecting the primary .mis file
If folder contains multiple .mis files:
1. Prefer the file whose basename matches the folder's time prefix (e.g. folder `0730 - Convoy` → prefer `0730.mis`)
2. Else prefer the file whose basename matches the folder name exactly
3. Else use the first file alphabetically

---

## 4. PARSING — .mis FILE

The `.mis` format is an INI-style file with `[SectionName]` headers.

### 4a. Section extraction
```
parseMisSections(text) → Map<string, string>
Split on lines. Line matching /^\s*\[([^\]]+)\]\s*$/ starts a new section.
Key = trimmed section name. Value = all subsequent lines until next section header.
Strip UTF-8 BOM from start of file.
```

### 4b. Key fields from [MAIN] section
```
TIME    0.416667        → float hours since midnight → convert to "HHMM"
DayOffset -1            → integer, offset from campaign start date
player  RAF_54Sqn.00    → player's default air group id
```

**Time conversion**: `timeFloatToHHMM(f)` → `hours = floor(f)`, `minutes = round((f - hours) * 60)`, return `HH:MM` zero-padded. E.g. `0.416667` → `"1000"`.

### 4c. Air group sections
A section is an air group if its body contains `Class Aircraft.XxxYyy` or `Class XxxAircraft`.

```
For each section [SectionName] where body matches:
  /^\s*Class\s+Aircraft\.([^\s;]+)/mi  → aircraft = match[1]
  OR /^\s*Class\s+([^\s;]*Aircraft[^\s;]*)/mi → aircraft = last dot-segment

  briefing = body match /^\s*Briefing\s+"?(.+?)"?\s*$/mi
           OR body match /^\s*Name\s+"?(.+?)"?\s*$/mi
           OR SectionName

  formation = body match /^\s*Formation\s+(.+)$/mi
  skill     = body match /^\s*Skill\s+(.+)$/mi
```

### 4d. Waypoint sections
For air group id `FOO`, waypoints live in section `[FOO_Way]` or `[FOO.Way]`.
```
For each non-empty, non-comment line in waypoint section:
  parts = line.trim().split(/\s+/)
  action = parts[0]          // e.g. "NORMFLY"
  nums = parts[1..] converted to float, stripping trailing commas
  if nums.length >= 2:
    waypoint = { action, x: nums[0], y: nums[1], alt: nums[2]??0, speed: nums[3]??0 }
```

### 4e. Player detection
`playerPrefix = player field with trailing `.NN` stripped`
A section is **player-flyable** if its id starts with playerPrefix, OR if it appears as a named section in the .briefing file (see §5b).

---

## 5. PARSING — .briefing FILE

### 5a. Format
```
[SectionKey]
<Name> Human Name Of Section
<Description>
Full text of the briefing for this section.
Can be multiple paragraphs.
<Slide> filename.jpg
<Caption>
Caption text for this image.
<Slide> filename2.jpg
...

[AnotherSection]
...
```

### 5b. Section extraction algorithm
```
extractBriefingSections(text) → Section[]
Regex: /\[([^\]]+)\]([\s\S]*?)(?=\n\[[^\]]+\]|\s*$)/g
For each match:
  section = match[1].trim()
  body = match[2]
  name = body.match(/<Name>\s*([\s\S]*?)(?=<|$)/i)?.[1].trim() ?? section
  description = body.match(/<Description>\s*([\s\S]*?)(?=<Slide>|<Caption>|\n\[|$)/i)?.[1].trim() ?? ""
  slides = extract all <Slide>filename\n<Caption>text blocks
```

### 5c. Narrative vs flyable sections
A section is **narrative** (skip, not a flyable role) if its key matches:
```
/^(info|mainintro|intro|introduction|success|failure|fail|missionintro|
   missionsuccess|missionfailure|background|notes?|objectives?|
   results?|debrief|summary)$/i
```

### 5d. Matching briefing sections to air groups
```
For each non-narrative briefing section:
  a = section.key.toLowerCase().replace(/[^a-z0-9]+/g, "")
  Find matching airGroup where:
    b = airGroup.briefing.toLowerCase().replace(/[^a-z0-9]+/g, "")
    c = airGroup.id.toLowerCase().replace(/[^a-z0-9]+/g, "")
    match = (a === b) OR (a.includes(b) || b.includes(a)) OR (c.includes(a))
```

If no matching airGroup AND section text does not contain squadron/unit keywords:
```
/sqn|squadron|staffel|gruppe|jg|kg|lg|stg|raf|luftwaffe|regia|
 aeronautica|italian|italia|squadriglia|cr[._-]?42|g[._-]?50|
 br[._-]?20|fiat|macchi/i
```
→ skip this section (it's narrative content without a recognised unit pattern).

### 5e. cleanBriefingText
For the Mission.briefing summary field: prefer `<Description>` of [Info] section, then first `<Description>` found anywhere, then first `<Caption>`, then strip all tags and return plain text.

---

## 6. SIDE INFERENCE

Single function `inferSide(entity)` used throughout. Build a search string from all available fields:
```
text = join([entity.id, entity.unit, entity.briefing, entity.airGroupId,
             entity.aircraft, entity.role, entity.side], " ")
compact = text.toLowerCase().replace(/[^a-z0-9]+/g, "_")
```
Test in priority order (Regia Aeronautica first to avoid false RAF matches):
```
RA:  /(^|_)ra(_|$)|(^|_)regia(_|$)|aeronautica|italian|italia|
      squadriglia|gruppo|cr_?42|g_?50|br_?20|cant|z_?1007|
      mc_?200|fiat|macchi/

LW:  /(^|_)lw(_|$)|luftwaffe|(^|_)(jg|kg|lg|stg|zg)\d*|
      staffel|gruppe|aufkl|erprob|bf_?\d|me_?\d|he_?\d|do_?\d|ju_?\d/

RAF: /(^|_)raf(_|$)|(^|_)raaf(_|$)|sqn|squadron|spitfire|
      hurricane|blenheim|defiant|beaufighter|gladiator|
      wellington|kittyhawk|tomahawk|dh82/
```
Return matching side string, or `entity.side ?? "Unknown"`.

**Also used server-side** (in .mis parsing) as `inferAirSide(text)` — same patterns but operates on a plain string instead of an entity object.

---

## 7. AIRCRAFT CLASSIFICATION

### 7a. prettyAircraft(raw) — display name
```
Replace "_" → " "
Replace /\bBf-/ → "Bf "
Replace /\bMe-/ → "Me "   (and He-, Do-, Ju-)
Replace /HurricaneMkI\b/ → "Hurricane Mk I"
Replace /SpitfireMkIa\b/ → "Spitfire Mk Ia"
Replace /SpitfireMkI\b/  → "Spitfire Mk I"
Replace /100oct/ → "100 oct"
Collapse whitespace, trim.
```

### 7b. aircraftFamilyLabel(raw) — family grouping
Normalise: `text = raw.toLowerCase().replace(/[_-]+/g, " ")`
```
/spitfire/          → "Spitfire"
/hurricane/         → "Hurricane"
/blenheim/          → "Blenheim"
/boston/            → "Boston"
/typhoon/           → "Typhoon"
/beaufighter/       → "Beaufighter"
/defiant/           → "Defiant"
/bf\s*109|me\s*109|\b109\b/ → "Bf 109"
/bf\s*110|me\s*110|\b110\b/ → "Bf 110"
/fw\s*190|fw190|\b190\b/    → "Fw 190"
/ju\s*88|ju88/              → "Ju 88"
/ju\s*87|ju87|stuka/        → "Ju 87 Stuka"
/he\s*111|he111/            → "He 111"
/do\s*17|do17/              → "Do 17"
/do\s*217|do217/            → "Do 217"
/fiat|g\.?50/               → "Fiat G.50"
/cr\.?42/                   → "Fiat CR.42"
/mc\.?200|macchi/           → "Macchi C.200"
else                         → first segment of prettyAircraft(raw)
```

### 7c. aircraftFamilyToken(raw) — short token for route-role matching
```
normalise: t = raw.toLowerCase().replace(/[^a-z0-9]+/g, "")
t.includes("spitfire")           → "spitfire"
t.includes("hurricane")          → "hurricane"
t.includes("bf109")||"me109"||"109" → "109"
t.includes("bf110")||"me110"||"110" → "110"
t.includes("do17")||"dornier"    → "do17"
t.includes("he111")||"heinkel"   → "he111"
t.includes("ju87")||"stuka"      → "ju87"
t.includes("ju88")               → "ju88"
t.includes("blenheim")           → "blenheim"
else                              → t.slice(0, 8)
```

---

## 8. MISSION KIND CLASSIFICATION

`missionKindLabel(text)` — called with concatenation of mission title, type, chapter title, role text, route text.

```
/\bcircus\b/                          → "Circus"
/\brodeo\b/                           → "Rodeo"
/\brhubarb\b/                         → "Rhubarb"
/\bramrod\b/                          → "Ramrod"
/\bnoball\b|no-ball/                  → "Noball"
/rescue|seenot|search/                → "Search & Rescue"
/recon|recce|aufkl|photo/             → "Reconnaissance"
/escort|cover|umbrella/               → "Escort / Cover"
/convoy|shipping|ship|e-boat|boat|
 barge|harbour|harbor|anchorage|
 minefield/                           → "Anti-shipping"
/bomb|bomber|bombing|strike|attack|
 battery|factory|raid|suppression|
 jabo|ju-88|ju88|blenheim|boston|
 he-111|he111|do-17|do17|do-217|do217/ → "Bomber / Strike"
/fighter|intercept|scramble|patrol|
 sweep|dogfight|furball|cap/          → "Fighter / Patrol"
/training/                            → "Training"
else                                  → "Other / Special"
```

---

## 9. TIME / DATE PARSING

### 9a. timeOfDayLabel(HHMM) — narrative label
```
hour = parseInt(HHMM.slice(0, 2))
< 5  → "Night sortie"
< 7  → "First light"
< 11 → "Morning patrol"
< 14 → "Midday operations"
< 17 → "Afternoon operations"
< 20 → "Evening patrol"
else → "Night operations"
```

### 9b. weatherProfile(text, time) → { label, icon, tone }
```
/thunder|cumulonimbus|storm/  → "Thunderstorms"
/rain|shower|drizzle|wet/     → "Rain showers"
/overcast|cloudy|8 10ths|7-8|low cloud|large cumulus/ → "Overcast"
/partly|broken|scattered|cumulus/ → "Partly cloudy"
hour < 6 or >= 20             → "Night / low light"
else                          → "Clear"
```

### 9c. parseBriefingDateTime(text) → { date, time }
Extracts date and time from free-form briefing prose.

**Time patterns** (in priority order):
```
/\b(\d{4})\s*(?:hrs?|hours?)\b/i          → "HHMM"
/\bat\s+(\d{1,2})[:.h](\d{2})/i           → "HHMM"
/\b(\d{1,2})[:.h](\d{2})\s*(?:hrs?|am|pm)?/i
/\b(\d{4})\b/ where value 0000-2359
```

**Date patterns**:
```
/\b(\d{4})-(\d{2})-(\d{2})\b/             → ISO direct
/\b(\d{1,2})\s+([A-Za-z]+)\s+(\d{4})\b/  → "9 April 1941"
/\b(\d{1,2})(?:st|nd|rd|th)?\s+of\s+([A-Za-z]+)\s*,?\s*(\d{4})\b/
Month name map: january→01, february→02, ... december→12
```

---

## 10. COORDINATE SYSTEMS & MAP CALIBRATION

### 10a. The problem
Game world coordinates (large floats, e.g. `234931.02, 232454.51`) must be mapped to pixel coordinates on the map image.

### 10b. Affine transform (preferred — requires ≥3 calibration points)
Given N points each with `{ gameX, gameY, pixelX, pixelY }`:

Fit: `pixelX ≈ a·gameX + b·gameY + c` and separately for Y.

Use least-squares via the normal equations (3×3 linear system):
```
AᵀA · coeffs = Aᵀb
Solve with Gaussian elimination with partial pivoting.
Returns [a, b, c] for X and [a, b, c] for Y.

mapGameToPixel(wp):
  px = a_x·wp.gameX + b_x·wp.gameY + c_x
  py = a_y·wp.gameX + b_y·wp.gameY + c_y
```

### 10c. Linear fallback (2 calibration points)
```
sx = (b.pixelX - a.pixelX) / (b.gameX - a.gameX)
sy = (b.pixelY - a.pixelY) / (b.gameY - a.gameY)
px = a.pixelX + (wp.gameX - a.gameX) * sx
py = a.pixelY + (wp.gameY - a.gameY) * sy
```

### 10d. Default calibration points (Battle of Britain map, 2048×1798px)
```
Hawkinge:           pixelX=1260, pixelY=540,  gameX=234931.02, gameY=232454.51
Pihen-lès-Guînes:  pixelX=1470, pixelY=665,  gameX=277141.95, gameY=206459.20
```
These two points define the default linear transform used as fallback when no `gameCalibration` array is provided in the campaign JSON.

### 10e. Coordinate detection pipeline
For each route, attempt transforms in order until one produces a "visible" result:
```
1. If route.coordinateSystem == "pixel": use waypoints as-is (already pixel coords)
2. If native (x,y) values look like pixel coords → use as-is
   (routeLooksVisible test — see §10f)
3. Try affine/linear transform using map.gameCalibration points
4. Try default linear transform (Hawkinge + Pihen)
5. Fallback: fitWaypointsToMap — schematic layout
```

### 10f. routeLooksVisible(points, mapW, mapH)
```
marginX = mapW * 0.18
marginY = mapH * 0.18
visibleCount = points where:
  x >= -marginX AND x <= mapW + marginX AND
  y >= -marginY AND y <= mapH + marginY
return visibleCount >= max(2, ceil(points.length * 0.45))
```

### 10g. fitWaypointsToMap (schematic fallback)
When no calibration works, scale waypoints to fit map with padding:
```
pad = 0.16
drawW = mapW * (1 - pad*2),  drawH = mapH * (1 - pad*2)
scale = min(drawW / spanX, drawH / spanY)
offsetX = (mapW - spanX*scale) / 2
offsetY = (mapH - spanY*scale) / 2
point.x = offsetX + (wp.x - minX) * scale
point.y = offsetY + (wp.y - minY) * scale
mark as { schematic: true } → rendered dashed
```

---

## 11. ROUTE-ROLE MATCHING

### 11a. routeScoreForRole(route, role) → number
Used to find which route best represents a given flyable role.
```
if inferSide(route) != inferSide(role): return -999

routeUnitBits = normaliseToken([route.briefing, route.id, route.airGroupId])
roleUnitBits  = normaliseToken([role.unit, role.id])
routeAircraft = aircraftFamilyToken(route.aircraft)
roleAircraft  = aircraftFamilyToken(role.aircraft)

score = 5   // baseline (same side)

for rb in roleUnitBits:
  for tb in routeUnitBits:
    if rb == tb and both non-empty:          score = max(score, 100)
    elif len>=3 and (rb.includes(tb) or tb.includes(rb)): score = max(score, 85)

if routeAircraft == roleAircraft and both non-empty: score += 10

return score
```

`normaliseToken(v)` = `v.toLowerCase().replace(/[^a-z0-9]+/g, "")`

### 11b. routesForRole(routes, role)
```
usable = routes where waypoints.length >= 2
if !role: return usable.slice(0, 1)

scored = usable.map(r => { route: r, score: routeScoreForRole(r, role) })
         .filter(s => s.score > -999)
         .sort descending by score

strong = scored where score >= 80
if strong: return [strong[0].route]

// fallback: same side, first available
sameSide = usable where inferSide(route) == inferSide(role)
return sameSide.length ? [sameSide[0]] : [usable[0]]
```

### 11c. Route deduplication
Before displaying, deduplicate routes by signature:
```
sig = `${route.id}|${route.aircraft}|${route.briefing}|${waypoints.map(w => round(w.x)+":"+round(w.y)).join(";")}`
Keep first occurrence of each unique sig.
```

---

## 12. SLIDE-ROLE MATCHING

### 12a. slideMatchesRole(slide, role) → boolean
```
hay     = normaliseToken([slide.section, slide.sectionName, slide.title, slide.filename])
needles = normaliseToken([role.unit, role.id]).filter(len >= 2)
return needles.some(n => hay.some(h => h.includes(n) || n.includes(h)))
```

### 12b. slideLooksGeneral(slide) → boolean
```
text = [slide.section, slide.sectionName, slide.title].join(" ").toLowerCase()
return !text || /^info\b/.test(text)
```

### 12c. slidesForSelection(slides, selectedRole, sideFilter)
```
if selectedRole:
  direct  = slides.filter(s => slideMatchesRole(s, selectedRole))
  general = slides.filter(slideLooksGeneral).slice(0, 1)
  if direct.length: return dedupe([...general, ...direct])

if sideFilter == "All": return slides

sideTerms:
  RAF:  /\b(sqn|squadron|raf|spitfire|hurricane|blenheim|defiant)\b/i
  RA:   /\b(regia|aeronautica|italian|cr\.?42|g\.?50|br\.?20|cant|z\.?1007|mc\.?200|macchi|fiat)\b/i
  LW:   /\b(jg|kg|lg|stg|staffel|gruppe|luftwaffe|messerschmitt|heinkel|dornier|junkers|bf-|me-|he-|do-|ju-)\b/i

filtered = slides where slideLooksGeneral(s) OR sideTerms.test(joinedFields)
return filtered.length ? filtered : slides   // fallback to all
```

---

## 13. UI FILTERING PIPELINE

Four independent filter axes plus a free-text query. All applied together.

### 13a. Filter values
```
sideFilter:        "All" | "RAF" | "Luftwaffe" | "Regia Aeronautica"
groupFilter:       "All" | unit/briefing string (exact, case-insensitive)
missionTypeFilter: "All" | missionKindLabel string
aircraftFilter:    "All" | "family:TOKEN" | "variant:TOKEN"
query:             free text (searched against title, time, type, role fields)
```

### 13b. missionWithDisplayFilters(mission, q, side, group, type, aircraft)
```
if missionKindForMission(mission) != missionTypeFilter (and filter != "All"): return null

roles  = mission.playable.map(r => augment with inferSide)
routes = dedupeRoutes(mission.routes).map(r => augment with inferSide)

baseRoles  = roles.filter(r =>  sideMatches(r, side) AND groupMatches(r, group) AND aircraftMatches(r, aircraft))
baseRoutes = routes.filter(r => sideMatches(r, side) AND groupMatches(r, group) AND aircraftMatches(r, aircraft))

coreMatch = !query OR missionCoreBlob(mission).includes(query)
  where missionCoreBlob = join([time, title, type, location])

if coreMatch:
  displayRoles  = baseRoles
  displayRoutes = baseRoutes
  include = displayRoles.length OR displayRoutes.length
            OR (all filters == "All")
else:
  queryRoles  = baseRoles.filter(r => roleSearchBlob(r).includes(query))
  queryRoutes = baseRoutes.filter(r => routeSearchBlob(r).includes(query))
  include = queryRoles.length OR queryRoutes.length

if !include: return null
return { ...mission, playable: displayRoles/queryRoles, routes: displayRoutes/queryRoutes,
         _allPlayable: roles, _allRoutes: routes }
```

`roleSearchBlob`  = join([side, id, unit, aircraft, role]).toLowerCase()
`routeSearchBlob` = join([side, id, airGroupId, briefing, aircraft, formation]).toLowerCase()

### 13c. groupFilter population
```
extractGroups(days, sideFilter):
  map = Map<string, { value, label, side }>
  for each mission.playable where sideMatches:
    key = groupKey(role.unit || role.id)
    add if not present
  for each mission.routes where sideMatches:
    key = groupKey(route.briefing)
    add if not present
  return Array.from(map.values()).sort(label, numeric collation)

groupKey(v) = v.trim().toLowerCase()
```

### 13d. Aircraft filter option tree
Hierarchical: Family → Variants. Built by scanning all visible roles+routes.
```
families = Map<familyKey, { family, familyKey, variants: Map<variantKey, label> }>
aircraftFilterKey(raw, "family") = "family:" + normaliseToken(aircraftFamilyLabel(raw))
aircraftFilterKey(raw, "variant") = "variant:" + normaliseToken(prettyAircraft(raw))
```

---

## 14. TIMELINE MODES

Two display modes for the left-hand timeline:

### 14a. Day mode (default)
One entry per Day in filteredDays. Key = `"day:${dayIndex}"`.

### 14b. Time-slot mode
Group all missions across all days by their time field.
```
timeSlots = Map<"time:HHMM", {
  key, time, date (first occurrence), title = timeOfDayLabel(time),
  missions[], missionCount, roleCount
}>
Sort by time string ascending.
```
When time mode is active, selecting a slot shows all missions at that time across all days, with the first mission's weather used for display.

---

## 15. MAP COMPONENT

### 15a. State
```
zoom: number          // 1.0 to 5.0
panX, panY: number    // pixel offset of map origin
showLabels: boolean   // toggle airfield name labels
selectedRoleIndex: number
```

### 15b. Zoom behaviour
- Min zoom: 1.0 (fits map to container)
- Max zoom: 5.0
- Zoom pivot: pointer position (zoom in towards cursor)
- On zoom: adjust panX/panY to keep pointer-under-cursor stable
- Clamp pan so map cannot be dragged outside container bounds

```
// On wheel event:
newZoom = clamp(zoom * (delta > 0 ? 1.12 : 0.89), 1.0, 5.0)
ratio = newZoom / zoom
panX = pointerX - ratio * (pointerX - panX)
panY = pointerY - ratio * (pointerY - panY)
// Then clamp pan to valid range
```

### 15c. Route rendering (SVG overlay)
Rendered as `<svg>` overlay on top of map `<img>`, sharing same dimensions.

**Route selection**: Show routes for selected role (via routesForRole), or all routes if no role selected.

**Per-route rendering**:
```
points = getRouteDisplayPoints(route, mapW, mapH, map)
if points.length < 2: skip

// Two-pass: halo then coloured line
halo:  stroke="rgba(0,0,0,0.84)", strokeWidth = routeStroke * 3.5
line:  stroke = sideLine[route.side], strokeWidth = routeStroke

// Dashed if schematic (fallback layout):
strokeDasharray = schematic ? `${14/zoom} ${10/zoom}` : none

// Waypoint dots at each point:
outer circle: r = 8/zoom, fill="rgba(0,0,0,0.75)"
inner circle: r = 5/zoom, fill = sideLine[route.side]
```

**Side colours**:
```
RAF:              "#38bdf8"   (sky blue)
Luftwaffe:        "#f97316"   (orange)
Regia Aeronautica:"#22c55e"   (green)
Unknown:          "#fbbf24"   (amber)
```

**Stroke widths** scale inversely with zoom:
```
inverseZoom = 1 / zoom
routeStroke = 3.5 * inverseZoom
routeHaloStroke = routeStroke * 3.5
waypointOuterRadius = 8 * inverseZoom
waypointInnerRadius = 5 * inverseZoom
```

### 15d. Reference point labels
Airfield/location labels from `map.calibrationPoints`.
```
For each calibrationPoint where pixelX/Y is within map bounds:
  Render as pill label at (pixelX, pixelY)
  Scale inversely with zoom (transform: scale(1/zoom))
  Style by side:
    RAF:              sky-950 bg, sky-200 border, sky-100 text
    Luftwaffe:        slate-950 bg, slate-200 border, slate-100 text
    Regia Aeronautica:emerald-950 bg, emerald-200 border, emerald-100 text
```

### 15e. Compact map (Overview tab preview)
Same component, `compact` prop:
- No zoom/pan controls
- Touch/scroll disabled
- Fixed height container
- Double-click navigates to full Map tab

---

## 16. THEATRE DETECTION

Used to auto-select the correct map image.

```
isTobruk(campaignRoot, days):
  text = campaignRoot + all mission titles + briefings
  return /tobruk|libya|cyrenaica|north.?africa|desert|
          western.?desert|middle.?east|mediterranean/i.test(text)

Maps:
  BoB:    strait_of_dover_map.jpg  (2048 × 1798)
  Tobruk: tobruk_map.jpg           (width/height as provided)
```

Default calibration points are BoB-specific. Tobruk campaigns start with empty `calibrationPoints` (game-to-pixel calibration must be provided in campaign data or derived from game files).

---

## 17. PILOT LOG

Simple append-only log of mission outcomes. Stored locally (localStorage in web version; a JSON file in desktop).

### 17a. Entry shape
```
PilotLogEntry {
  id: string                    // timestamp + random
  createdAt: ISO8601
  dayDate, dayTitle: string
  missionTime, missionTitle, missionType: string
  side, unit, aircraft, role: string
  result: "Completed" | "KIA" | "MIA" | "Bailed" | "Captured" | "Wounded" | "RTB Damaged"
  claimsAir: string             // number as string e.g. "2"
  claimsGround: string
  damage: string                // "None recorded" | free text
  landing: string               // "Returned to base" | free text
  duration: string              // "HH:MM" or free text
  notes: string
  autoEvents: BridgeEvent[]     // injected from in-game bridge
}
```

### 17b. In-game bridge (optional C# script)
`CloD_PilotLogBridge.cs` runs as an IL-2 mission script.
Writes events to `events.jsonl` (one JSON object per line):
```
{ "type": "Takeoff" | "Landing" | "Hit" | "Kill", "time": float, "actor": string, ... }
```
The board polls this file and pre-fills the log draft with auto-detected events.

---

## 18. UNIT EXTRACTION

`unitFromAirGroupId(id, briefing)` — extracts human-readable unit from raw air group id.

```
// id examples: "RAF_54Sqn_SpitfireI.00", "Tobruk_RA_150Gruppo_363Sq.00"
Strip trailing .NN
Split on _ 
Find segment matching squadron/gruppe/staffel pattern:
  /(\d+)\s*(?:sqn|squadron|sq\.?)/i  → "No. N Squadron"
  /(\d+)\s*(?:gruppe?|gr\.?)/i       → "N Gruppe"
  /(\d+)\s*(?:staffel|st\.?)/i       → "N Staffel"
  /(\d+)\s*(?:gruppo|gr\.?)/i        → "N Gruppo"
  /jg(\d+)/i                         → "JG N"
  /kg(\d+)/i                         → "KG N"
  /stg(\d+)/i                        → "StG N"
Fallback: return briefing string or id
```

---

## 19. NOTES FOR IMPLEMENTORS

- **All file I/O is synchronous at scan time**; the resulting JSON is the only runtime dependency.
- **Side inference is intentionally greedy** — false positives on "Unknown" are acceptable; false negatives (wrong side) must be avoided.
- **The affine solver is numerically stable enough** for the coordinate ranges used (~100k–500k game units to ~2000px maps). No need for double-precision beyond what the language natively uses.
- **Briefing section matching is fuzzy by design** — exact match preferred, substring match accepted, to handle naming inconsistencies across campaign authors.
- **`missionKindForMission` builds its text from the mission itself plus all role and route text** — not just the mission title — because many campaigns embed type information in air group names rather than mission names.
- **Route rendering uses two SVG `<path>` elements per route** (halo + colour) to ensure readability over both light and dark map regions.
- **Zoom/pan state is not persisted** — resets on mission change.
- **The `_allPlayable` / `_allRoutes` fields on filtered missions** preserve the unfiltered arrays for map and gallery display, which should always show all routes for context even when roles are filtered.
