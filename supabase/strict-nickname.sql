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
    v_nickname := btrim(regexp_replace(coalesce(p_nickname, ''), E'\\s+', ' ', 'g'));
    if char_length(v_nickname) not between 2 and 16 or v_nickname ~ '[[:cntrl:]]' then
        return query select 'invalid_nickname'::text, null::text;
        return;
    end if;
    if coalesce(p_claim_token_hash, '') !~ '^[0-9a-f]{64}$' then
        return query select 'invalid_token'::text, null::text;
        return;
    end if;
    begin
        -- Serialize requests for the same existing identity before checking the name.
        perform 1 from public.thirteenth_bell_leaderboard
        where claim_token_hash = p_claim_token_hash for update;
        if exists (
            select 1 from public.thirteenth_bell_leaderboard
            where nickname_key = lower(v_nickname)
        ) then
            return query select 'nickname_taken'::text, null::text;
            return;
        end if;
        update public.thirteenth_bell_leaderboard
        set nickname = v_nickname, run_started_at = clock_timestamp(),
            clear_time_ms = null, ending = null, completed_at = null,
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
    exception when unique_violation then
        return query select 'nickname_taken'::text, null::text;
    end;
end;
$$;

