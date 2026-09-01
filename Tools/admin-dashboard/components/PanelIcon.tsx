import type { PanelIcon as PanelIconName } from "@/lib/registry";

const PATHS: Record<PanelIconName, React.ReactNode> = {
  users: (
    <>
      <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
      <circle cx="9" cy="7" r="4" />
      <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
      <path d="M16 3.13a4 4 0 0 1 0 7.75" />
    </>
  ),
  coins: (
    <>
      <circle cx="8" cy="8" r="6" />
      <path d="M18.09 10.37A6 6 0 1 1 10.34 18" />
      <path d="M7 6h1v4" />
    </>
  ),
  flag: (
    <>
      <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" />
      <line x1="4" y1="22" x2="4" y2="15" />
    </>
  ),
  chart: (
    <>
      <line x1="18" y1="20" x2="18" y2="10" />
      <line x1="12" y1="20" x2="12" y2="4" />
      <line x1="6" y1="20" x2="6" y2="14" />
    </>
  ),
  shield: (
    <>
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
      <path d="m9 12 2 2 4-4" />
    </>
  ),
  megaphone: (
    <>
      <path d="M3 11v2a1 1 0 0 0 1 1h3l6 4V6L7 10H4a1 1 0 0 0-1 1z" />
      <path d="M17 9a3.5 3.5 0 0 1 0 6" />
      <path d="M7 14v5" />
    </>
  ),
  image: (
    <>
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <circle cx="8.5" cy="10" r="1.5" />
      <polyline points="21 16 16 11 5 19" />
    </>
  ),
  // ---- content catalogs (content_admin_panels) ----------------------------
  club: (
    <>
      <path d="M17 3v9.5a5 5 0 0 1-5 5 4 4 0 0 0-4 4" />
      <path d="M17 3h2.5L21 6l-4 1.5z" />
      <circle cx="6" cy="20" r="1.5" />
    </>
  ),
  // A dimpled circle — the ball itself, in the same stroke style as `club`.
  ball: (
    <>
      <circle cx="12" cy="12" r="9" />
      <circle cx="9" cy="9.5" r="1" />
      <circle cx="14.5" cy="9" r="1" />
      <circle cx="11.5" cy="14" r="1" />
      <circle cx="16" cy="13.5" r="1" />
    </>
  ),
  character: (
    <>
      <circle cx="12" cy="7" r="4" />
      <path d="M5.5 21a6.5 6.5 0 0 1 13 0" />
    </>
  ),
  box: (
    <>
      <path d="M21 8 12 3 3 8v8l9 5 9-5z" />
      <path d="m3 8 9 5 9-5" />
      <path d="M12 13v8" />
    </>
  ),
  text: (
    <>
      <path d="M4 7V5h16v2" />
      <path d="M12 5v14" />
      <path d="M9 19h6" />
    </>
  ),
  cart: (
    <>
      <circle cx="9" cy="20" r="1.5" />
      <circle cx="18" cy="20" r="1.5" />
      <path d="M2 3h3l2.6 12.4a1 1 0 0 0 1 .8h8.9a1 1 0 0 0 1-.8L21 7H6" />
    </>
  ),
  // A pin in a green — one flag per mode, the thing you tee off toward.
  flagpole: (
    <>
      <path d="M6 21V3" />
      <path d="M6 4h11l-2.5 3.5L17 11H6" />
      <path d="M3 21h8" />
    </>
  ),
  // A wrapped box — what an action pays out.
  gift: (
    <>
      <rect x="3" y="9" width="18" height="12" rx="1" />
      <path d="M3 13h18" />
      <path d="M12 9v12" />
      <path d="M12 9C10 5 4 5 5.5 8.2 6.2 9 12 9 12 9z" />
      <path d="M12 9c2-4 8-4 6.5-0.8C17.8 9 12 9 12 9z" />
    </>
  ),
  // Concentric rings — a mission is a target you either hit or you do not.
  target: (
    <>
      <circle cx="12" cy="12" r="9" />
      <circle cx="12" cy="12" r="5" />
      <circle cx="12" cy="12" r="1.5" />
    </>
  ),
  // Two interlocking pieces — the parts a mission is composed from.
  puzzle: (
    <>
      <path d="M4 6a2 2 0 0 1 2-2h3a2 2 0 1 1 4 0h3a2 2 0 0 1 2 2v3a2 2 0 1 1 0 4v3a2 2 0 0 1-2 2h-3a2 2 0 1 0-4 0H6a2 2 0 0 1-2-2v-3a2 2 0 1 0 0-4z" />
    </>
  ),
  // A month grid — the daily is one row per UTC date.
  calendar: (
    <>
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M3 10h18" />
      <path d="M8 3v4" />
      <path d="M16 3v4" />
    </>
  ),
  // A torn stub with a perforation — one pull, one ticket.
  ticket: (
    <>
      <path d="M3 8a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v1.5a2.5 2.5 0 0 0 0 5V16a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-1.5a2.5 2.5 0 0 0 0-5z" />
      <path d="M14 7v2" />
      <path d="M14 11v2" />
      <path d="M14 15v2" />
    </>
  ),
  // A rising staircase — levels, each one a step you pay for.
  ladder: (
    <>
      <path d="M3 20h5v-5h5v-5h5V5h3" />
      <path d="M3 20V9" />
    </>
  ),
};

export function PanelIcon({
  name,
  className = "h-4 w-4",
}: {
  name: PanelIconName;
  className?: string;
}) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden
    >
      {PATHS[name]}
    </svg>
  );
}
