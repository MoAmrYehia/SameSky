-- SameSky on Supabase / Postgres: defense in depth.
--
-- Access model: browsers never query the database, Storage or Realtime directly. Every read and write goes
-- through the SameSky API, which authenticates the actor from the verified token and enforces grants,
-- membership generations, consent and versions (api/samesky/services/access.py). The worker applies the
-- same checks. The API and worker connect with a dedicated role (samesky_api) that owns these tables.
--
-- This script therefore enables Row Level Security on every application table and grants the client
-- roles (anon, authenticated) NOTHING. If a publishable key leaks or someone points a Supabase client at
-- the project, RLS denies every row. Run after `alembic upgrade head`, as a superuser.

do $$
declare t text;
begin
  foreach t in array array[
    'users','avatars','rooms','memberships','invitations','audience_snapshots','recipient_grants',
    'media_assets','moments','recaps','recap_sources','recap_approvals','replies','reactions','room_objects',
    'viewing_sessions','achievement_events','awards','availability_rules','availability_exceptions',
    'free_now','call_opportunities','notifications','push_subscriptions','jobs'
  ] loop
    execute format('alter table public.%I enable row level security', t);
    execute format('alter table public.%I force row level security', t);
    execute format('revoke all on table public.%I from anon, authenticated', t);
    -- No policies for anon/authenticated: with RLS enabled and no policy, every row is invisible to them.
    execute format('drop policy if exists samesky_api_all on public.%I', t);
    execute format('create policy samesky_api_all on public.%I for all to samesky_api using (true) with check (true)', t);
  end loop;
end $$;

-- Media: SameSky stores files on the API's private volume (SAMESKY_MEDIA_ROOT) and serves them only through
-- short-lived signed URLs that re-check access on every request. If you move media to Supabase Storage,
-- create a PRIVATE bucket with no storage.objects policies for anon/authenticated, and keep serving through
-- the API (service role on the server only):
--   insert into storage.buckets (id, name, public) values ('samesky-media', 'samesky-media', false)
--   on conflict (id) do update set public = false;

-- Realtime: SameSky uses its own authenticated WebSocket (/v1/realtime), not Supabase Realtime, so no
-- realtime.messages policies are created. Do not enable Realtime publication on these tables.
