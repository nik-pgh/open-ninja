// Vercel Serverless Function: GET top-10 scores, POST a new score.
// Storage: Upstash Redis sorted set "leaderboard" with member=nickname,
// score=best score (we ZADD with GT so only strict improvements survive).
//
// Why a sorted set keyed by nickname (instead of unique-per-run entries):
//   - Avoids one player flooding the board with N attempts.
//   - Naturally collapses to "personal best per nickname".

const KV_URL   = process.env.KV_REST_API_URL;
const KV_TOKEN = process.env.KV_REST_API_TOKEN;
const KEY      = "leaderboard";

async function kv(cmd) {
  const res = await fetch(KV_URL, {
    method: "POST",
    headers: {
      Authorization: "Bearer " + KV_TOKEN,
      "Content-Type": "application/json",
    },
    body: JSON.stringify(cmd),
  });
  if (!res.ok) throw new Error("KV " + res.status + ": " + (await res.text()));
  return (await res.json()).result;
}

// Allowlist: letters, numbers, space, underscore, hyphen, dot.
const NICK_OK = /^[\p{L}\p{N} _\-\.]+$/u;

function sanitizeNickname(raw) {
  if (typeof raw !== "string") return "";
  const trimmed = raw.replace(/\s+/g, " ").trim().slice(0, 16);
  if (!trimmed || !NICK_OK.test(trimmed)) return "";
  return trimmed;
}

export default async function handler(req, res) {
  res.setHeader("Access-Control-Allow-Origin", "*");
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type");
  if (req.method === "OPTIONS") return res.status(204).end();

  try {
    if (req.method === "GET") {
      const flat = await kv(["ZRANGE", KEY, "0", "9", "REV", "WITHSCORES"]);
      const out = [];
      for (let i = 0; i < flat.length; i += 2) {
        out.push({ nickname: flat[i], score: Number(flat[i + 1]) });
      }
      res.setHeader("Cache-Control", "public, s-maxage=5, stale-while-revalidate=15");
      return res.status(200).json({ top: out });
    }

    if (req.method === "POST") {
      let body = req.body;
      if (typeof body === "string") {
        try { body = JSON.parse(body); } catch { body = {}; }
      }
      const nickname = sanitizeNickname(body && body.nickname);
      const score    = Number(body && body.score);

      if (!nickname) return res.status(400).json({ error: "invalid nickname" });
      if (!Number.isFinite(score) || score < 0 || score > 999999) {
        return res.status(400).json({ error: "score out of range" });
      }

      await kv(["ZADD", KEY, "GT", String(Math.floor(score)), nickname]);
      return res.status(200).json({ ok: true });
    }

    return res.status(405).json({ error: "method not allowed" });
  } catch (e) {
    return res.status(500).json({ error: String(e && e.message || e) });
  }
}
