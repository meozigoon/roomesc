// Local request and RPC response checks. No production network access is used.
let handler: (request: Request) => Promise<Response> = () => Promise.reject(new Error("Server handler not installed"));
let rpcData: unknown = [{ result: "reserved", reserved_nickname: "Tester" }];
let calls = 0;
let checks = 0;
let lastRpcBody: Record<string, unknown> = {};
let lastUrl = "";
function rpcBody(): Record<string, unknown> { return lastRpcBody; }
Deno.env.set("SUPABASE_URL", "https://local.invalid");
Deno.env.set("SUPABASE_SERVICE_ROLE_KEY", "local-test-only");
Object.defineProperty(Deno, "serve", {
  value: (callback: typeof handler) => { handler = callback; },
});
globalThis.fetch = (_input, init) => {
  calls++;
  lastUrl = String(_input);
  if (typeof init?.body === "string") { lastRpcBody = JSON.parse(init.body); }
  return Promise.resolve(Response.json(rpcData));
};
await import("../supabase/functions/thirteenth-bell-leaderboard/index.ts");
const config = JSON.parse(await Deno.readTextFile(new URL("../src/ThirteenthBell/Assets/online-config.json", import.meta.url)));
async function check(body: string, status: number, error?: string) {
  const response = await handler(new Request("https://local.invalid", {
    method: "POST",
    headers: { apikey: config.publishableKey, "content-type": "application/json" },
    body,
  }));
  const payload = await response.json();
  if (response.status !== status || (error && payload.error !== error)) {
    throw new Error(`Unexpected result for ${body}: ${response.status} ${JSON.stringify(payload)}`);
  }
  checks++;
}
for (const body of ["null", "[]", "true", "123", '"text"', "", "x".repeat(65537)]) {
  await check(body, 400, "invalid_request_body");
}
await check("{", 400, "invalid_json");
await check("{}", 400, "unknown_action");
await check('{"action":"reserve"}', 400, "invalid_reservation");
await check('{"action":"submit"}', 400, "invalid_submission");
if (calls !== 0) { throw new Error("Invalid requests reached the database"); }
checks++;
const reserve = JSON.stringify({ action: "reserve", nickname: "Tester", claimTokenHash: "a".repeat(64) });
const submit = JSON.stringify({ action: "submit", ending: "deliver_gift", claimTokenHash: "a".repeat(64) });
for (const value of [null, [], [null], [1], [[]]]) {
  rpcData = value;
  await check(reserve, 502, "reservation_failed");
  await check(submit, 502, "submission_failed");
}
rpcData = [{ result: "nickname_taken" }];
await check(reserve, 409, "nickname_taken");
rpcData = [{ result: "reserved", reserved_nickname: "Tester" }];
await check(reserve, 200);
rpcData = [{ result: "saved", stored_clear_time_ms: 1000, leaderboard_rank: 1 }];
await check(submit, 200);
if (rpcBody().p_elapsed_ms !== null || rpcBody().p_failed_attempts !== 0 || rpcBody().p_hint_count !== 0) {
  throw new Error("Previous clients must retain compatible statistics defaults");
}
checks++;
await check(JSON.stringify({ action: "submit", ending: "deliver_gift", claimTokenHash: "a".repeat(64), elapsedMilliseconds: 123456, failedAttempts: 3, hintCount: 2 }), 200);
if (rpcBody().p_elapsed_ms !== 123456 || rpcBody().p_failed_attempts !== 3 || rpcBody().p_hint_count !== 2) {
  throw new Error("Run statistics were not forwarded to the ranking RPC");
}
checks++;
for (const fields of [
  { failedAttempts: -1 }, { failedAttempts: 11 }, { failedAttempts: 1.5 }, { failedAttempts: "2" },
  { hintCount: -1 }, { hintCount: 2147483648 }, { hintCount: 1.5 }, { hintCount: "2" },
  { elapsedMilliseconds: 0 }, { elapsedMilliseconds: -1 }, { elapsedMilliseconds: 1.5 }, { elapsedMilliseconds: "1" },
]) {
  const before: number = calls;
  await check(JSON.stringify({ action: "submit", ending: "deliver_gift", claimTokenHash: "a".repeat(64), ...fields }), 400, "invalid_run_statistics");
  if (calls !== before) { throw new Error("Invalid statistics reached database"); }
  checks++;
}
const failure = JSON.stringify({ action: "fail", claimTokenHash: "a".repeat(64) });
await check('{"action":"fail"}', 400, "invalid_failure_submission");
await check('{"action":"fail","claimTokenHash":"x"}', 400, "invalid_failure_submission");
for (const value of [null, [], [null], [1], [[]]]) {
  rpcData = value;
  await check(failure, 502, "failure_submission_failed");
}
rpcData = [{ result: "reservation_not_found" }];
await check(failure, 400, "reservation_not_found");
rpcData = [{ result: "saved", leaderboard_rank: 0 }];
await check(failure, 502, "invalid_failure_response");
rpcData = [{ result: "saved", leaderboard_rank: 9 }];
await check(failure, 200);
rpcData = [{ result: "already_completed", leaderboard_rank: 2 }];
await check(failure, 200);
rpcData = [{ nickname: "Clear", clear_time_ms: 1000, failed: false }, { nickname: "Failed", clear_time_ms: null, failed: true }];
const rankingResponse = await handler(new Request("https://local.invalid", { headers: { apikey: config.publishableKey } }));
const ranking = await rankingResponse.json();
if (rankingResponse.status !== 200 || ranking.entries.length !== 2 || ranking.totalCount !== 2 || !ranking.entries[1].failed) {
  throw new Error("Failed runs missing from ranking response");
}
checks++;
if (!decodeURIComponent(lastUrl).includes("clear_time_ms.asc.nullslast,ranking_penalty.asc,completed_at.asc,nickname.asc")) {
  throw new Error("Ranking query is missing time/penalty tie break");
}
checks++;
const recordId = "12345678-1234-4567-89ab-123456789abc";
const deletion = JSON.stringify({ action: "delete", recordIds: [recordId, recordId.toUpperCase()], password: "local-test-password" });
for (const fields of [
  {}, { recordIds: [] }, { recordIds: [null] }, { recordIds: ["not-a-uuid"] },
  { recordIds: Array(1001).fill(recordId) }, { recordIds: ["00000000-0000-0000-0000-000000000000"] },
  { recordIds: [recordId], password: "" }, { recordIds: [recordId], password: "가".repeat(25) },
]) {
  const before: number = calls;
  await check(JSON.stringify({ action: "delete", password: "local-test-password", ...fields }), 400, "invalid_deletion");
  if (calls !== before) { throw new Error("Invalid deletion reached database"); }
  checks++;
}
for (const value of [null, [], [null], [1], [{}], [{ result: "deleted", deleted_count: "1" }], [{ result: "deleted", deleted_count: -1 }], [{ result: "deleted", deleted_count: 3 }]]) {
  rpcData = value;
  await check(deletion, 502);
}
rpcData = [{ result: "invalid_password", deleted_count: 0 }];
await check(deletion, 403, "invalid_password");
rpcData = [{ result: "rate_limited", deleted_count: 0 }];
await check(deletion, 429, "rate_limited");
rpcData = [{ result: "deleted", deleted_count: 1 }];
await check(deletion, 200);
if (!lastUrl.endsWith("/rpc/thirteenth_bell_delete_records")
  || JSON.stringify(rpcBody().p_record_ids) !== JSON.stringify([recordId])
  || rpcBody().p_password !== "local-test-password") {
  throw new Error("Deletion did not forward unique IDs and password to protected RPC");
}
checks++;
rpcData = [{ result: "deleted", deleted_count: 0 }];
await check(deletion, 200);
console.log(`LEADERBOARD_SMOKE_OK checks=${checks}, productionRequests=0`);
