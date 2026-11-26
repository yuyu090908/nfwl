-- CreateEnum
CREATE TYPE "Position" AS ENUM ('GK', 'DF', 'MF', 'FW');

-- CreateEnum
CREATE TYPE "Rarity" AS ENUM ('Common', 'Rare', 'Epic', 'Mythic', 'Legendary');

-- CreateEnum
CREATE TYPE "CrystalType" AS ENUM ('ICE', 'SAPPHIRE', 'AMETHYST', 'EMERALD', 'GOLD', 'VIOLET');

-- CreateEnum
CREATE TYPE "PlayerOrigin" AS ENUM ('STARTER', 'REWARD', 'PRIMARY_SALE', 'DEMO');

-- CreateEnum
CREATE TYPE "AccountProvider" AS ENUM ('EMAIL', 'GOOGLE', 'DISCORD', 'WALLET_EVM');

-- CreateEnum
CREATE TYPE "MatchMode" AS ENUM ('PRACTICE_CPU', 'LEAGUE_SIM', 'FRIENDLY');

-- CreateEnum
CREATE TYPE "MatchStatus" AS ENUM ('SCHEDULED', 'IN_PROGRESS', 'FINISHED', 'VOID', 'REJECTED');

-- CreateEnum
CREATE TYPE "ResultVerification" AS ENUM ('PENDING', 'CLIENT_REPORTED', 'SERVER_SIMULATED', 'FLAGGED', 'REJECTED');

-- CreateEnum
CREATE TYPE "MatchSessionStatus" AS ENUM ('CREATED', 'LOADING', 'IN_PLAY', 'FINISHED', 'ABORTED', 'EXPIRED', 'REJECTED');

-- CreateEnum
CREATE TYPE "SeasonStatus" AS ENUM ('UPCOMING', 'ACTIVE', 'SETTLING', 'CLOSED');

-- CreateEnum
CREATE TYPE "Currency" AS ENUM ('NFWL_POINTS', 'PICK_SCORE');

-- CreateEnum
CREATE TYPE "LedgerAccountKind" AS ENUM ('USER', 'SYSTEM_ISSUANCE', 'SYSTEM_SINK', 'SYSTEM_LOCKED');

-- CreateEnum
CREATE TYPE "TxStatus" AS ENUM ('PENDING', 'CONFIRMED', 'FAILED', 'REVERSED');

-- CreateEnum
CREATE TYPE "ListingType" AS ENUM ('FIXED_PRICE', 'AUCTION');

-- CreateEnum
CREATE TYPE "ListingStatus" AS ENUM ('DRAFT', 'ACTIVE', 'SOLD', 'CANCELLED', 'EXPIRED');

-- CreateEnum
CREATE TYPE "PredictionPick" AS ENUM ('HOME', 'DRAW', 'AWAY');

-- CreateEnum
CREATE TYPE "PredictionStatus" AS ENUM ('OPEN', 'WON', 'LOST', 'VOID');

-- CreateEnum
CREATE TYPE "RewardKind" AS ENUM ('XP', 'CLUB_XP', 'COSMETIC', 'BADGE', 'DEVELOPMENT_ITEM', 'SEASON', 'POINTS');

-- CreateTable
CREATE TABLE "User" (
    "id" UUID NOT NULL,
    "email" TEXT,
    "displayName" TEXT NOT NULL,
    "birthYear" INTEGER,
    "role" TEXT NOT NULL DEFAULT 'user',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,
    "disabledAt" TIMESTAMP(3),
    "settings" JSONB NOT NULL DEFAULT '{}',

    CONSTRAINT "User_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Account" (
    "id" UUID NOT NULL,
    "userId" UUID NOT NULL,
    "provider" "AccountProvider" NOT NULL,
    "providerAccountId" TEXT NOT NULL,
    "passwordHash" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Account_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Session" (
    "id" UUID NOT NULL,
    "userId" UUID NOT NULL,
    "tokenHash" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "expiresAt" TIMESTAMP(3) NOT NULL,
    "lastSeenAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "userAgent" TEXT,
    "ip" TEXT,

    CONSTRAINT "Session_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "AuthNonce" (
    "id" UUID NOT NULL,
    "nonce" TEXT NOT NULL,
    "purpose" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "expiresAt" TIMESTAMP(3) NOT NULL,
    "usedAt" TIMESTAMP(3),

    CONSTRAINT "AuthNonce_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Club" (
    "id" UUID NOT NULL,
    "ownerId" UUID NOT NULL,
    "name" TEXT NOT NULL,
    "shortName" TEXT NOT NULL,
    "logo" JSONB NOT NULL,
    "colors" JSONB NOT NULL,
    "level" INTEGER NOT NULL DEFAULT 1,
    "clubXp" INTEGER NOT NULL DEFAULT 0,
    "rp" INTEGER NOT NULL DEFAULT 0,
    "mmr" INTEGER NOT NULL DEFAULT 1000,
    "divisionId" INTEGER NOT NULL,
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "onboardingStep" INTEGER NOT NULL DEFAULT 0,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Club_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PlayerTemplate" (
    "id" UUID NOT NULL,
    "code" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "position" "Position" NOT NULL,
    "crystalType" "CrystalType" NOT NULL,
    "rarity" "Rarity" NOT NULL DEFAULT 'Common',
    "baseLevel" INTEGER NOT NULL,
    "pool" TEXT NOT NULL,
    "active" BOOLEAN NOT NULL DEFAULT true,
    "appearance" JSONB NOT NULL DEFAULT '{}',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "PlayerTemplate_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Player" (
    "id" UUID NOT NULL,
    "templateId" UUID,
    "name" TEXT NOT NULL,
    "position" "Position" NOT NULL,
    "subPositions" "Position"[],
    "rarity" "Rarity" NOT NULL DEFAULT 'Common',
    "crystalType" "CrystalType" NOT NULL,
    "preferredFoot" TEXT NOT NULL DEFAULT 'Right',
    "weakFoot" INTEGER NOT NULL DEFAULT 3,
    "level" INTEGER NOT NULL DEFAULT 1,
    "levelCap" INTEGER NOT NULL DEFAULT 10,
    "xp" INTEGER NOT NULL DEFAULT 0,
    "growthPoints" INTEGER NOT NULL DEFAULT 0,
    "origin" "PlayerOrigin" NOT NULL,
    "appearance" JSONB NOT NULL DEFAULT '{}',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Player_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PlayerAttributes" (
    "playerId" UUID NOT NULL,
    "speed" INTEGER NOT NULL,
    "acceleration" INTEGER NOT NULL,
    "agility" INTEGER NOT NULL,
    "ballControl" INTEGER NOT NULL,
    "shortPass" INTEGER NOT NULL,
    "longPass" INTEGER NOT NULL,
    "shotPower" INTEGER NOT NULL,
    "finishing" INTEGER NOT NULL,
    "tackling" INTEGER NOT NULL,
    "goalkeeping" INTEGER NOT NULL,

    CONSTRAINT "PlayerAttributes_pkey" PRIMARY KEY ("playerId")
);

-- CreateTable
CREATE TABLE "Skill" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "description" TEXT NOT NULL,
    "minRarity" "Rarity" NOT NULL DEFAULT 'Epic',
    "positions" "Position"[],
    "unitySkillId" TEXT,
    "effect" JSONB NOT NULL,

    CONSTRAINT "Skill_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PlayerSkill" (
    "playerId" UUID NOT NULL,
    "skillId" TEXT NOT NULL,
    "slot" INTEGER NOT NULL,
    "acquiredAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "PlayerSkill_pkey" PRIMARY KEY ("playerId","skillId")
);

-- CreateTable
CREATE TABLE "ClubPlayer" (
    "id" UUID NOT NULL,
    "clubId" UUID NOT NULL,
    "playerId" UUID NOT NULL,
    "joinedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "leftAt" TIMESTAMP(3),
    "lockedUntil" TIMESTAMP(3),
    "lockReason" TEXT,
    "active" BOOLEAN NOT NULL DEFAULT true,

    CONSTRAINT "ClubPlayer_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Formation" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "slots" JSONB NOT NULL,

    CONSTRAINT "Formation_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Squad" (
    "id" UUID NOT NULL,
    "clubId" UUID NOT NULL,
    "name" TEXT NOT NULL DEFAULT 'Main',
    "formationId" TEXT NOT NULL,
    "starters" UUID[],
    "bench" UUID[],
    "isActive" BOOLEAN NOT NULL DEFAULT true,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "Squad_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Division" (
    "id" INTEGER NOT NULL,
    "name" TEXT NOT NULL,
    "minRp" INTEGER,
    "maxRp" INTEGER,

    CONSTRAINT "Division_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Season" (
    "id" SERIAL NOT NULL,
    "number" INTEGER NOT NULL,
    "startsAt" TIMESTAMP(3) NOT NULL,
    "endsAt" TIMESTAMP(3) NOT NULL,
    "status" "SeasonStatus" NOT NULL DEFAULT 'UPCOMING',
    "official" BOOLEAN NOT NULL DEFAULT false,
    "config" JSONB NOT NULL DEFAULT '{}',

    CONSTRAINT "Season_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LeagueStanding" (
    "id" UUID NOT NULL,
    "seasonId" INTEGER NOT NULL,
    "divisionId" INTEGER NOT NULL,
    "clubId" UUID NOT NULL,
    "played" INTEGER NOT NULL DEFAULT 0,
    "wins" INTEGER NOT NULL DEFAULT 0,
    "draws" INTEGER NOT NULL DEFAULT 0,
    "losses" INTEGER NOT NULL DEFAULT 0,
    "goalsFor" INTEGER NOT NULL DEFAULT 0,
    "goalsAgainst" INTEGER NOT NULL DEFAULT 0,
    "points" INTEGER NOT NULL DEFAULT 0,
    "provisional" INTEGER NOT NULL DEFAULT 0,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "LeagueStanding_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Match" (
    "id" UUID NOT NULL,
    "seasonId" INTEGER,
    "mode" "MatchMode" NOT NULL,
    "status" "MatchStatus" NOT NULL DEFAULT 'SCHEDULED',
    "verification" "ResultVerification" NOT NULL DEFAULT 'PENDING',
    "homeClubId" UUID,
    "awayClubId" UUID,
    "cpuOpponent" JSONB,
    "homeScore" INTEGER,
    "awayScore" INTEGER,
    "kickoffAt" TIMESTAMP(3),
    "finishedAt" TIMESTAMP(3),
    "appliedAt" TIMESTAMP(3),
    "seed" INTEGER NOT NULL,
    "anomalies" JSONB NOT NULL DEFAULT '[]',
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "Match_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "MatchSession" (
    "id" UUID NOT NULL,
    "matchId" UUID NOT NULL,
    "userId" UUID NOT NULL,
    "status" "MatchSessionStatus" NOT NULL DEFAULT 'CREATED',
    "clientConfig" JSONB NOT NULL,
    "halfSeconds" INTEGER NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "loadedAt" TIMESTAMP(3),
    "startedAt" TIMESTAMP(3),
    "finishedAt" TIMESTAMP(3),
    "expiresAt" TIMESTAMP(3) NOT NULL,
    "lastSeq" INTEGER NOT NULL DEFAULT 0,
    "pausedMs" INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT "MatchSession_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "MatchEvent" (
    "id" UUID NOT NULL,
    "matchId" UUID NOT NULL,
    "messageId" TEXT NOT NULL,
    "eventType" TEXT NOT NULL,
    "seq" INTEGER,
    "gameClock" DOUBLE PRECISION,
    "payload" JSONB NOT NULL,
    "receivedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "MatchEvent_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "MatchStatistic" (
    "id" UUID NOT NULL,
    "matchId" UUID NOT NULL,
    "playerId" UUID,
    "clubId" UUID,
    "side" TEXT NOT NULL,
    "slot" INTEGER,
    "goals" INTEGER NOT NULL DEFAULT 0,
    "assists" INTEGER NOT NULL DEFAULT 0,
    "saves" INTEGER NOT NULL DEFAULT 0,
    "minutes" INTEGER NOT NULL DEFAULT 0,
    "xpAwarded" INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT "MatchStatistic_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PlayerExperience" (
    "id" UUID NOT NULL,
    "playerId" UUID NOT NULL,
    "matchId" UUID,
    "source" TEXT NOT NULL,
    "amount" INTEGER NOT NULL,
    "levelFrom" INTEGER NOT NULL,
    "levelTo" INTEGER NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "PlayerExperience_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PlayerUpgrade" (
    "id" UUID NOT NULL,
    "playerId" UUID NOT NULL,
    "kind" TEXT NOT NULL,
    "idempotencyKey" TEXT NOT NULL,
    "before" JSONB NOT NULL,
    "after" JSONB NOT NULL,
    "tokenTxId" UUID,
    "status" "TxStatus" NOT NULL DEFAULT 'PENDING',
    "testMode" BOOLEAN NOT NULL DEFAULT true,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "PlayerUpgrade_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "RewardTransaction" (
    "id" UUID NOT NULL,
    "clubId" UUID NOT NULL,
    "kind" "RewardKind" NOT NULL,
    "amount" INTEGER NOT NULL DEFAULT 0,
    "itemCode" TEXT,
    "reason" TEXT NOT NULL,
    "sourceRef" TEXT,
    "idempotencyKey" TEXT NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "RewardTransaction_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "LedgerAccount" (
    "id" UUID NOT NULL,
    "kind" "LedgerAccountKind" NOT NULL,
    "currency" "Currency" NOT NULL,
    "userId" UUID,
    "balance" INTEGER NOT NULL DEFAULT 0,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "LedgerAccount_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "TokenTransaction" (
    "id" UUID NOT NULL,
    "idempotencyKey" TEXT NOT NULL,
    "currency" "Currency" NOT NULL,
    "kind" TEXT NOT NULL,
    "status" "TxStatus" NOT NULL DEFAULT 'PENDING',
    "onChain" BOOLEAN NOT NULL DEFAULT false,
    "chainId" INTEGER,
    "txHash" TEXT,
    "confirmations" INTEGER,
    "memo" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "confirmedAt" TIMESTAMP(3),

    CONSTRAINT "TokenTransaction_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "TokenLedgerEntry" (
    "id" UUID NOT NULL,
    "transactionId" UUID NOT NULL,
    "accountId" UUID NOT NULL,
    "amount" INTEGER NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "TokenLedgerEntry_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "NFTAsset" (
    "id" UUID NOT NULL,
    "playerId" UUID NOT NULL,
    "chainId" INTEGER NOT NULL,
    "contract" TEXT NOT NULL,
    "tokenId" TEXT NOT NULL,
    "metadataUri" TEXT,
    "ownerAddress" TEXT,
    "verifiedAt" TIMESTAMP(3),
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "NFTAsset_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "MarketplaceListing" (
    "id" UUID NOT NULL,
    "playerId" UUID NOT NULL,
    "sellerId" UUID NOT NULL,
    "type" "ListingType" NOT NULL,
    "status" "ListingStatus" NOT NULL DEFAULT 'DRAFT',
    "currency" TEXT NOT NULL DEFAULT 'UNSET',
    "price" DECIMAL(38,0),
    "startPrice" DECIMAL(38,0),
    "minIncrement" DECIMAL(38,0),
    "endsAt" TIMESTAMP(3),
    "signature" TEXT,
    "version" INTEGER NOT NULL DEFAULT 0,
    "isDemo" BOOLEAN NOT NULL DEFAULT false,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "soldAt" TIMESTAMP(3),

    CONSTRAINT "MarketplaceListing_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "AuctionBid" (
    "id" UUID NOT NULL,
    "listingId" UUID NOT NULL,
    "bidderId" UUID NOT NULL,
    "amount" DECIMAL(38,0) NOT NULL,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "AuctionBid_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "PredictionEntry" (
    "id" UUID NOT NULL,
    "userId" UUID NOT NULL,
    "matchId" UUID NOT NULL,
    "pick" "PredictionPick" NOT NULL,
    "status" "PredictionStatus" NOT NULL DEFAULT 'OPEN',
    "score" INTEGER NOT NULL DEFAULT 0,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "settledAt" TIMESTAMP(3),

    CONSTRAINT "PredictionEntry_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "Achievement" (
    "id" TEXT NOT NULL,
    "name" TEXT NOT NULL,
    "description" TEXT NOT NULL,
    "category" TEXT NOT NULL,
    "icon" TEXT NOT NULL,

    CONSTRAINT "Achievement_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "UserAchievement" (
    "userId" UUID NOT NULL,
    "achievementId" TEXT NOT NULL,
    "earnedAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "UserAchievement_pkey" PRIMARY KEY ("userId","achievementId")
);

-- CreateTable
CREATE TABLE "AuditLog" (
    "id" UUID NOT NULL,
    "userId" UUID,
    "action" TEXT NOT NULL,
    "target" TEXT,
    "severity" TEXT NOT NULL DEFAULT 'info',
    "data" JSONB NOT NULL DEFAULT '{}',
    "ip" TEXT,
    "createdAt" TIMESTAMP(3) NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT "AuditLog_pkey" PRIMARY KEY ("id")
);

-- CreateTable
CREATE TABLE "GameConfig" (
    "key" TEXT NOT NULL,
    "value" JSONB NOT NULL,
    "updatedAt" TIMESTAMP(3) NOT NULL,

    CONSTRAINT "GameConfig_pkey" PRIMARY KEY ("key")
);

-- CreateIndex
CREATE UNIQUE INDEX "User_email_key" ON "User"("email");

-- CreateIndex
CREATE INDEX "Account_userId_idx" ON "Account"("userId");

-- CreateIndex
CREATE UNIQUE INDEX "Account_provider_providerAccountId_key" ON "Account"("provider", "providerAccountId");

-- CreateIndex
CREATE UNIQUE INDEX "Session_tokenHash_key" ON "Session"("tokenHash");

-- CreateIndex
CREATE INDEX "Session_userId_idx" ON "Session"("userId");

-- CreateIndex
CREATE INDEX "Session_expiresAt_idx" ON "Session"("expiresAt");

-- CreateIndex
CREATE UNIQUE INDEX "AuthNonce_nonce_key" ON "AuthNonce"("nonce");

-- CreateIndex
CREATE UNIQUE INDEX "Club_ownerId_key" ON "Club"("ownerId");

-- CreateIndex
CREATE INDEX "Club_divisionId_rp_idx" ON "Club"("divisionId", "rp");

-- CreateIndex
CREATE INDEX "Club_name_idx" ON "Club"("name");

-- CreateIndex
CREATE UNIQUE INDEX "PlayerTemplate_code_key" ON "PlayerTemplate"("code");

-- CreateIndex
CREATE INDEX "PlayerTemplate_pool_position_active_idx" ON "PlayerTemplate"("pool", "position", "active");

-- CreateIndex
CREATE INDEX "Player_rarity_position_idx" ON "Player"("rarity", "position");

-- CreateIndex
CREATE UNIQUE INDEX "PlayerSkill_playerId_slot_key" ON "PlayerSkill"("playerId", "slot");

-- CreateIndex
CREATE INDEX "ClubPlayer_clubId_active_idx" ON "ClubPlayer"("clubId", "active");

-- CreateIndex
CREATE INDEX "ClubPlayer_playerId_active_idx" ON "ClubPlayer"("playerId", "active");

-- CreateIndex
CREATE INDEX "Squad_clubId_isActive_idx" ON "Squad"("clubId", "isActive");

-- CreateIndex
CREATE UNIQUE INDEX "Season_number_key" ON "Season"("number");

-- CreateIndex
CREATE INDEX "LeagueStanding_seasonId_divisionId_points_idx" ON "LeagueStanding"("seasonId", "divisionId", "points");

-- CreateIndex
CREATE UNIQUE INDEX "LeagueStanding_seasonId_clubId_key" ON "LeagueStanding"("seasonId", "clubId");

-- CreateIndex
CREATE INDEX "Match_homeClubId_createdAt_idx" ON "Match"("homeClubId", "createdAt");

-- CreateIndex
CREATE INDEX "Match_awayClubId_createdAt_idx" ON "Match"("awayClubId", "createdAt");

-- CreateIndex
CREATE INDEX "Match_status_kickoffAt_idx" ON "Match"("status", "kickoffAt");

-- CreateIndex
CREATE INDEX "Match_seasonId_status_idx" ON "Match"("seasonId", "status");

-- CreateIndex
CREATE UNIQUE INDEX "MatchSession_matchId_key" ON "MatchSession"("matchId");

-- CreateIndex
CREATE INDEX "MatchSession_userId_createdAt_idx" ON "MatchSession"("userId", "createdAt");

-- CreateIndex
CREATE INDEX "MatchEvent_matchId_receivedAt_idx" ON "MatchEvent"("matchId", "receivedAt");

-- CreateIndex
CREATE UNIQUE INDEX "MatchEvent_matchId_messageId_key" ON "MatchEvent"("matchId", "messageId");

-- CreateIndex
CREATE INDEX "MatchStatistic_playerId_idx" ON "MatchStatistic"("playerId");

-- CreateIndex
CREATE UNIQUE INDEX "MatchStatistic_matchId_side_slot_key" ON "MatchStatistic"("matchId", "side", "slot");

-- CreateIndex
CREATE INDEX "PlayerExperience_playerId_createdAt_idx" ON "PlayerExperience"("playerId", "createdAt");

-- CreateIndex
CREATE UNIQUE INDEX "PlayerExperience_playerId_matchId_source_key" ON "PlayerExperience"("playerId", "matchId", "source");

-- CreateIndex
CREATE UNIQUE INDEX "PlayerUpgrade_idempotencyKey_key" ON "PlayerUpgrade"("idempotencyKey");

-- CreateIndex
CREATE INDEX "PlayerUpgrade_playerId_createdAt_idx" ON "PlayerUpgrade"("playerId", "createdAt");

-- CreateIndex
CREATE UNIQUE INDEX "RewardTransaction_idempotencyKey_key" ON "RewardTransaction"("idempotencyKey");

-- CreateIndex
CREATE INDEX "RewardTransaction_clubId_createdAt_idx" ON "RewardTransaction"("clubId", "createdAt");

-- CreateIndex
CREATE UNIQUE INDEX "LedgerAccount_kind_currency_userId_key" ON "LedgerAccount"("kind", "currency", "userId");

-- CreateIndex
CREATE UNIQUE INDEX "TokenTransaction_idempotencyKey_key" ON "TokenTransaction"("idempotencyKey");

-- CreateIndex
CREATE INDEX "TokenTransaction_kind_createdAt_idx" ON "TokenTransaction"("kind", "createdAt");

-- CreateIndex
CREATE INDEX "TokenLedgerEntry_accountId_idx" ON "TokenLedgerEntry"("accountId");

-- CreateIndex
CREATE UNIQUE INDEX "NFTAsset_playerId_key" ON "NFTAsset"("playerId");

-- CreateIndex
CREATE UNIQUE INDEX "NFTAsset_chainId_contract_tokenId_key" ON "NFTAsset"("chainId", "contract", "tokenId");

-- CreateIndex
CREATE INDEX "MarketplaceListing_status_type_createdAt_idx" ON "MarketplaceListing"("status", "type", "createdAt");

-- CreateIndex
CREATE INDEX "MarketplaceListing_playerId_status_idx" ON "MarketplaceListing"("playerId", "status");

-- CreateIndex
CREATE INDEX "AuctionBid_listingId_amount_idx" ON "AuctionBid"("listingId", "amount");

-- CreateIndex
CREATE INDEX "PredictionEntry_matchId_status_idx" ON "PredictionEntry"("matchId", "status");

-- CreateIndex
CREATE UNIQUE INDEX "PredictionEntry_userId_matchId_key" ON "PredictionEntry"("userId", "matchId");

-- CreateIndex
CREATE INDEX "AuditLog_action_createdAt_idx" ON "AuditLog"("action", "createdAt");

-- CreateIndex
CREATE INDEX "AuditLog_userId_createdAt_idx" ON "AuditLog"("userId", "createdAt");

-- AddForeignKey
ALTER TABLE "Account" ADD CONSTRAINT "Account_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Session" ADD CONSTRAINT "Session_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Club" ADD CONSTRAINT "Club_ownerId_fkey" FOREIGN KEY ("ownerId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Club" ADD CONSTRAINT "Club_divisionId_fkey" FOREIGN KEY ("divisionId") REFERENCES "Division"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Player" ADD CONSTRAINT "Player_templateId_fkey" FOREIGN KEY ("templateId") REFERENCES "PlayerTemplate"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PlayerAttributes" ADD CONSTRAINT "PlayerAttributes_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PlayerSkill" ADD CONSTRAINT "PlayerSkill_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PlayerSkill" ADD CONSTRAINT "PlayerSkill_skillId_fkey" FOREIGN KEY ("skillId") REFERENCES "Skill"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ClubPlayer" ADD CONSTRAINT "ClubPlayer_clubId_fkey" FOREIGN KEY ("clubId") REFERENCES "Club"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "ClubPlayer" ADD CONSTRAINT "ClubPlayer_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Squad" ADD CONSTRAINT "Squad_clubId_fkey" FOREIGN KEY ("clubId") REFERENCES "Club"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Squad" ADD CONSTRAINT "Squad_formationId_fkey" FOREIGN KEY ("formationId") REFERENCES "Formation"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LeagueStanding" ADD CONSTRAINT "LeagueStanding_seasonId_fkey" FOREIGN KEY ("seasonId") REFERENCES "Season"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LeagueStanding" ADD CONSTRAINT "LeagueStanding_divisionId_fkey" FOREIGN KEY ("divisionId") REFERENCES "Division"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LeagueStanding" ADD CONSTRAINT "LeagueStanding_clubId_fkey" FOREIGN KEY ("clubId") REFERENCES "Club"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Match" ADD CONSTRAINT "Match_seasonId_fkey" FOREIGN KEY ("seasonId") REFERENCES "Season"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Match" ADD CONSTRAINT "Match_homeClubId_fkey" FOREIGN KEY ("homeClubId") REFERENCES "Club"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "Match" ADD CONSTRAINT "Match_awayClubId_fkey" FOREIGN KEY ("awayClubId") REFERENCES "Club"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MatchSession" ADD CONSTRAINT "MatchSession_matchId_fkey" FOREIGN KEY ("matchId") REFERENCES "Match"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MatchEvent" ADD CONSTRAINT "MatchEvent_matchId_fkey" FOREIGN KEY ("matchId") REFERENCES "Match"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MatchStatistic" ADD CONSTRAINT "MatchStatistic_matchId_fkey" FOREIGN KEY ("matchId") REFERENCES "Match"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MatchStatistic" ADD CONSTRAINT "MatchStatistic_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE SET NULL ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PlayerExperience" ADD CONSTRAINT "PlayerExperience_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PlayerUpgrade" ADD CONSTRAINT "PlayerUpgrade_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "RewardTransaction" ADD CONSTRAINT "RewardTransaction_clubId_fkey" FOREIGN KEY ("clubId") REFERENCES "Club"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "LedgerAccount" ADD CONSTRAINT "LedgerAccount_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "TokenLedgerEntry" ADD CONSTRAINT "TokenLedgerEntry_transactionId_fkey" FOREIGN KEY ("transactionId") REFERENCES "TokenTransaction"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "TokenLedgerEntry" ADD CONSTRAINT "TokenLedgerEntry_accountId_fkey" FOREIGN KEY ("accountId") REFERENCES "LedgerAccount"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "NFTAsset" ADD CONSTRAINT "NFTAsset_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MarketplaceListing" ADD CONSTRAINT "MarketplaceListing_playerId_fkey" FOREIGN KEY ("playerId") REFERENCES "Player"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "MarketplaceListing" ADD CONSTRAINT "MarketplaceListing_sellerId_fkey" FOREIGN KEY ("sellerId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "AuctionBid" ADD CONSTRAINT "AuctionBid_listingId_fkey" FOREIGN KEY ("listingId") REFERENCES "MarketplaceListing"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "AuctionBid" ADD CONSTRAINT "AuctionBid_bidderId_fkey" FOREIGN KEY ("bidderId") REFERENCES "User"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PredictionEntry" ADD CONSTRAINT "PredictionEntry_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "PredictionEntry" ADD CONSTRAINT "PredictionEntry_matchId_fkey" FOREIGN KEY ("matchId") REFERENCES "Match"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "UserAchievement" ADD CONSTRAINT "UserAchievement_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE CASCADE ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "UserAchievement" ADD CONSTRAINT "UserAchievement_achievementId_fkey" FOREIGN KEY ("achievementId") REFERENCES "Achievement"("id") ON DELETE RESTRICT ON UPDATE CASCADE;

-- AddForeignKey
ALTER TABLE "AuditLog" ADD CONSTRAINT "AuditLog_userId_fkey" FOREIGN KEY ("userId") REFERENCES "User"("id") ON DELETE SET NULL ON UPDATE CASCADE;
