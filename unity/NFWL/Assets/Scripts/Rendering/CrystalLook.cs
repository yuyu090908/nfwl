namespace NFWL
{
    /// <summary>How a rarity looks (PHASE 7.3). Glow stays moderate so outlines and the ball read first.</summary>
    public struct CrystalLook
    {
        public float opacity, glow, fresnel, pulseSpeed, pulseAmount, sparkle;
        public int shoulderShards, armShards, legShards, hairShards;
        public float saturation;

        public static CrystalLook For(Rarity r)
        {
            switch (r)
            {
                case Rarity.Common: return new CrystalLook { opacity = 0.7f, glow = 0.35f, fresnel = 0.6f, saturation = 0.55f, shoulderShards = 2, armShards = 1, legShards = 1, hairShards = 1 };
                case Rarity.Rare: return new CrystalLook { opacity = 0.72f, glow = 0.55f, fresnel = 0.9f, saturation = 1f, shoulderShards = 3, armShards = 2, legShards = 2, hairShards = 1 };
                case Rarity.Epic: return new CrystalLook { opacity = 0.75f, glow = 0.7f, fresnel = 1.1f, saturation = 1f, shoulderShards = 4, armShards = 3, legShards = 3, hairShards = 2 };
                case Rarity.Mythic: return new CrystalLook { opacity = 0.78f, glow = 0.8f, fresnel = 1.3f, pulseSpeed = 2.2f, pulseAmount = 0.35f, saturation = 1f, shoulderShards = 4, armShards = 3, legShards = 3, hairShards = 3 };
                default: return new CrystalLook { opacity = 0.8f, glow = 0.95f, fresnel = 1.5f, pulseSpeed = 3f, pulseAmount = 0.3f, sparkle = 0.6f, saturation = 1f, shoulderShards = 5, armShards = 4, legShards = 3, hairShards = 4 };
            }
        }
    }
}
