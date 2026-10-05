-- All fixtures and assertions run in one transaction and are rolled back.
begin;
do $$
declare
    v_prefix text := 'qa' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 7);
    v_clear text := encode(gen_random_bytes(32), 'hex');
    v_fail text := encode(gen_random_bytes(32), 'hex');
    v_active text := encode(gen_random_bytes(32), 'hex');
    v_row record;
    v_time bigint;
    v_completed timestamptz;
    v_count bigint;
begin
    select * into v_row from public.thirteenth_bell_reserve_nickname(v_prefix || 'C', v_clear);
    assert v_row.result = 'reserved', 'success reservation';
    select * into v_row from public.thirteenth_bell_reserve_nickname(v_prefix || 'C', v_active);
    assert v_row.result = 'nickname_taken', 'nickname collision';
    select * into v_row from public.thirteenth_bell_reserve_nickname(v_prefix || 'F', v_fail);
    assert v_row.result = 'reserved', 'failure reservation';
    select * into v_row from public.thirteenth_bell_reserve_nickname(v_prefix || 'A', v_active);
    assert v_row.result = 'reserved', 'new run uses new claim';
    select * into v_row from public.thirteenth_bell_submit_clear(v_clear, 'deliver_gift');
    assert v_row.result = 'saved' and v_row.stored_clear_time_ms > 0, 'successful clear saved';
    v_time := v_row.stored_clear_time_ms;
    select * into v_row from public.thirteenth_bell_submit_clear(v_clear, 'feed_clock');
    assert v_row.result = 'saved' and v_row.stored_clear_time_ms = v_time, 'clear retry idempotent';
    select * into v_row from public.thirteenth_bell_submit_failure(v_clear);
    assert v_row.result = 'already_completed', 'late failure cannot overwrite clear';
    select * into v_row from public.thirteenth_bell_submit_failure(v_fail);
    select count(*) into v_count from public.thirteenth_bell_leaderboard where completed_at is not null;
    assert v_row.result = 'saved' and v_row.leaderboard_rank = v_count, 'failure shares last rank';
    select completed_at into v_completed from public.thirteenth_bell_leaderboard where claim_token_hash = v_fail;
    select * into v_row from public.thirteenth_bell_submit_failure(v_fail);
    assert v_row.result = 'saved', 'failure retry accepted';
    assert (select completed_at = v_completed and failed and clear_time_ms is null and ending is null
        from public.thirteenth_bell_leaderboard where claim_token_hash = v_fail), 'failure unchanged on retry';
    select * into v_row from public.thirteenth_bell_submit_clear(v_fail, 'deliver_gift');
    assert v_row.result = 'run_failed', 'failed run cannot clear later';
    assert (select completed_at is null and not failed from public.thirteenth_bell_leaderboard
        where claim_token_hash = v_active), 'fresh run does not erase past failure';
    assert not has_column_privilege('anon', 'public.thirteenth_bell_leaderboard', 'claim_token_hash', 'SELECT'), 'claim tokens private';
    assert not has_table_privilege('anon', 'public.thirteenth_bell_leaderboard', 'INSERT'), 'anonymous inserts forbidden';
    assert not has_function_privilege('anon', 'public.thirteenth_bell_submit_failure(text)', 'EXECUTE'), 'failure RPC private';
end;
$$;
rollback;
select 'PASS: 15 database assertions, fixtures rolled back' as result;
