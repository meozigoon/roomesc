alter table public.thirteenth_bell_leaderboard
    add column failed_attempts integer not null default 0 check (failed_attempts between 0 and 10),
    add column hint_count integer not null default 0 check (hint_count >= 0),
    add column ranking_penalty bigint generated always as (failed_attempts::bigint + hint_count::bigint) stored;

grant select (failed_attempts, hint_count, ranking_penalty)
    on public.thirteenth_bell_leaderboard to anon, authenticated;

drop index public.thirteenth_bell_outcome_ranking_idx;
create index thirteenth_bell_outcome_ranking_idx
    on public.thirteenth_bell_leaderboard (failed, clear_time_ms, ranking_penalty, completed_at, nickname)
    where completed_at is not null;

create or replace function public.thirteenth_bell_submit_clear(
    p_claim_token_hash text, p_ending text, p_elapsed_ms bigint,
    p_failed_attempts integer, p_hint_count integer)
returns table(result text, stored_clear_time_ms bigint, leaderboard_rank bigint)
language plpgsql security invoker set search_path = ''
as $$
declare
    v_player public.thirteenth_bell_leaderboard%rowtype;
    v_now timestamptz;
    v_wall_ms bigint;
    v_rank bigint;
begin
    if coalesce(p_claim_token_hash, '') !~ '^[0-9a-f]{64}$' then
        return query select 'invalid_token'::text, null::bigint, null::bigint;
        return;
    end if;
    if p_ending is null or p_ending not in ('deliver_gift', 'feed_clock') then
        return query select 'invalid_ending'::text, null::bigint, null::bigint;
        return;
    end if;
    if p_failed_attempts is null or p_failed_attempts not between 0 and 10
        or p_hint_count is null or p_hint_count < 0
        or (p_elapsed_ms is not null and p_elapsed_ms < 1) then
        return query select 'invalid_run_statistics'::text, null::bigint, null::bigint;
        return;
    end if;
    select * into v_player from public.thirteenth_bell_leaderboard
    where claim_token_hash = p_claim_token_hash for update;
    if not found then
        return query select 'reservation_not_found'::text, null::bigint, null::bigint;
        return;
    end if;
    if v_player.failed then
        return query select 'run_failed'::text, null::bigint, null::bigint;
        return;
    end if;
    if v_player.completed_at is null then
        v_now := clock_timestamp();
        v_wall_ms := greatest(1, floor(extract(epoch from (v_now - v_player.run_started_at)) * 1000)::bigint);
        -- Active gameplay excludes the opening letter and time spent outside a restored session.
        -- Allow clock rounding/transit tolerance, but never accept a longer-than-reservation run.
        if p_elapsed_ms is not null and p_elapsed_ms > v_wall_ms + 5000 then
            return query select 'invalid_run_statistics'::text, null::bigint, null::bigint;
            return;
        end if;
        update public.thirteenth_bell_leaderboard set
            clear_time_ms = coalesce(p_elapsed_ms, v_wall_ms),
            failed_attempts = p_failed_attempts, hint_count = p_hint_count,
            ending = p_ending, completed_at = v_now, updated_at = v_now
        where id = v_player.id returning * into v_player;
    end if;
    select 1 + count(*) into v_rank from public.thirteenth_bell_leaderboard
    where not failed and completed_at is not null
      and (clear_time_ms, ranking_penalty, completed_at, nickname)
          < (v_player.clear_time_ms, v_player.ranking_penalty, v_player.completed_at, v_player.nickname);
    return query select 'saved'::text, v_player.clear_time_ms, v_rank;
end;
$$;

-- Keep previous clients compatible; their historical timer remains the reservation wall time.
create or replace function public.thirteenth_bell_submit_clear(p_claim_token_hash text, p_ending text)
returns table(result text, stored_clear_time_ms bigint, leaderboard_rank bigint)
language sql security invoker set search_path = ''
as $$
    select * from public.thirteenth_bell_submit_clear(p_claim_token_hash, p_ending, null::bigint, 0, 0);
$$;

create or replace function public.thirteenth_bell_submit_failure(p_claim_token_hash text)
returns table(result text, leaderboard_rank bigint)
language plpgsql security invoker set search_path = ''
as $$
declare
    v_player public.thirteenth_bell_leaderboard%rowtype;
    v_rank bigint;
begin
    if coalesce(p_claim_token_hash, '') !~ '^[0-9a-f]{64}$' then
        return query select 'invalid_token'::text, null::bigint;
        return;
    end if;
    select * into v_player from public.thirteenth_bell_leaderboard
    where claim_token_hash = p_claim_token_hash for update;
    if not found then
        return query select 'reservation_not_found'::text, null::bigint;
        return;
    end if;
    if v_player.completed_at is not null and not v_player.failed then
        select 1 + count(*) into v_rank from public.thirteenth_bell_leaderboard
        where not failed and completed_at is not null
          and (clear_time_ms, ranking_penalty, completed_at, nickname)
              < (v_player.clear_time_ms, v_player.ranking_penalty, v_player.completed_at, v_player.nickname);
        return query select 'already_completed'::text, v_rank;
        return;
    end if;
    if not v_player.failed then
        update public.thirteenth_bell_leaderboard set failed = true,
            completed_at = clock_timestamp(), updated_at = clock_timestamp()
        where id = v_player.id;
    end if;
    select count(*) into v_rank from public.thirteenth_bell_leaderboard where completed_at is not null;
    return query select 'saved'::text, v_rank;
end;
$$;

revoke all on function public.thirteenth_bell_submit_clear(text, text, bigint, integer, integer) from public, anon, authenticated;
revoke all on function public.thirteenth_bell_submit_clear(text, text) from public, anon, authenticated;
revoke all on function public.thirteenth_bell_submit_failure(text) from public, anon, authenticated;
grant execute on function public.thirteenth_bell_submit_clear(text, text, bigint, integer, integer) to service_role;
grant execute on function public.thirteenth_bell_submit_clear(text, text) to service_role;
grant execute on function public.thirteenth_bell_submit_failure(text) to service_role;
