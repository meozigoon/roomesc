// Local request and RPC response checks. No production network access is used.
let handler: (request: Request) => Promise<Response>;
let rpcData: unknown = [{ result: "reserved", reserved_nickname: "Tester" }];
let calls = 0;
let checks = 0;
Deno.env.set("SUPABASE_URL", "https://local.invalid");
Deno.env.set("SUPABASE_SERVICE_ROLE_KEY", "local-test-only");
Object.defineProperty(Deno, "serve", {
  value: (callback: typeof handler) => { handler = callback; },
});
globalThis.fetch = () => {
  calls++;
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
for (const body of ["null", "[]", "true", "123", '"text"', "", "x".repeat(4097)]) {
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
console.log(`LEADERBOARD_SMOKE_OK checks=${checks}, productionRequests=0`);
