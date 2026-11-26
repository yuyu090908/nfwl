import { route, ApiError } from "@/lib/server/api";

// Trading stays off until the chain, the smart contracts and the legal review are settled (prompt 11.4).
// This endpoint exists so the UI and tests have a single, explicit "disabled" answer.
export const POST = route({ name: "market.list", limit: 10 }, async () => {
  throw new ApiError(503, "MARKET_DISABLED", "使用チェーンとスマートコントラクト仕様が確定するまで売買は無効です");
});
