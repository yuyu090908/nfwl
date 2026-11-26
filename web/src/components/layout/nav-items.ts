export const PRIMARY_NAV = [
  { href: "/play", label: "Play" },
  { href: "/club", label: "Club" },
  { href: "/squad", label: "Squad" },
  { href: "/players", label: "Players" },
  { href: "/league", label: "League" },
  { href: "/rankings", label: "Rankings" },
  { href: "/marketplace", label: "Market" },
] as const;

export const MORE_NAV = [
  { href: "/upgrade", label: "Upgrade（育成）" },
  { href: "/matches", label: "Matches（試合履歴）" },
  { href: "/league/divisions", label: "Divisions" },
  { href: "/league/champions", label: "Champions League" },
  { href: "/auctions", label: "Auctions" },
  { href: "/predictions", label: "Free Pick（試合予想）" },
  { href: "/rewards", label: "Rewards" },
  { href: "/wallet", label: "Wallet" },
  { href: "/profile", label: "Profile" },
  { href: "/settings", label: "Settings" },
  { href: "/help", label: "Help" },
] as const;

export const MOBILE_TABS = [
  { href: "/", label: "Home", icon: "home" },
  { href: "/club", label: "Club", icon: "shield" },
  { href: "/play", label: "Play", icon: "play" },
  { href: "/league", label: "League", icon: "trophy" },
  { href: "/marketplace", label: "Market", icon: "store" },
] as const;
