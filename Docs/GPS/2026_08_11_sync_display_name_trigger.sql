-- 2026-08-11 — Fix: Create Username (PUT /auth/v1/user → user_metadata.display_name) never reached
-- public.profiles.display_name, because handle_new_user only copies metadata at signup.
-- The PLAYLIFE backend reads profiles.display_name (rankings/social/gifts), so post-signup
-- username choices were invisible server-side. Verified 2026-08-11: cesar test user had
-- meta_display_name='Cratilo' but profiles.display_name=NULL.
--
-- Adds an AFTER UPDATE trigger on auth.users syncing metadata display_name → profiles,
-- plus a one-time backfill. Additive; no existing behavior changes; Flutter app unaffected.
-- TODO: copy into the backend repo's migration folder (playlife-main/backend/migrations/).

create or replace function public.sync_display_name_from_metadata()
returns trigger language plpgsql security definer as $$
begin
  update public.profiles
     set display_name = new.raw_user_meta_data->>'display_name'
   where id = new.id
     and coalesce(new.raw_user_meta_data->>'display_name','') <> '';
  return new;
end $$;

drop trigger if exists on_auth_user_metadata_updated on auth.users;

create trigger on_auth_user_metadata_updated
  after update of raw_user_meta_data on auth.users
  for each row execute function public.sync_display_name_from_metadata();

-- one-time backfill for rows created before this trigger existed
update public.profiles p
   set display_name = u.raw_user_meta_data->>'display_name'
  from auth.users u
 where u.id = p.id
   and p.display_name is null
   and coalesce(u.raw_user_meta_data->>'display_name','') <> '';
