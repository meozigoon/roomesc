alter table public.thirteenth_bell_leaderboard
    add column failed boolean not null default false;

alter table public.thirteenth_bell_leaderboard
    add constraint thirteenth_bell_failed_result check (
        not failed or (clear_time_ms is null and ending is null and completed_at is not null)
    );

grant select (failed) on public.thirteenth_bell_leaderboard to anon, authenticated;
alter policy "Completed leaderboard scores are public"
    on public.thirteenth_bell_leaderboard using (completed_at is not null);

create index thirteenth_bell_outcome_ranking_idx
    on public.thirteenth_bell_leaderboard (failed, clear_time_ms, completed_at, nickname)
    where completed_at is not null;

comment on table public.thirteenth_bell_leaderboard is
    'One immutable terminal result per reserved nickname/run; failed results rank last.';

create or replace function public.thirteenth_bell_reserve_nickname(p_nickname text, p_claim_token_hash text)
returns table(result text, reserved_nickname text)
language plpgsql security invoker set search_path = ''
as $$
declare
    v_nickname text := btrim(regexp_replace(coalesce(p_nickname, ''), '[[:space:]]+', ' ', 'g'));
begin
    if char_length(v_nickname) not between 2 and 16 or v_nickname ~ '[[:cntrl:]]' then
        return query select 'invalid_nickname'::text, null::text;
        return;
    end if;
    if coalesce(p_claim_token_hash, '') !~ '^[0-9a-f]{64}$' then
        return query select 'invalid_token'::text, null::text;
        return;
    end if;
    begin
        -- New reservations never overwrite another run's success or failure.
        insert into public.thirteenth_bell_leaderboard (nickname, claim_token_hash)
        values (v_nickname, p_claim_token_hash);
        return query select 'reserved'::text, v_nickname;
    exception when unique_violation then
        return query select 'nickname_taken'::text, null::text;
    end;
end;
$$;

create or replace function public.thirteenth_bell_submit_clear(p_claim_token_hash text, p_ending text)
returns table(result text, stored_clear_time_ms bigint, leaderboard_rank bigint)
language plpgsql security invoker set search_path = ''
as $$
declare
    v_player public.thirteenth_bell_leaderboard%rowtype;
    v_now timestamptz;
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
        update public.thirteenth_bell_leaderboard set
            clear_time_ms = greatest(1, floor(extract(epoch from (v_now - v_player.run_started_at)) * 1000)::bigint),
            ending = p_ending, completed_at = v_now, updated_at = v_now
        where id = v_player.id returning * into v_player;
    end if;
    select 1 + count(*) into v_rank from public.thirteenth_bell_leaderboard
    where not failed and completed_at is not null
      and (clear_time_ms, completed_at, nickname) < (v_player.clear_time_ms, v_player.completed_at, v_player.nickname);
    return query select 'saved'::text, v_player.clear_time_ms, v_rank;
end;
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
          and (clear_time_ms, completed_at, nickname) < (v_player.clear_time_ms, v_player.completed_at, v_player.nickname);
        return query select 'already_completed'::text, v_rank;
        return;
    end if;
    if not v_player.failed then
        update public.thirteenth_bell_leaderboard set failed = true,
            completed_at = clock_timestamp(), updated_at = clock_timestamp()
        where id = v_player.id;
    end if;
    -- Every failed run shares the last rank, below every successful clear.
    select count(*) into v_rank from public.thirteenth_bell_leaderboard where completed_at is not null;
    return query select 'saved'::text, v_rank;
end;
$$;

revoke all on function public.thirteenth_bell_submit_failure(text) from public, anon, authenticated;
grant execute on function public.thirteenth_bell_submit_failure(text) to service_role;
