IMPLEMENTER_BLOCKED

gps_gifts_votes — the client half is complete and committed (b823510d5); the backend half is
written and committed (playlife 4206a56) but NOT applied and NOT deployed.

BLOCKER: backend/migrations/2026_09_02_gift_atomic.sql is DDL and needs Cesar in the Supabase
SQL editor. The Fly deploy is gated behind it in that order — gifts.py now calls
golfin_gift_pts / golfin_gift_purchase by name, so deploying first would take /gifts/send-pts
and /gifts/purchase down entirely.

Also needs a decision: the migration restores Cratilo total_points 6808 -> 7158 (+350 RP).
That is the invariant repair, but it is a real balance change.

After apply + deploy, the two blocked acceptance items close with one command:
  python3 Docs/Specs/Active/gps_gifts_votes/e2e_gift_economy.py \
      --env-file Tools/admin-dashboard/.env.development.local
