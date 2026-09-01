/**
 * banner.test.ts — the dashboard half of the banner link allowlist.
 *
 * The Unity client ships its OWN copy of these rules (`BannerPolicy`), so a URL
 * the dashboard accepts and the client refuses is a banner that looks fine to
 * the operator and does nothing on the device. These cases are the mirror of
 * `BannerLinkAllowlistTests` in Assets/Scripts/TournamentsRuntime/Tests/.
 */
import { describe, expect, it } from "vitest";
import {
  ALLOWED_LINK_HOSTS,
  INTERNAL_LINK_ROUTES,
  validateBannerLinkUrl,
} from "../banner";

describe("validateBannerLinkUrl — in-app routes (gps_hub_entry §2)", () => {
  it("accepts golfin://gps exactly", () => {
    expect(validateBannerLinkUrl("golfin://gps")).toBeNull();
  });

  it("accepts it case-insensitively, matching Uri lower-casing on the client", () => {
    expect(validateBannerLinkUrl("GOLFIN://GPS")).toBeNull();
    expect(validateBannerLinkUrl("  golfin://GPS  ")).toBeNull();
  });

  it("refuses an in-app route the client does not enumerate", () => {
    // The client's switch has one case. Accepting more here would let an
    // operator save a link that silently does nothing on every device.
    expect(validateBannerLinkUrl("golfin://shop")).not.toBeNull();
    expect(validateBannerLinkUrl("golfin://gps/checkin")).not.toBeNull();
    expect(validateBannerLinkUrl("golfin://gps?tab=1")).not.toBeNull();
  });

  it("names the routes in the host-rejection message", () => {
    const err = validateBannerLinkUrl("https://evil-golfin.io/x");
    expect(err).toContain("golfin://gps");
    expect(err).toContain(ALLOWED_LINK_HOSTS[0]);
  });

  it("keeps INTERNAL_LINK_ROUTES to what the shipped client understands", () => {
    expect([...INTERNAL_LINK_ROUTES]).toEqual(["golfin://gps"]);
  });
});

describe("validateBannerLinkUrl — external links are unchanged", () => {
  it("accepts the four allowlisted hosts", () => {
    for (const h of ALLOWED_LINK_HOSTS) {
      expect(validateBannerLinkUrl(`https://${h}/x`)).toBeNull();
    }
  });

  it("accepts a query and fragment", () => {
    expect(validateBannerLinkUrl("https://golfin.io/campaign/august?utm=banner#top")).toBeNull();
  });

  it("still refuses the reject table", () => {
    const table: [string, string][] = [
      ["http://golfin.io", "http, not https"],
      ["https://evil-golfin.io", "prefix-adjacent host"],
      ["https://golfin.io.attacker.net", "suffix-adjacent host"],
      ["https://golfin.io:8443", "explicit port"],
      ["https://a@golfin.io", "userinfo"],
      ["https://sub.golfin.io", "no wildcard subdomains"],
      ["golfin.io/x", "no scheme"],
      ["javascript:alert(1)", "not https"],
      ["", "empty"],
    ];
    for (const [url, why] of table) {
      expect(validateBannerLinkUrl(url), `must reject (${why}): ${url}`).not.toBeNull();
    }
  });
});
