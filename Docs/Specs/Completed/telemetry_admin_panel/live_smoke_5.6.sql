-- =============================================================================
-- telemetry_admin_panel — SPEC §5.6 live smoke
--
-- WHAT THIS IS FOR. The panel computes every number in TypeScript
-- (Tools/admin-dashboard/lib/telemetryData.ts) from rows it fetched. These
-- queries make Postgres compute the same numbers its own way. If the two agree,
-- the aggregation is right; if they disagree, the panel is lying and this is the
-- only test that would ever have told you.
--
-- Mock fixtures cannot catch this class of bug: a wrong date boundary or a
-- miscounted `distinct session_id` would be equally wrong in the fixture and in
-- the panel, and they would agree with each other all the way to the wrong
-- answer.
--
-- WHEN. Once the beta testers have generated real rows. Against an empty table
-- every block returns zero and proves nothing.
--
-- HOW.
--   1. Open the panel, note the range it shows (top right, `YYYY-MM-DD → YYYY-MM-DD`).
--   2. Find-and-replace the two timestamps below. Five blocks, but SIX runnable
--      statements (block 4 is two), so twelve literals in total — replace all of
--      them or a block will silently report on the wrong week.
--   3. Run a block, compare against the section named in its header.
--
-- NOT YET RUN. There is no Postgres client on the dev machine (ADMIN_DASHBOARD_OPS
-- §3.2: no DDL path over REST, no connection string), so this file has been
-- read carefully but never executed. If a block errors, that is a typo here and
-- not a finding about the data — fix it and say so.
--
-- `received_at`, never `ts`. `received_at` is the server clock and is what the
-- panel filters on; `ts` is the tester's device clock, and using it here would
-- manufacture a mismatch that is not a real one.
-- =============================================================================


-- -----------------------------------------------------------------------------
-- BLOCK 1 — §3.1 KPI cards.  THIS IS THE ONE §5.6 ACTUALLY ASKS FOR.
-- Compare with: Active testers · Sessions · Rounds started · Holes completed ·
--               Abandons · Crashes.
-- `abandon_rate` is what turns the Abandons card amber above 0.20.
-- -----------------------------------------------------------------------------
with params as (
  select '2026-08-24T00:00:00Z'::timestamptz as range_from,
         '2026-08-31T23:59:59Z'::timestamptz as range_to
),
ev as (
  -- today_start rides along as a COLUMN rather than a subquery in the FILTER
  -- clauses below: a subquery there is not reliably legal, and this reads
  -- better anyway.
  select e.*,
         date_trunc('day', p.range_to at time zone 'UTC') at time zone 'UTC' as today_start
  from public.telemetry_events e, params p
  where e.received_at >= p.range_from and e.received_at <= p.range_to
)
select
  count(distinct user_id)                                              as active_testers,
  count(distinct session_id)                                           as sessions,
  count(*) filter (where name = 'round_start')                         as rounds_started,
  count(*) filter (where name = 'hole_complete')                       as holes_completed,
  count(*) filter (where name = 'round_abandoned')                     as abandons,
  count(*) filter (where name = 'client_error')                        as crashes,
  round(
    count(*) filter (where name = 'round_abandoned')::numeric
    / nullif(count(*) filter (where name = 'round_start'), 0), 4)      as abandon_rate,
  -- The "N today" line under the first two cards. The panel's "today" is the UTC
  -- day the RANGE ENDS on, not the wall clock — so this matches even when you
  -- run it days later.
  count(distinct user_id)    filter (where received_at >= today_start)  as active_testers_today,
  count(distinct session_id) filter (where received_at >= today_start)  as sessions_today,
  count(*)                                                             as rows_scanned
from ev;


-- -----------------------------------------------------------------------------
-- BLOCK 2 — §3.2 session funnel.
-- Compare with the five bars (the count in parentheses, not the percentage).
--
-- A session counts at a stage if it reached that stage OR ANY LATER ONE — hence
-- max(depth) rather than five independent counts. That is deliberate, and it is
-- why the bars can never read as increasing when a screen_view is lost in a
-- dropped batch. `>= 0` therefore equals sessions_total unless a session
-- somehow produced no mapped event at all.
-- -----------------------------------------------------------------------------
with params as (
  select '2026-08-24T00:00:00Z'::timestamptz as range_from,
         '2026-08-31T23:59:59Z'::timestamptz as range_to
),
ev as (
  select e.* from public.telemetry_events e, params p
  where e.received_at >= p.range_from and e.received_at <= p.range_to
),
depth as (
  select session_id, max(
    case
      when name = 'session_start'                                              then 0
      when name = 'screen_view' and payload->>'screen' = 'Home'                then 1
      when name = 'screen_view' and payload->>'screen'
             in ('HoleSelection', 'TournamentHoleSelection')                   then 2
      when name = 'round_start'                                                then 3
      when name = 'hole_complete'                                              then 4
      else -1
    end) as md
  from ev group by session_id
)
select
  count(*)                             as sessions_total,
  count(*) filter (where md >= 0)      as stage1_app_opened,
  count(*) filter (where md >= 1)      as stage2_reached_home,
  count(*) filter (where md >= 2)      as stage3_reached_hole_selection,
  count(*) filter (where md >= 3)      as stage4_started_a_round,
  count(*) filter (where md >= 4)      as stage5_completed_a_hole
from depth;


-- -----------------------------------------------------------------------------
-- BLOCK 3 — §3.3 per-hole difficulty.
-- Compare row for row. `ob_rate` red-tints above 0.25, `fps_low_median` below 20.
-- percentile_cont is a true median (it averages the two middle values on an even
-- count), which is exactly what the panel's median() does.
-- -----------------------------------------------------------------------------
with params as (
  select '2026-08-24T00:00:00Z'::timestamptz as range_from,
         '2026-08-31T23:59:59Z'::timestamptz as range_to
),
ev as (
  select e.* from public.telemetry_events e, params p
  where e.received_at >= p.range_from and e.received_at <= p.range_to
),
h as (select *, (payload->>'hole')::int as hole from ev
       where payload->>'hole' is not null)
select
  hole,
  count(*) filter (where name = 'round_start')                     as plays,
  count(*) filter (where name = 'hole_complete')                   as completed,
  count(*) filter (where name = 'round_abandoned')                 as abandoned,
  round(avg((payload->>'strokes')::numeric)
        filter (where name = 'hole_complete'), 1)                  as avg_strokes,
  round(avg((payload->>'penalty_strokes')::numeric)
        filter (where name = 'hole_complete'), 1)                  as avg_penalty,
  round(
    count(*) filter (where name = 'shot_taken'
                       and upper(payload->>'terminal') = 'OB')::numeric
    / nullif(count(*) filter (where name = 'shot_taken'), 0), 4)   as ob_rate,
  round(avg((payload->>'duration_s')::numeric)
        filter (where name = 'hole_complete'), 0)                  as avg_duration_s,
  percentile_cont(0.5) within group (
    order by (payload->>'fps_low')::numeric)
    filter (where name = 'hole_complete')                          as fps_low_median
from h group by hole order by hole;


-- -----------------------------------------------------------------------------
-- BLOCK 4 — §3.4 shot quality.
-- Compare with the four cards and the club table.
-- flick_reject_rate is the headline number of the whole beta.
-- -----------------------------------------------------------------------------
with params as (
  select '2026-08-24T00:00:00Z'::timestamptz as range_from,
         '2026-08-31T23:59:59Z'::timestamptz as range_to
),
ev as (
  select e.* from public.telemetry_events e, params p
  where e.received_at >= p.range_from and e.received_at <= p.range_to
)
select
  count(*) filter (where name = 'shot_taken')                          as shots_taken,
  count(*) filter (where name = 'flick_rejected')                      as flick_rejected,
  count(*) filter (where name = 'shot_cancelled')                      as shot_cancelled,
  round(count(*) filter (where name = 'flick_rejected')::numeric
        / nullif(count(*) filter (where name in ('flick_rejected','shot_taken')), 0), 4)
                                                                       as flick_reject_rate,
  round(count(*) filter (where name = 'shot_cancelled')::numeric
        / nullif(count(*) filter (where name in ('shot_cancelled','shot_taken')), 0), 4)
                                                                       as cancel_rate,
  round(count(*) filter (where name = 'shot_taken'
                           and upper(payload->>'terminal') = 'OB')::numeric
        / nullif(count(*) filter (where name = 'shot_taken'), 0), 4)   as ob_rate
from ev;

-- …and the club breakdown underneath those cards:
with params as (
  select '2026-08-24T00:00:00Z'::timestamptz as range_from,
         '2026-08-31T23:59:59Z'::timestamptz as range_to
)
select
  payload->>'club'                                    as club,
  count(*)                                            as shots,
  round(avg((payload->>'distance_m')::numeric), 1)    as avg_distance_m
from public.telemetry_events e, params p
where e.received_at >= p.range_from and e.received_at <= p.range_to
  and e.name = 'shot_taken'
group by 1 order by shots desc, club;


-- -----------------------------------------------------------------------------
-- BLOCK 5 — §3.5 testers table.
--
-- `play_time_s` is the subtle one, and the most likely place for an honest
-- disagreement: it sums max(duration_s) PER SESSION, not every session_end row.
-- session_end fires on every OnApplicationPause(true) and duration_s is
-- realtimeSinceStartup, so summing them all counts the same minutes repeatedly.
-- `unclean_exits` = sessions that produced no session_end at all.
-- -----------------------------------------------------------------------------
with params as (
  select '2026-08-24T00:00:00Z'::timestamptz as range_from,
         '2026-08-31T23:59:59Z'::timestamptz as range_to
),
ev as (
  select e.* from public.telemetry_events e, params p
  where e.received_at >= p.range_from and e.received_at <= p.range_to
),
per_session as (
  select user_id, session_id,
         max((payload->>'duration_s')::numeric)
           filter (where name = 'session_end') as session_seconds
  from ev group by user_id, session_id
),
sess as (
  select user_id,
         count(*)                                        as sessions,
         count(*) filter (where session_seconds is null) as unclean_exits,
         round(coalesce(sum(session_seconds), 0))        as play_time_s
  from per_session group by user_id
),
acts as (
  select user_id,
         count(*) filter (where name = 'round_start')    as rounds,
         count(*) filter (where name = 'hole_complete')  as holes_completed,
         count(*) filter (where name = 'client_error')   as crashes,
         max(received_at)                                as last_seen
  from ev group by user_id
)
-- The panel shows an email here; this shows the raw uuid. To name them, join
-- auth.users on id — or just match the row order, which is `last seen` in both.
select s.user_id, s.sessions, s.unclean_exits, s.play_time_s,
       a.rounds, a.holes_completed, a.crashes, a.last_seen
from sess s join acts a using (user_id)
order by a.last_seen desc;
