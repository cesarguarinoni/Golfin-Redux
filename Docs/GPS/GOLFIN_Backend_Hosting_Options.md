# GOLFIN Unified Backend — Hosting Options & Cost Report

**Scope:** one shared backend for the merged GOLFIN game + GPS app · async/turn-based multiplayer + leaderboards · **hundreds of MAU** at launch (→ low thousands in 6–12 mo) · **Japan-primary** (Tokyo).
**Method:** exhaustive web research across 3 directions, then 3-vote adversarial verification on every cost claim (20 confirmed, 5 killed as wrong/outdated). All prices are **2026** vendor figures; primary sources cited.

---

## TL;DR — Recommendation

**Stay on your current stack: Fly.io + Supabase. It is the cheapest option that actually fits, at ~$25–35/month all-in.**

The reason is structural, not just price: **async/turn-based multiplayer and leaderboards are already native to what you run.** Your ~60 REST endpoints, the 5-axis ranking endpoint, and the tournaments module already live on Postgres. Turn notifications and presence come from **Supabase Realtime** (websockets) with no new infrastructure. Nothing about "add multiplayer + leaderboards" requires a new class of server, because you explicitly do **not** need real-time netcode.

Every alternative is either more expensive, a worse fit, or a second backend that re-splits the data you're trying to unify:

- **Managed AWS** is the most expensive direction — always-on database + Tokyo egress are the traps.
- **Purpose-built game backends** (PlayFab, Nakama/Heroic Cloud) are real but don't undercut you at this scale, and they'd fragment your data away from the one Postgres both apps share.
- **Self-hosted VPS** wins on raw compute (~$4–6/mo) but loses on total cost of ownership once your ops time is priced in.

> The one thing to decide is Supabase **Free vs Pro ($25/mo)** — that's the difference between a ~$5/mo floor and ~$30/mo. See "What to measure before committing."

---

## Side-by-side, at hundreds of MAU

| Direction | Realistic monthly (hundreds of MAU) | Fits async MP + leaderboards? | One shared backend? | Curve to a few thousand MAU |
|---|---|---|---|---|
| **1. Extend Fly.io + Supabase** ✅ | **~$25–35** (Fly API $2–6 + Supabase Pro $25 + few-$ egress). Floor ~$5 on Supabase Free. | Yes — Postgres + Realtime, native | **Yes** (already unified) | Gentle: bump Fly machine to 1–2 GB; Supabase Pro still covers it → ~$30–50 |
| **2a. Managed AWS** | **~$40–90+** (Fargate/App Runner + Aurora/RDS + DynamoDB + egress) | Yes, but you assemble it | Yes, but more moving parts | Steeper: always-on DB + $0.114/GB Tokyo egress climb |
| **2b. PlayFab** | **$0 today** (Foundation ≤1,000 MAU) → **$99** Standard once you cross | Yes (built for it) | **No** — separate backend | Cliff at 1,000 MAU → $99/mo flat |
| **2c. Nakama / Heroic Cloud** | **Quote-only** (variable CPU pricing; no cheap flat tier) | Yes (purpose-built) | No — separate backend | Variable, CPU-driven |
| **3. Self-hosted VPS** | **~$4–6 compute** (Oracle Free Tier $0) **+ your ops time** | Yes (self-run PG + Nakama) | Yes | Compute flat; **ops burden grows** |

*Dollar figures for Fly/Supabase/AWS/PlayFab are from verified 2026 primary sources. VPS/Lightsail figures are indicative market rates (secondary sources) — treat as directional; the TCO argument is the reliable part.*

---

## Direction 1 — Extend Fly.io + Supabase (recommended)

**Compute (FastAPI on Fly.io).** `shared-cpu-1x` is cheap and granular: **$1.94/mo (256 MB)**, $3.19 (512 MB), $5.70 (1 GB), $10.70 (2 GB) always-on; `shared-cpu-4x` 8 GB is $42.79. Your machine is already **scale-to-zero** (`min_machines_running=0`): a stopped machine bills only rootfs storage at **$0.15/GB per 30 days**, so idle cost is pennies to ~$1/mo. A 256 MB machine left running full-time is ~$2.32/mo. [1][3]

**Realtime (the multiplayer/presence layer).** Supabase Realtime **Free** allows **200 concurrent websocket connections / 100 msg/s**; **Pro ($25/mo)** raises it to **500 / 500**, and Pro with the spend cap removed reaches **10,000 / 2,500**, plus 2M messages/month included. One client = one connection. Because play is **async/turn-based**, simultaneous connections at hundreds of MAU sit *far* below 200 — so **Free is technically sufficient**; you pick Pro for backups and production reliability, not for connection headroom. [2][6]

**Japan egress (the one region-specific line item).** Fly bills outbound egress by region group, and **Tokyo is in Asia-Pacific at $0.04/GB — double the $0.02 NA/EU rate.** New Fly orgs get **no free bandwidth allowance**. At hundreds of MAU for a turn-based game this is still only a few dollars/month, but it's a real doubled rate to note — and it can climb if the AI score-recognition image proxy pushes bandwidth. [4][1]

**Caveats to run with:** Fly has **no free tier, no billing cap, and no billing alerts** (legacy free plans deprecated Oct 7, 2024) — so keep an eye on the bill, though total exposure here is bounded to tens of dollars. A dedicated IPv4 is ~$2/mo. [3][5]

**All-in:** ~$5/mo (Supabase Free + scale-to-zero Fly) to ~$25–35/mo (Supabase Pro + a small always-on Fly machine + egress).

---

## Direction 2 — Managed cloud

**AWS — the most expensive direction, and it's about the database and egress, not the API.** Compute for the API (Fargate/App Runner/Lightsail) is affordable, but two traps dominate:

- **Always-on database.** Aurora Serverless v2 bills **$0.12/ACU-hr (Standard)** / $0.156 (I/O-Optimized), and **Tokyo runs ~$0.20/ACU-hr**. Aurora v2 *can* now auto-pause toward zero (see rejected myth #2), but a comparable always-on small DB is still a standing monthly cost that Supabase folds into its flat $25. [10]
- **Egress.** AWS gives the **first 100 GB/mo free, then $0.09/GB — but Tokyo (ap-northeast-1) is ~$0.114/GB**, ~25% above the headline. [10]

A realistic small AWS assembly (App Runner/Fargate API + Aurora/RDS + DynamoDB leaderboards + egress) lands **well above** the Fly+Supabase stack at this scale, for more assembly work. It only starts to make sense if you're already deep in the AWS ecosystem.

**PlayFab — real, but not cheaper and not unifying.** PlayFab is active in 2026 (GDC 2026 "Foundation Mode," monthly digests through March 2026) — *unlike* GameSparks, which Amazon shut down Sept 30, 2022. Its tiers are **$0 pay-as-you-go / $99 Standard / $1,999 Premium / $10,000+ Enterprise.** But in **March 2026 PlayFab cut its free tier from 100,000 MAU to 1,000** (a 99% cut). So at hundreds of MAU you'd fit the $0 Foundation tier *today* — but you'd be one growth spurt from the **$99/mo** step, on a free tier that just got gutted, and it's a **separate backend** that fragments the data both your apps share. Also note: legacy Insights analytics APIs retire March 31, 2026. Net: doesn't durably beat your stack, adds vendor risk. [7][12-rejected]

**Nakama via Heroic Cloud — no cheap flat price to compare.** Standard deployments use **variable pricing driven by CPU allocation** (1–64+ Nakama cores, 1–16+ DB cores) shown only through an interactive calculator; the smaller "Development Tier" is **"Contact us"** with no published figure. Adjacent products *do* list fixed prices (Satori from $600/mo, Studio $2,000/$6,000), so the omission is deliberate. You can't confirm it beats a $25–35/mo self-managed stack without a sales quote — and self-hosting Nakama just moves the cost to your ops time. [8]

**Firebase — keep it for analytics only.** The free **Spark** plan **hard-caps**: exceed a product's no-cost quota and that product is **shut off for the rest of the month** (no overage billing, because there's no payment method) — outages are the only failure mode. Moving past it means **Blaze** pay-as-you-go with metered billing. Fine where you already use it (analytics); not a cost-safe home for the data/leaderboard backend. [9]

---

## Direction 3 — Self-hosted VPS

**Cheapest compute, if your time is free.** A Tokyo-region VPS runs roughly **$4–6/mo** (indicative: Vultr ~$2.50 512 MB, Linode ~$5 1 GB, DigitalOcean ~$6; Amazon Lightsail Nano ~$5 / Micro ~$7 / Small ~$12), and **Oracle Cloud Always-Free** is $0 with a Tokyo region. You'd self-run Postgres and (if you wanted managed-style async features) Nakama. *These VPS prices are from secondary/blog sources — not adversarially confirmed here — so treat them as directional.*

**Why it loses on TCO for a solo dev.** The dollar delta vs managed Supabase is only ~$20–30/mo, and against that you take on: OS/security patching, Postgres upgrades, **backups you configure and test yourself**, uptime/on-call, bandwidth, and **no managed failover or point-in-time recovery**. For a solo dev (you), that labor typically costs more than the delta it saves. Plus a **Japan-latency penalty** if the box is hosted outside Japan — Hetzner (the cheapest EU option) has no Tokyo region; Oracle and Vultr do. **Self-host only if ops time is genuinely free and your reliability bar is relaxed** — which is a poor fit for a live points economy + IAP records.

---

## Hidden-cost / TCO — managed vs self-hosted

The headline compute numbers hide where money and risk actually accrue:

- **Database is the real cost center, not the API.** Every direction's cheap number is the app server; the spread comes from the database. Supabase's flat $25 (with backups + PITR) is the value here — AWS makes it a variable always-on line, self-hosting makes it *your* backup/failover problem.
- **Egress is a Japan tax.** Fly Tokyo $0.04/GB and AWS Tokyo ~$0.114/GB are both above their own headline rates. Measure real GB/mo — the AI Vision score-recognition proxy and image handling can push this well above a pure turn-based estimate.
- **Ops labor is the self-hosted iceberg.** Patching, backup verification, and on-call routinely exceed the $20–30/mo you'd save versus managed.
- **"Free" tiers fail differently.** Firebase Spark shuts the product off; Fly has no cap or alerts; PlayFab just cut its free MAU by 99%. Know each one's failure mode before you lean on it.

---

## Adversarially rejected (cut as wrong or outdated)

| Claim that was floating around | Verdict | Reality |
|---|---|---|
| "PlayFab is free until 100K players" | ✗ killed 0-3 | Free/Foundation tier was **cut to 1,000 MAU in March 2026**; first real paid step is **$99/mo**. |
| "Aurora Serverless v2 has a 0.5-ACU floor ⇒ ~$43.80/mo minimum always-on" | ✗ killed 0-3 | Aurora v2 **can auto-pause / scale toward zero**; the fixed-floor framing is outdated. |
| "Fly.io includes 100 GB/mo free egress at $0.02/GB" | ✗ killed 0-3 | **New Fly orgs get no free bandwidth**; Tokyo egress is $0.04/GB. |
| "Fly Managed Postgres $33.90/mo vs Supabase $60/mo" | ✗ killed 0-3 | Unreliable secondary figures — not used. |
| "Fly 256 MB machine = $2.09/mo" | ✗ killed 0-3 | Correct figure is **$1.94/mo**. |
| **GameSparks** as a game-backend option | ✗ cut | **Shut down by Amazon Sept 30, 2022.** Dead. |

---

## What to measure before committing

1. **Supabase Free vs Pro** — is Free-tier project-pausing-after-inactivity and 7-day backup retention OK for launch (~$5/mo floor), or do you want Pro ($25/mo) from day one for backups/reliability? This is the single biggest swing in the bill.
2. **Real monthly egress (GB)** — the AI score-recognition image proxy is the wildcard. Measure it; it drives both Fly ($0.04/GB) and any AWS ($0.114/GB) number.
3. **RPO/RTO for the points economy + IAP records** — if you need managed point-in-time recovery / failover, that's a strong vote for Supabase/RDS over self-hosting.
4. **If you ever add real-time synchronous play** (live shared shots) — *that's* when a relay/game server (Nakama, Photon) enters the picture. It's explicitly out of scope now, so it isn't priced here beyond this note.

---

## Sources (2026, primary unless noted)

1. Fly.io Resource Pricing — https://fly.io/docs/about/pricing/ *(primary)*
2. Supabase Realtime limits — https://supabase.com/docs/guides/realtime/limits *(primary)*
3. Fly.io Cost Management — https://fly.io/docs/about/cost-management/ *(primary)*
4. Fly.io Pricing — https://fly.io/pricing/ *(primary)*
5. Fly.io Free Trial — https://fly.io/docs/about/free-trial/ *(primary)*
6. Supabase Realtime concurrent-connections troubleshooting — https://supabase.com/docs/guides/troubleshooting/realtime-concurrent-peak-connections-quota-jdDqcp *(primary)*
7. PlayFab Pricing Overview — https://learn.microsoft.com/en-us/gaming/playfab/pricing/pricing-overview *(primary)*
8. Heroic Labs (Nakama) Pricing — https://heroiclabs.com/pricing/ *(primary)*
9. Firebase Pricing Plans — https://firebase.google.com/docs/projects/billing/firebase-pricing-plans *(primary)*
10. AWS Aurora Pricing — https://aws.amazon.com/rds/aurora/pricing/ *(primary)*; AWS egress — https://egresscost.com/aws/data-transfer-pricing/ *(secondary)*; Aurora Serverless v2 — https://www.usage.ai/blogs/aws/rds/aurora-serverless-v2/ *(secondary)*
11. PlayFab Roadmap — https://learn.microsoft.com/en-us/gaming/playfab/roadmap/ *(primary)*

*Secondary/indicative (VPS & Lightsail): getdeploying.com, cloudburn.io (Lightsail), vpscomparehub.com (Tokyo VPS), usage.ai. Cross-checked against primary vendor pages where possible; VPS dollar figures are directional, not adversarially confirmed.*

*Prices drift — Fly deprecated free plans Oct 2024 and has no billing alerts; PlayFab changed its free tier in 2026. Re-verify the two or three numbers you actually depend on before committing.*
