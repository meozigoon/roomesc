-- All fixtures, deletions, password changes and throttling counters are rolled back.
begin;
do $$
declare
    v_prefix text := 'qd' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 7);
    v_a uuid := gen_random_uuid();
    v_b uuid := gen_random_uuid();
    v_pending uuid := gen_random_uuid();
    v_missing uuid := gen_random_uuid();
    v_reused uuid := gen_random_uuid();
    v_row record;
    v_count bigint;
begin
    select count(*) into v_count from public.thirteenth_bell_leaderboard;
    insert into bell_private.leaderboard_admin (singleton, password_hash)
    values (true, extensions.crypt('fixture-password', extensions.gen_salt('bf', 4)))
    on conflict (singleton) do update set
        password_hash = excluded.password_hash, failed_attempts = 0, blocked_until = null;
    insert into public.thirteenth_bell_leaderboard (id, nickname, claim_token_hash, clear_time_ms, ending, completed_at, failed)
    values (v_a, v_prefix || 'A', encode(extensions.gen_random_bytes(32), 'hex'), 1000, 'deliver_gift', clock_timestamp(), false),
           (v_b, v_prefix || 'B', encode(extensions.gen_random_bytes(32), 'hex'), null, null, clock_timestamp(), true),
           (v_pending, v_prefix || 'P', encode(extensions.gen_random_bytes(32), 'hex'), null, null, null, false);
    select * into v_row from public.thirteenth_bell_delete_records(array[v_a, v_b], 'wrong-password');
    assert v_row.result = 'invalid_password' and v_row.deleted_count = 0, 'wrong password denied';
    assert (select count(*) = 3 from public.thirteenth_bell_leaderboard where id = any(array[v_a, v_b, v_pending])), 'wrong password preserves records';
    select * into v_row from public.thirteenth_bell_delete_records(array[]::uuid[], 'fixture-password');
    assert v_row.result = 'invalid_deletion', 'empty selection denied';
    select * into v_row from public.thirteenth_bell_delete_records(array[null]::uuid[], 'fixture-password');
    assert v_row.result = 'invalid_deletion', 'null ID denied';
    select * into v_row from public.thirteenth_bell_delete_records(array[v_a], repeat('가', 25));
    assert v_row.result = 'invalid_deletion', 'bcrypt byte limit enforced';
    select * into v_row from public.thirteenth_bell_delete_records(array[v_pending, v_missing], 'fixture-password');
    assert v_row.result = 'deleted' and v_row.deleted_count = 0, 'missing and ongoing rows untouched';
    select * into v_row from public.thirteenth_bell_delete_records(array[v_a, v_a, v_b, v_missing], 'fixture-password');
    assert v_row.result = 'deleted' and v_row.deleted_count = 2, 'success and failure deleted atomically, duplicates count once';
    assert (select count(*) = 1 from public.thirteenth_bell_leaderboard where id = any(array[v_a, v_b, v_pending])), 'only completed selected rows deleted';
    assert (select count(*) = v_count + 1 from public.thirteenth_bell_leaderboard), 'existing records preserved';
    insert into public.thirteenth_bell_leaderboard (id, nickname, claim_token_hash, clear_time_ms, ending, completed_at, failed)
    values (v_reused, v_prefix || 'A', encode(extensions.gen_random_bytes(32), 'hex'), 2000, 'deliver_gift', clock_timestamp(), false);
    select * into v_row from public.thirteenth_bell_delete_records(array[v_a, v_b], 'fixture-password');
    assert v_row.result = 'deleted' and v_row.deleted_count = 0, 'retry is idempotent';
    assert exists(select 1 from public.thirteenth_bell_leaderboard where id = v_reused), 'stale selection preserves reused nickname';
    for i in 1..10 loop
        perform public.thirteenth_bell_delete_records(array[v_pending], 'wrong-password');
    end loop;
    select * into v_row from public.thirteenth_bell_delete_records(array[v_pending], 'fixture-password');
    assert v_row.result = 'rate_limited', 'shared throttle denies even correct password while blocked';
    update bell_private.leaderboard_admin set blocked_until = clock_timestamp() - interval '1 second' where singleton;
    select * into v_row from public.thirteenth_bell_delete_records(array[v_pending], 'fixture-password');
    assert v_row.result = 'deleted', 'expired throttle releases';
    assert (select failed_attempts = 0 and blocked_until is null from bell_private.leaderboard_admin), 'success resets throttle';
    assert not has_function_privilege('anon', 'public.thirteenth_bell_delete_records(uuid[],text)', 'EXECUTE'), 'anon cannot bypass Edge Function';
    assert not has_function_privilege('authenticated', 'public.thirteenth_bell_delete_records(uuid[],text)', 'EXECUTE'), 'authenticated cannot bypass Edge Function';
    assert has_function_privilege('service_role', 'public.thirteenth_bell_delete_records(uuid[],text)', 'EXECUTE'), 'server can call deletion';
    assert not has_schema_privilege('anon', 'bell_private', 'USAGE'), 'password schema private';
    assert not has_table_privilege('authenticated', 'bell_private.leaderboard_admin', 'SELECT'), 'password table private';
    assert not has_table_privilege('anon', 'public.thirteenth_bell_leaderboard', 'DELETE'), 'direct public deletion denied';
    assert has_column_privilege('anon', 'public.thirteenth_bell_leaderboard', 'id', 'SELECT'), 'public completed-record ID available';
    assert not has_column_privilege('anon', 'public.thirteenth_bell_leaderboard', 'claim_token_hash', 'SELECT'), 'claim remains private';
end;
$$;
rollback;
select 'PASS: 22 deletion assertions; all fixtures and admin state rolled back' as result;
