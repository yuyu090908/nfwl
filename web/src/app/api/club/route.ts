import { z } from "zod";
import { route } from "@/lib/server/api";
import { createClubWithStarters } from "@/lib/server/club";
import { LOGO_SHAPES, LOGO_SYMBOLS } from "@/lib/domain/emblem";

const hex = z.string().regex(/^#[0-9a-fA-F]{6}$/);
const body = z.object({
  name: z.string().trim().min(3).max(24).regex(/^[\p{L}\p{N} .'-]+$/u, "使える文字: 文字・数字・空白・.'-"),
  shortName: z.string().trim().toUpperCase().regex(/^[A-Z0-9]{2,4}$/, "英大文字・数字2〜4文字"),
  logo: z.object({ shape: z.enum(LOGO_SHAPES), symbol: z.enum(LOGO_SYMBOLS) }),
  colors: z.tuple([hex, hex, hex]),
});

export const POST = route({ name: "club.create", body, limit: 5 }, async ({ user, ip }, b) => {
  const { club, players } = await createClubWithStarters(user.id, b, ip);
  return { clubId: club.id, players: players.length, next: "/onboarding?step=players" };
});
