-- Production-safe assertions: all test records are rolled back.
begin;
do $$
declare
    v_prefix text := 'qa' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 7);
    v_a text := encode(gen_random_bytes(32), 'hex');
    v_b text := encode(gen_random_bytes(32), 'hex');
    v_c text := encode(gen_random_bytes(32), 'hex');
    v_d text := encode(gen_random_bytes(32), 'hex');
    v_row record;
    v_rank_a bigint;
    v_rank_b bigint;
    v_rank_c bigint;
begin
    perform public.thirteenth_bell_reserve_nickname(v_prefix || 'A', v_a);
    perform public.thirteenth_bell_reserve_nickname(v_prefix || 'B', v_b);
    perform public.thirteenth_bell_reserve_nickname(v_prefix || 'C', v_c);
    perform public.thirteenth_bell_reserve_nickname(v_prefix || 'D', v_d);
    update public.thirteenth_bell_leaderboard set run_started_at = clock_timestamp() - interval '10 minutes'
    where claim_token_hash in (v_a, v_b, v_c, v_d);
    select * into v_row from public.thirteenth_bell_submit_clear(v_a, 'deliver_gift', 1000::bigint, 3, 2);
    assert v_row.result = 'saved' and v_row.stored_clear_time_ms = 1000, 'active timer stored exactly';
    assert (select failed_attempts = 3 and hint_count = 2 and ranking_penalty = 5
        from public.thirteenth_bell_leaderboard where claim_token_hash = v_a), 'statistics and generated sum stored';
    select * into v_row from public.thirteenth_bell_submit_clear(v_b, 'deliver_gift', 1000::bigint, 1, 1);
    assert v_row.result = 'saved', 'equal-time lower-penalty clear saved';
    v_rank_b := v_row.leaderboard_rank;
    select * into v_row from public.thirteenth_bell_submit_clear(v_a, 'feed_clock', 1::bigint, 0, 0);
    v_rank_a := v_row.leaderboard_rank;
    assert v_row.stored_clear_time_ms = 1000 and v_rank_a = v_rank_b + 1, 'lower sum wins equal-time ranking';
    assert (select failed_attempts = 3 and hint_count = 2 and ending = 'deliver_gift'
        from public.thirteenth_bell_leaderboard where claim_token_hash = v_a), 'completed statistics and ending immutable';
    select * into v_row from public.thirteenth_bell_submit_failure(v_a);
    assert v_row.result = 'already_completed' and v_row.leaderboard_rank = v_rank_a, 'late failure returns penalty-aware rank';
    select * into v_row from public.thirteenth_bell_submit_clear(v_c, 'deliver_gift', 999::bigint, 10, 2147483647);
    v_rank_c := v_row.leaderboard_rank;
    select * into v_row from public.thirteenth_bell_submit_clear(v_b, 'deliver_gift', 1000::bigint, 1, 1);
    assert v_row.result = 'saved' and v_rank_c < v_row.leaderboard_rank, 'time takes priority over penalty';
    assert (select ranking_penalty = 2147483657::bigint from public.thirteenth_bell_leaderboard
        where claim_token_hash = v_c), 'sum avoids integer overflow';
    select * into v_row from public.thirteenth_bell_submit_clear(v_d, 'deliver_gift', 0::bigint, 0, 0);
    assert v_row.result = 'invalid_run_statistics', 'zero time rejected';
    select * into v_row from public.thirteenth_bell_submit_clear(v_d, 'deliver_gift', 999999999::bigint, 0, 0);
    assert v_row.result = 'invalid_run_statistics', 'time beyond reservation rejected';
    select * into v_row from public.thirteenth_bell_submit_clear(v_d, 'deliver_gift', 1000::bigint, 11, 0);
    assert v_row.result = 'invalid_run_statistics', 'failed run count cannot submit clear';
    select * into v_row from public.thirteenth_bell_submit_clear(v_d, 'deliver_gift', 1000::bigint, 0, -1);
    assert v_row.result = 'invalid_run_statistics', 'negative hints rejected';
    assert (select completed_at is null from public.thirteenth_bell_leaderboard
        where claim_token_hash = v_d), 'invalid requests do not complete run';
    select * into v_row from public.thirteenth_bell_submit_clear(v_d, 'deliver_gift');
    assert v_row.result = 'saved' and v_row.stored_clear_time_ms >= 600000, 'legacy RPC retains wall timer';
    assert has_column_privilege('anon', 'public.thirteenth_bell_leaderboard', 'ranking_penalty', 'SELECT'), 'public can order by penalty';
    assert not has_column_privilege('anon', 'public.thirteenth_bell_leaderboard', 'claim_token_hash', 'SELECT'), 'claim remains private';
    assert not has_function_privilege('anon', 'public.thirteenth_bell_submit_clear(text,text,bigint,integer,integer)', 'EXECUTE'), 'statistics RPC remains private';
end;
$$;
rollback;
select 'PASS: 17 database assertions, fixtures rolled back' as result;
