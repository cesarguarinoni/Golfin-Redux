IMPLEMENTER_BLOCKED

weekly_rotation_admin — iter-1 built, deployed (dashboard f8063af6b / CF 0ff72acc-d587-4baa-bfe6-dae2299453ef),
wk_2026_38 materialized and published on prod. ONE acceptance item cannot be closed by the implementer:
the pity migration `2026_09_11_gacha_pity_group.sql` is DDL (create or replace function) and goes
through the Supabase SQL editor — Cesar's step. Until it is applied, prod keys pity by banner id and the
§6.4 live E2E (pity continuing across two grouped banners) cannot pass.

Unblock: apply Part 1 of the migration, run Part 2 (self-rolling-back verification) and paste the
NOTICEs; I then create two short test rotations, run the pulls, quote golfin_gacha_pity, deactivate
them, flip the checklist row to PASS and set READY_FOR_SELF_REVIEW. See IMPLEMENTER_REPORT.md
§ Manual verification needed.
