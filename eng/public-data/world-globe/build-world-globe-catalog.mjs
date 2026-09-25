import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";

const naturalEarthCommit = "ca96624a56bd078437bca8184e78163e5039ad19";
const defaults = {
  naturalEarth: `https://raw.githubusercontent.com/nvkelso/natural-earth-vector/${naturalEarthCommit}/geojson/ne_110m_admin_0_countries.geojson`,
  worldBankCountries: "https://api.worldbank.org/v2/country?format=json&per_page=400",
  worldBankPopulation: "https://api.worldbank.org/v2/country/all/indicator/SP.POP.TOTL?format=json&mrnev=1&per_page=400",
};

const args = parseArgs(process.argv.slice(2));
if (!args.output) fail("--output 경로가 필요합니다.");

const naturalEarthBytes = await load(args.naturalEarth ?? defaults.naturalEarth);
const worldBankCountryBytes = await load(args.worldBankCountries ?? defaults.worldBankCountries);
const worldBankPopulationBytes = await load(args.worldBankPopulation ?? defaults.worldBankPopulation);

const naturalEarth = JSON.parse(naturalEarthBytes.toString("utf8"));
const worldBankCountriesEnvelope = JSON.parse(worldBankCountryBytes.toString("utf8"));
const worldBankPopulationEnvelope = JSON.parse(worldBankPopulationBytes.toString("utf8"));

if (naturalEarth?.type !== "FeatureCollection" || !Array.isArray(naturalEarth.features))
  fail("NaturalEarthFeatureCollectionInvalid");
if (!Array.isArray(worldBankCountriesEnvelope) || !Array.isArray(worldBankCountriesEnvelope[1]))
  fail("WorldBankCountryEnvelopeInvalid");
if (!Array.isArray(worldBankPopulationEnvelope) || !Array.isArray(worldBankPopulationEnvelope[1]))
  fail("WorldBankPopulationEnvelopeInvalid");

const worldBankCountries = new Map(
  worldBankCountriesEnvelope[1]
    .filter(item => item && /^[A-Z]{3}$/.test(item.id ?? "") && item.region?.id)
    .map(item => [item.id, item]));
const worldBankPopulations = new Map(
  worldBankPopulationEnvelope[1]
    .filter(item => item && /^[A-Z]{3}$/.test(item.countryiso3code ?? ""))
    .map(item => [item.countryiso3code, item]));

const countries = naturalEarth.features
  .map(toCountry)
  .filter(Boolean)
  .sort((left, right) => left.stableId.localeCompare(right.stableId, "en"));

const ids = new Set();
for (const country of countries) {
  if (ids.has(country.stableId)) fail(`CountryStableIdDuplicate:${country.stableId}`);
  ids.add(country.stableId);
}
if (countries.length < 170) fail(`CountryCountTooSmall:${countries.length}`);

const naturalEarthHash = sha256(naturalEarthBytes);
const countryHash = sha256(worldBankCountryBytes);
const populationHash = sha256(worldBankPopulationBytes);
const worldBankUpdated = worldBankPopulationEnvelope[0]?.lastupdated ?? "unknown";
const sourceRevision = `ne-${naturalEarthCommit.slice(0, 12)}+wb-${worldBankUpdated}`;

const catalog = {
  schemaVersion: "world-globe-country-catalog.v1",
  revision: sourceRevision,
  generatedAtUtc: worldBankUpdated === "unknown"
    ? "1970-01-01T00:00:00Z"
    : `${worldBankUpdated}T00:00:00Z`,
  distributionApproved: true,
  observationPresentationOnly: true,
  boundaryNotice: "Natural Earth 1:110m 경계는 저해상 관찰 표현 자료이며 법적·외교적 경계 확정 자료가 아닙니다.",
  sources: [
    {
      sourceId: "natural-earth-admin0-110m",
      datasetId: "ne_110m_admin_0_countries",
      sourceName: "Natural Earth",
      sourceUrl: defaults.naturalEarth,
      sourceRevision: naturalEarthCommit,
      licenseCode: "PublicDomain",
      contentHashSha256: naturalEarthHash,
      contentLength: naturalEarthBytes.length,
      coordinateReferenceSystem: "EPSG:4326",
      attribution: "Made with Natural Earth",
    },
    {
      sourceId: "world-bank-country-api-v2",
      datasetId: "country-metadata",
      sourceName: "World Bank API v2",
      sourceUrl: defaults.worldBankCountries,
      sourceRevision: worldBankUpdated,
      licenseCode: "CC-BY-4.0",
      contentHashSha256: countryHash,
      contentLength: worldBankCountryBytes.length,
      coordinateReferenceSystem: "WGS84",
      attribution: "World Bank",
    },
    {
      sourceId: "world-bank-wdi",
      datasetId: "SP.POP.TOTL",
      sourceName: "World Development Indicators",
      sourceUrl: defaults.worldBankPopulation,
      sourceRevision: worldBankUpdated,
      licenseCode: "CC-BY-4.0",
      contentHashSha256: populationHash,
      contentLength: worldBankPopulationBytes.length,
      coordinateReferenceSystem: "NotApplicable",
      attribution: "World Bank",
    },
  ],
  countries,
};

const output = resolve(args.output);
await mkdir(dirname(output), { recursive: true });
const serialized = `${JSON.stringify(catalog, null, 2)}\n`;
await writeFile(output, serialized, "utf8");
process.stdout.write(JSON.stringify({
  output,
  revision: sourceRevision,
  countryCount: countries.length,
  ringCount: countries.reduce((sum, item) => sum + item.rings.length, 0),
  pointCount: countries.reduce((sum, item) => sum
    + item.rings.reduce((ringSum, ring) => ringSum + ring.points.length, 0), 0),
  outputSha256: sha256(Buffer.from(serialized, "utf8")),
  sources: catalog.sources.map(source => ({
    sourceId: source.sourceId,
    contentLength: source.contentLength,
    contentHashSha256: source.contentHashSha256,
  })),
}, null, 2) + "\n");

function toCountry(feature) {
  const properties = feature?.properties ?? {};
  const geometry = feature?.geometry;
  if (!geometry || !["Polygon", "MultiPolygon"].includes(geometry.type)) return null;

  const iso2 = validCode(properties.ISO_A2, 2) ? properties.ISO_A2 : "";
  const iso3 = firstCode(3, properties.ISO_A3, properties.WB_A3, properties.ADM0_A3);
  const naturalEarthCode = firstCode(3, properties.ADM0_A3, properties.SOV_A3, properties.BRK_A3);
  if (!iso2 && !naturalEarthCode) return null;
  const worldBankCode = firstCode(3, properties.WB_A3, properties.ISO_A3, properties.ADM0_A3);
  const worldBank = worldBankCountries.get(worldBankCode);
  const population = worldBankPopulations.get(worldBankCode);
  const polygons = geometry.type === "Polygon" ? [geometry.coordinates] : geometry.coordinates;
  const rings = [];
  let polygonIndex = 0;
  const bounds = { minLatitude: 90, maxLatitude: -90, minLongitude: 180, maxLongitude: -180 };
  for (const polygon of polygons) {
    let ringIndex = 0;
    for (const sourceRing of polygon) {
      const points = sourceRing
        .filter(value => Array.isArray(value) && value.length >= 2)
        .map(value => ({
          longitude: round(Number(value[0])),
          latitude: round(Number(value[1])),
        }));
      if (points.length < 4) continue;
      for (const point of points) {
        bounds.minLatitude = Math.min(bounds.minLatitude, point.latitude);
        bounds.maxLatitude = Math.max(bounds.maxLatitude, point.latitude);
        bounds.minLongitude = Math.min(bounds.minLongitude, point.longitude);
        bounds.maxLongitude = Math.max(bounds.maxLongitude, point.longitude);
      }
      rings.push({ polygonIndex, isHole: ringIndex > 0, points });
      ringIndex += 1;
    }
    polygonIndex += 1;
  }
  if (rings.length === 0) return null;

  const stableId = iso2
    ? `country:${iso2.toLowerCase()}`
    : `country:ne:${naturalEarthCode.toLowerCase()}`;
  const isKorea = iso2 === "KR";
  return {
    stableId,
    naturalEarthId: String(properties.NE_ID ?? ""),
    countryCode: iso2 || naturalEarthCode,
    iso2,
    iso3,
    naturalEarthCode,
    displayName: text(properties.NAME_KO) || text(properties.NAME) || text(properties.ADMIN),
    englishName: text(properties.NAME_EN) || text(properties.NAME) || text(properties.ADMIN),
    formalName: text(properties.FORMAL_EN),
    continent: text(properties.CONTINENT),
    region: text(worldBank?.region?.value) || text(properties.REGION_UN),
    subregion: text(properties.SUBREGION),
    capitalCity: text(worldBank?.capitalCity),
    centerLatitude: numberOrFallback(worldBank?.latitude, properties.LABEL_Y),
    centerLongitude: numberOrFallback(worldBank?.longitude, properties.LABEL_X),
    populationValue: Number.isSafeInteger(population?.value) ? population.value : -1,
    populationYear: text(population?.date),
    populationSourceId: population ? "world-bank-wdi" : "",
    dataAvailabilityCode: isKorea
      ? "AdministrativeAreaReady"
      : worldBank ? "CountryOverview" : "BoundaryOnly",
    maximumDetailCode: isKorea ? "AdministrativeArea" : "Country",
    bounds,
    rings,
  };
}

function parseArgs(values) {
  const parsed = {};
  for (let index = 0; index < values.length; index += 1) {
    const value = values[index];
    if (!value.startsWith("--")) fail(`UnknownArgument:${value}`);
    const key = value.slice(2).replace(/-([a-z])/g, (_, character) => character.toUpperCase());
    const next = values[index + 1];
    if (!next || next.startsWith("--")) fail(`ArgumentValueMissing:${value}`);
    parsed[key] = next;
    index += 1;
  }
  return parsed;
}

async function load(location) {
  if (/^https:\/\//i.test(location)) {
    const response = await fetch(location, { headers: { "user-agent": "Ssalddel-WorldGlobeCatalog/1.0" } });
    if (!response.ok) fail(`SourceDownloadFailed:${response.status}:${location}`);
    return Buffer.from(await response.arrayBuffer());
  }
  return readFile(resolve(location));
}

function firstCode(length, ...values) {
  return values.find(value => validCode(value, length)) ?? "";
}

function validCode(value, length) {
  return typeof value === "string"
    && new RegExp(`^[A-Z]{${length}}$`).test(value)
    && !/^X+$/.test(value);
}

function text(value) {
  return typeof value === "string" ? value.trim() : "";
}

function numberOrFallback(primary, fallback) {
  const value = Number(primary);
  if (Number.isFinite(value)) return round(value);
  const alternative = Number(fallback);
  return Number.isFinite(alternative) ? round(alternative) : 0;
}

function round(value) {
  return Math.round(value * 100000) / 100000;
}

function sha256(value) {
  return createHash("sha256").update(value).digest("hex").toUpperCase();
}

function fail(message) {
  throw new Error(message);
}
