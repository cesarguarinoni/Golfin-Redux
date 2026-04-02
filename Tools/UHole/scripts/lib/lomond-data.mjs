export const lomondMetadata = {
  courseId: "lomond-country-club",
  displayName: "Lomond Country Club",
  nativeName: "ローモンドカントリー倶楽部",
  website: "https://www.lomond-cc.com/course/",
  address: "2570-3 Ryoocho, Kameyama, Mie 519-0222, Japan",
  countryCode: "JP",
  parTotal: 72,
  overviewYardage: 7028,
  backTeeTotal: 7024,
  openedOn: "1997-10-08",
  architect: "Taizo Kawata"
};

export const lomondHoles = [
  { holeNumber: 1, par: 5, strokeIndex: 9, back: 531, regular: 509, front: 488, ladies: 458 },
  { holeNumber: 2, par: 4, strokeIndex: 3, back: 403, regular: 391, front: 378, ladies: 366 },
  { holeNumber: 3, par: 4, strokeIndex: 15, back: 368, regular: 349, front: 315, ladies: 285 },
  { holeNumber: 4, par: 3, strokeIndex: 13, back: 138, regular: 126, front: 124, ladies: 106 },
  { holeNumber: 5, par: 4, strokeIndex: 1, back: 425, regular: 383, front: 372, ladies: 366 },
  { holeNumber: 6, par: 3, strokeIndex: 7, back: 193, regular: 173, front: 149, ladies: 129 },
  { holeNumber: 7, par: 4, strokeIndex: 5, back: 430, regular: 410, front: 375, ladies: 335 },
  { holeNumber: 8, par: 5, strokeIndex: 17, back: 562, regular: 480, front: 469, ladies: 452 },
  { holeNumber: 9, par: 4, strokeIndex: 11, back: 434, regular: 403, front: 376, ladies: 357 },
  { holeNumber: 10, par: 4, strokeIndex: 10, back: 392, regular: 368, front: 346, ladies: 328 },
  { holeNumber: 11, par: 3, strokeIndex: 16, back: 179, regular: 162, front: 138, ladies: 120 },
  { holeNumber: 12, par: 4, strokeIndex: 4, back: 435, regular: 359, front: 348, ladies: 269 },
  { holeNumber: 13, par: 5, strokeIndex: 8, back: 597, regular: 559, front: 534, ladies: 508 },
  { holeNumber: 14, par: 4, strokeIndex: 2, back: 420, regular: 392, front: 373, ladies: 357 },
  { holeNumber: 15, par: 3, strokeIndex: 14, back: 184, regular: 169, front: 146, ladies: 121 },
  { holeNumber: 16, par: 4, strokeIndex: 18, back: 356, regular: 327, front: 316, ladies: 292 },
  { holeNumber: 17, par: 4, strokeIndex: 6, back: 410, regular: 379, front: 353, ladies: 328 },
  { holeNumber: 18, par: 5, strokeIndex: 12, back: 567, regular: 548, front: 521, ladies: 502 }
];

export function yardsToMeters(yards) {
  return Number((yards * 0.9144).toFixed(2));
}

export function buildHoleRecord(holeNumber, holeLink = null) {
  const hole = lomondHoles.find((item) => item.holeNumber === holeNumber);
  if (!hole) {
    throw new Error(`Unknown hole ${holeNumber}`);
  }

  const padded = String(holeNumber).padStart(2, "0");

  return {
    schema_version: "1.0.0",
    course_id: lomondMetadata.courseId,
    hole_number: hole.holeNumber,
    display_name: `Hole ${hole.holeNumber}`,
    par: hole.par,
    stroke_index: hole.strokeIndex,
    tee_yardages: {
      source_id: "official-course-page-hole-table",
      entries: [
        { tee_name: "Back", yards: hole.back, meters: yardsToMeters(hole.back) },
        { tee_name: "Regular", yards: hole.regular, meters: yardsToMeters(hole.regular) },
        { tee_name: "Front", yards: hole.front, meters: yardsToMeters(hole.front) },
        { tee_name: "Ladies", yards: hole.ladies, meters: yardsToMeters(hole.ladies) }
      ]
    },
    tee_sets: [
      { name: "Back", yards: hole.back, meters: yardsToMeters(hole.back) },
      { name: "Regular", yards: hole.regular, meters: yardsToMeters(hole.regular) },
      { name: "Front", yards: hole.front, meters: yardsToMeters(hole.front) },
      { name: "Ladies", yards: hole.ladies, meters: yardsToMeters(hole.ladies) }
    ],
    review_status: "ingested",
    geometry_status: "missing",
    assets: {
      official_references: [
        `holes/${padded}/source/official-map${holeLink ? pathSuffix(holeLink) : ".jpg"}`
      ],
      alignment_json: `holes/${padded}/alignment.json`
    },
    notes: holeLink
      ? [`Official hole detail link discovered at ${holeLink}`]
      : ["Official hole detail link not resolved yet; scraper should retry on the live page."]
  };
}

export function buildAlignmentRecord(holeNumber, holeLink = null) {
  const padded = String(holeNumber).padStart(2, "0");

  return {
    schema_version: "1.0.0",
    course_id: lomondMetadata.courseId,
    hole_number: holeNumber,
    status: "needs_base_map",
    official_map: {
      path: `holes/${padded}/source/official-map${holeLink ? pathSuffix(holeLink) : ".jpg"}`,
      source_url: holeLink,
      trusted_for: [
        "hole layout reference",
        "manual control point placement"
      ]
    },
    target_base_map: {
      provider: "gsi_or_other_licensable_source",
      path: null,
      license_checked: false
    },
    control_points: [],
    transform: {
      pixel_to_world_ready: false,
      residual_error_m: null
    },
    notes: [
      "Add a licensable aerial or orthophoto source before georeferencing.",
      "Place at least four well-spread control points before approving alignment."
    ]
  };
}

function pathSuffix(url) {
  try {
    const pathname = new URL(url).pathname;
    const ext = pathname.includes(".") ? pathname.slice(pathname.lastIndexOf(".")) : ".jpg";
    return ext.length <= 5 ? ext : ".jpg";
  } catch {
    return ".jpg";
  }
}
