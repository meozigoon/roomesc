-- Only immutable record IDs become public; claim hashes and admin state remain private.
grant select (id) on public.thirteenth_bell_leaderboard to anon, authenticated;

create schema if not exists bell_private;
revoke all on schema bell_private from public, anon, authenticated;
grant usage on schema bell_private to service_role;

create table bell_private.leaderboard_admin (
    singleton boolean primary key default true check (singleton),
    password_hash text not null check (length(password_hash) = 60),
    failed_attempts integer not null default 0 check (failed_attempts >= 0),
    blocked_until timestamptz
);
alter table bell_private.leaderboard_admin enable row level security;
revoke all on table bell_private.leaderboard_admin from public, anon, authenticated;
grant select, update on table bell_private.leaderboard_admin to service_role;

-- Initialize the password separately through the server's SQL Editor.
-- No password or password-derived verifier is committed to the public repository.

create function public.thirteenth_bell_delete_records(p_record_ids uuid[], p_password text)
returns table(result text, deleted_count integer)
language plpgsql security invoker set search_path = ''
as $$
declare
    v_admin bell_private.leaderboard_admin%rowtype;
    v_deleted integer;
begin
    if p_record_ids is null or cardinality(p_record_ids) not between 1 and 1000
        or array_position(p_record_ids, null) is not null
        or '00000000-0000-0000-0000-000000000000'::uuid = any(p_record_ids)
        or p_password is null or octet_length(p_password) not between 1 and 72 then
        return query select 'invalid_deletion'::text, 0;
        return;
    end if;
    -- Serializes attempts across all Edge Function instances, including concurrent failures.
    select * into v_admin from bell_private.leaderboard_admin where singleton for update;
    if not found then
        return query select 'service_unavailable'::text, 0;
        return;
    end if;
    if v_admin.blocked_until > clock_timestamp() then
        return query select 'rate_limited'::text, 0;
        return;
    end if;
    if v_admin.blocked_until is not null then
        v_admin.failed_attempts := 0;
    end if;
    if extensions.crypt(p_password, v_admin.password_hash) <> v_admin.password_hash then
        update bell_private.leaderboard_admin set
            failed_attempts = v_admin.failed_attempts + 1,
            blocked_until = case when v_admin.failed_attempts + 1 >= 10
                then clock_timestamp() + interval '1 minute' else null end
        where singleton;
        return query select 'invalid_password'::text, 0;
        return;
    end if;
    update bell_private.leaderboard_admin set failed_attempts = 0, blocked_until = null where singleton;
    -- UUIDs prevent a stale selection from deleting a new run that reused the nickname.
    -- Completed success AND failure records can be deleted; ongoing reservations are protected.
    delete from public.thirteenth_bell_leaderboard
    where id = any(p_record_ids) and completed_at is not null;
    get diagnostics v_deleted = row_count;
    return query select 'deleted'::text, v_deleted;
end;
$$;
revoke all on function public.thirteenth_bell_delete_records(uuid[], text) from public, anon, authenticated;
grant execute on function public.thirteenth_bell_delete_records(uuid[], text) to service_role;
