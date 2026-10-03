create table public.thirteenth_bell_leaderboard (
    id uuid primary key default gen_random_uuid(),
    nickname text not null,
    nickname_key text generated always as (
        lower(btrim(regexp_replace(nickname, E'\\s+', ' ', 'g')))
    ) stored,
    claim_token_hash text not null,
    run_started_at timestamptz not null default clock_timestamp(),
    clear_time_ms bigint,
    ending text,
    completed_at timestamptz,
    created_at timestamptz not null default clock_timestamp(),
    updated_at timestamptz not null default clock_timestamp(),
    constraint thirteenth_bell_nickname_length check (char_length(btrim(nickname)) between 2 and 16),
    constraint thirteenth_bell_nickname_no_controls check (nickname !~ '[[:cntrl:]]'),
    constraint thirteenth_bell_claim_hash_format check (claim_token_hash ~ '^[0-9a-f]{64}$'),
    constraint thirteenth_bell_clear_time_positive check (clear_time_ms is null or clear_time_ms > 0),
    constraint thirteenth_bell_ending_values check (ending is null or ending in ('deliver_gift', 'feed_clock')),
    constraint thirteenth_bell_nickname_unique unique (nickname_key),
    constraint thirteenth_bell_claim_unique unique (claim_token_hash)
);

comment on table public.thirteenth_bell_leaderboard is
    'Best clear time per locally-held claim token for the Thirteenth Bell WinForms game.';

create index thirteenth_bell_ranking_idx
    on public.thirteenth_bell_leaderboard (clear_time_ms, completed_at)
    where clear_time_ms is not null;

alter table public.thirteenth_bell_leaderboard enable row level security;

revoke all on table public.thirteenth_bell_leaderboard from public, anon, authenticated;
grant select (nickname, clear_time_ms, completed_at)
    on public.thirteenth_bell_leaderboard to anon, authenticated;
grant all on table public.thirteenth_bell_leaderboard to service_role;

create policy "Completed leaderboard scores are public"
    on public.thirteenth_bell_leaderboard
    for select
    to anon, authenticated
    using (clear_time_ms is not null);

create or replace function public.thirteenth_bell_reserve_nickname(
    p_nickname text,
    p_claim_token_hash text
)
returns table(result text, reserved_nickname text)
language plpgsql
security invoker
set search_path = ''
as $$
declare
    v_nickname text;
    v_reserved text;
begin
    v_nickname := btrim(regexp_replace(coalesce(p_nickname, ''), E'\s+', ' ', 'g'));

    if char_length(v_nickname) not between 2 and 16
       or v_nickname ~ '[[:cntrl:]]' then
        return query select 'invalid_nickname'::text, null::text;
        return;
    end if;

    if coalesce(p_claim_token_hash, '') !~ '^[0-9a-f]{64}$' then
        return query select 'invalid_token'::text, null::text;
        return;
    end if;

    begin
        update public.thirteenth_bell_leaderboard
        set nickname = v_nickname,
            run_started_at = clock_timestamp(),
            clear_time_ms = case when nickname_key = lower(v_nickname) then clear_time_ms else null end,
            ending = case when nickname_key = lower(v_nickname) then ending else null end,
            completed_at = case when nickname_key = lower(v_nickname) then completed_at else null end,
            updated_at = clock_timestamp()
        where claim_token_hash = p_claim_token_hash
        returning nickname into v_reserved;

        if found then
            return query select 'reserved'::text, v_reserved;
            return;
        end if;

        insert into public.thirteenth_bell_leaderboard (nickname, claim_token_hash)
        values (v_nickname, p_claim_token_hash)
        returning nickname into v_reserved;

        return query select 'reserved'::text, v_reserved;
    exception
        when unique_violation then
            return query select 'nickname_taken'::text, null::text;
    end;
end;
$$;

create or replace function public.thirteenth_bell_submit_clear(
    p_claim_token_hash text,
    p_ending text
)
returns table(result text, stored_clear_time_ms bigint, leaderboard_rank bigint)
language plpgsql
security invoker
set search_path = ''
as $$
declare
    v_player public.thirteenth_bell_leaderboard%rowtype;
    v_now timestamptz;
    v_elapsed_ms bigint;
    v_best_ms bigint;
    v_rank bigint;
begin
    if coalesce(p_claim_token_hash, '') !~ '^[0-9a-f]{64}$' then
        return query select 'invalid_token'::text, null::bigint, null::bigint;
        return;
    end if;

    if p_ending not in ('deliver_gift', 'feed_clock') then
        return query select 'invalid_ending'::text, null::bigint, null::bigint;
        return;
    end if;

    select * into v_player
    from public.thirteenth_bell_leaderboard
    where claim_token_hash = p_claim_token_hash
    for update;

    if not found then
        return query select 'reservation_not_found'::text, null::bigint, null::bigint;
        return;
    end if;

    v_now := clock_timestamp();
    v_elapsed_ms := greatest(
        1,
        floor(extract(epoch from (v_now - v_player.run_started_at)) * 1000)::bigint
    );

    if v_player.clear_time_ms is null or v_elapsed_ms < v_player.clear_time_ms then
        update public.thirteenth_bell_leaderboard
        set clear_time_ms = v_elapsed_ms,
            ending = p_ending,
            completed_at = v_now,
            updated_at = v_now
        where id = v_player.id
        returning clear_time_ms into v_best_ms;
    else
        v_best_ms := v_player.clear_time_ms;
    end if;

    select 1 + count(*) into v_rank
    from public.thirteenth_bell_leaderboard
    where clear_time_ms is not null
      and clear_time_ms < v_best_ms;

    return query select 'saved'::text, v_best_ms, v_rank;
end;
$$;

revoke all on function public.thirteenth_bell_reserve_nickname(text, text)
    from public, anon, authenticated;
revoke all on function public.thirteenth_bell_submit_clear(text, text)
    from public, anon, authenticated;
grant execute on function public.thirteenth_bell_reserve_nickname(text, text) to service_role;
grant execute on function public.thirteenth_bell_submit_clear(text, text) to service_role;
