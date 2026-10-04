const publishableKey = "sb_publishable_KxNOykyFdjOSAJa0QDMoyQ_VCzvXrzO";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "apikey, content-type",
  "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
};

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders,
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
    },
  });
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

async function callRpc(
  projectUrl: string,
  serviceRoleKey: string,
  functionName: string,
  body: Record<string, unknown>,
): Promise<{ ok: boolean; status: number; data: unknown }> {
  const response = await fetch(`${projectUrl}/rest/v1/rpc/${functionName}`, {
    method: "POST",
    headers: {
      apikey: serviceRoleKey,
      Authorization: `Bearer ${serviceRoleKey}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(body),
  });

  const text = await response.text();
  let data: unknown = null;
  if (text.length > 0) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  return { ok: response.ok, status: response.status, data };
}

Deno.serve(async (request: Request) => {
  if (request.method === "OPTIONS") {
    return new Response(null, { status: 204, headers: corsHeaders });
  }

  const projectUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  if (!projectUrl || !serviceRoleKey) {
    console.error("Required Supabase environment variables are unavailable.");
    return jsonResponse(503, { error: "service_unavailable" });
  }

  if (request.headers.get("apikey") !== publishableKey) {
    return jsonResponse(401, { error: "invalid_client_key" });
  }

  try {
    if (request.method === "GET") {
      const response = await fetch(
        `${projectUrl}/rest/v1/thirteenth_bell_leaderboard?select=nickname,clear_time_ms&clear_time_ms=not.is.null&order=clear_time_ms.asc,completed_at.asc&limit=10`,
        { headers: { apikey: serviceRoleKey, Authorization: `Bearer ${serviceRoleKey}` } },
      );

      if (!response.ok) {
        console.error("Leaderboard query failed", response.status, await response.text());
        return jsonResponse(502, { error: "leaderboard_query_failed" });
      }

      return jsonResponse(200, { entries: await response.json() });
    }

    if (request.method !== "POST") {
      return jsonResponse(405, { error: "method_not_allowed" });
    }

    const rawBody = await request.text();
    if (rawBody.length === 0 || rawBody.length > 4096) {
      return jsonResponse(400, { error: "invalid_request_body" });
    }

    let payload: unknown;
    try {
      payload = JSON.parse(rawBody);
    } catch {
      return jsonResponse(400, { error: "invalid_json" });
    }

    if (!isObject(payload)) {
      return jsonResponse(400, { error: "invalid_request_body" });
    }

    if (payload.action === "reserve") {
      if (typeof payload.nickname !== "string" || typeof payload.claimTokenHash !== "string") {
        return jsonResponse(400, { error: "invalid_reservation" });
      }

      const rpc = await callRpc(projectUrl, serviceRoleKey, "thirteenth_bell_reserve_nickname", {
        p_nickname: payload.nickname,
        p_claim_token_hash: payload.claimTokenHash,
      });
      if (!rpc.ok || !Array.isArray(rpc.data) || rpc.data.length !== 1 || !isObject(rpc.data[0])) {
        console.error("Nickname reservation RPC failed", rpc.status, rpc.data);
        return jsonResponse(502, { error: "reservation_failed" });
      }

      const row = rpc.data[0];
      if (row.result === "nickname_taken") {
        return jsonResponse(409, { error: "nickname_taken" });
      }

      if (row.result !== "reserved") {
        return jsonResponse(400, { error: row.result ?? "invalid_reservation" });
      }

      return jsonResponse(200, { result: "reserved", nickname: row.reserved_nickname });
    }

    if (payload.action === "submit") {
      if (typeof payload.claimTokenHash !== "string" || typeof payload.ending !== "string") {
        return jsonResponse(400, { error: "invalid_submission" });
      }

      const rpc = await callRpc(projectUrl, serviceRoleKey, "thirteenth_bell_submit_clear", {
        p_claim_token_hash: payload.claimTokenHash,
        p_ending: payload.ending,
      });
      if (!rpc.ok || !Array.isArray(rpc.data) || rpc.data.length !== 1 || !isObject(rpc.data[0])) {
        console.error("Clear submission RPC failed", rpc.status, rpc.data);
        return jsonResponse(502, { error: "submission_failed" });
      }

      const row = rpc.data[0];
      if (row.result !== "saved") {
        return jsonResponse(400, { error: row.result ?? "invalid_submission" });
      }

      return jsonResponse(200, {
        result: "saved",
        clearTimeMs: row.stored_clear_time_ms,
        rank: row.leaderboard_rank,
      });
    }

    return jsonResponse(400, { error: "unknown_action" });
  } catch (error) {
    console.error("Unhandled leaderboard function error", error);
    return jsonResponse(500, { error: "internal_error" });
  }
});
