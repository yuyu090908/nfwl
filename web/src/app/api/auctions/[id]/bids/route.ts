import { route, ApiError } from "@/lib/server/api";

export const POST = route({ name: "auction.bid", limit: 10 }, async () => {
  throw new ApiError(503, "MARKET_DISABLED", "オークションの入札は本番売買の有効化まで無効です");
});
