import { z } from "zod";
import { route } from "@/lib/server/api";
import { createPracticeSession } from "@/lib/server/match-session";

const body = z.object({
  halfSeconds: z.number().int().min(10).max(600),
  difficulty: z.enum(["beginner", "standard", "hard"]),
});

/** Starts an AI match. Returns the config the page hands to Unity (no secrets inside). */
export const POST = route({ name: "match.create", body, limit: 10 }, async ({ user }, b) => {
  return createPracticeSession(user.id, b.halfSeconds, b.difficulty);
});
