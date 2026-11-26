using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace NFWL
{
    // =====================================================================================
    // PHASE 11: player data from outside the game (NFT metadata), and skills.
    // The match still generates players exactly as in v0.5 when no data is supplied.
    // =====================================================================================

    /// <summary>One player's data as it would come from an NFT (or any other source).</summary>
    [Serializable]
    public class PlayerData
    {
        public string tokenId = "";
        public string name = "";
        public bool hasRarity;
        public Rarity rarity = Rarity.Common;
        public List<SkillId> skills = new List<SkillId>();
        /// <summary>Crystal element name from the web platform (ICE, SAPPHIRE, ...). Empty keeps the slot's element.</summary>
        public string element = "";
        /// <summary>Shirt number from the web platform. 0 keeps the slot's number.</summary>
        public int number;
        /// <summary>Ability values given by the source (lower-case key -> value). Others keep the generated value.</summary>
        public readonly Dictionary<string, float> traits = new Dictionary<string, float>();

        /// <summary>The generated attributes with this player's given values on top (clamped to 40..99).</summary>
        public PlayerAttributes BuildAttributes(PlayerAttributes generated)
        {
            PlayerAttributes a = new PlayerAttributes
            {
                Speed = generated.Speed, Acceleration = generated.Acceleration, Agility = generated.Agility,
                BallControl = generated.BallControl, ShortPass = generated.ShortPass, LongPass = generated.LongPass,
                ShotPower = generated.ShotPower, Finishing = generated.Finishing, Tackling = generated.Tackling,
                Goalkeeping = generated.Goalkeeping, PreferredFoot = generated.PreferredFoot, WeakFootRating = generated.WeakFootRating
            };
            foreach (KeyValuePair<string, float> t in traits)
            {
                int v = Mathf.Clamp(Mathf.RoundToInt(t.Value), 40, 99);
                switch (t.Key)
                {
                    case "speed": a.Speed = v; break;
                    case "acceleration": a.Acceleration = v; break;
                    case "agility": a.Agility = v; break;
                    case "ballcontrol": a.BallControl = v; break;
                    case "shortpass": a.ShortPass = v; break;
                    case "longpass": a.LongPass = v; break;
                    case "shotpower": a.ShotPower = v; break;
                    case "finishing": a.Finishing = v; break;
                    case "tackling": a.Tackling = v; break;
                    case "goalkeeping": a.Goalkeeping = v; break;
                    case "weakfoot": case "weakfootrating": a.WeakFootRating = Mathf.Clamp(Mathf.RoundToInt(t.Value), 1, 5); break;
                    case "preferredfoot": a.PreferredFoot = t.Value < 0f ? PreferredFoot.Left : PreferredFoot.Right; break;
                }
            }
            return a;
        }
    }

    /// <summary>Where a team's players come from. Return false to keep the random player for that slot.</summary>
    public interface IPlayerDataSource
    {
        bool TryGet(int team, int slotIndex, out PlayerData data);
    }

    /// <summary>
    /// Reads players from JSON. Two shapes are accepted per player:
    /// our own {"name","rarity","attributes":{"Speed":80,...},"skills":["CurlerPlus"]}
    /// or NFT-style metadata {"name", "attributes":[{"trait_type":"Speed","value":80}, {"trait_type":"Rarity","value":"Epic"}, {"trait_type":"Skill","value":"CatReflexes"}]}.
    /// The file lists {"home":[...5 players...], "away":[...]} in formation order (GK, DF, DF, MF, FW).
    /// Missing values keep the generated ones; abilities are clamped to 40..99.
    /// </summary>
    public class JsonPlayerSource : IPlayerDataSource
    {
        readonly List<PlayerData>[] teams = { new List<PlayerData>(), new List<PlayerData>() };

        public int Count(int team) => teams[team].Count;

        /// <summary>Resources/NFWLPlayers.json (a TextAsset), if the project has one.</summary>
        public static JsonPlayerSource FromResources()
        {
            TextAsset ta = Resources.Load<TextAsset>("NFWLPlayers");
            return ta != null ? Parse(ta.text) : null;
        }

        public static JsonPlayerSource Parse(string json)
        {
            JsonPlayerSource src = new JsonPlayerSource();
            src.ReadTeam(json, "home", 0);
            src.ReadTeam(json, "away", 1);
            return src;
        }

        public bool TryGet(int team, int slotIndex, out PlayerData data)
        {
            data = null;
            if (team < 0 || team > 1 || slotIndex < 0 || slotIndex >= teams[team].Count) return false;
            data = teams[team][slotIndex];
            return data != null;
        }

        void ReadTeam(string json, string key, int team)
        {
            int k = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (k < 0) return;
            int open = json.IndexOf('[', k);
            if (open < 0) return;
            int close = Matching(json, open, '[', ']');
            if (close < 0) return;
            // Split the array into its top-level objects.
            int i = open + 1;
            while (i < close)
            {
                int o = json.IndexOf('{', i);
                if (o < 0 || o > close) break;
                int c = Matching(json, o, '{', '}');
                if (c < 0) break;
                teams[team].Add(ParsePlayer(json.Substring(o, c - o + 1)));
                i = c + 1;
            }
        }

        static int Matching(string s, int at, char open, char close)
        {
            int depth = 0;
            bool str = false;
            for (int i = at; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '"' && (i == 0 || s[i - 1] != '\\')) str = !str;
                if (str) continue;
                if (ch == open) depth++;
                else if (ch == close && --depth == 0) return i;
            }
            return -1;
        }

        static readonly Regex trait = new Regex("\"trait_type\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"value\"\\s*:\\s*\"?([^\",}]+)\"?", RegexOptions.Compiled);
        static readonly Regex field = new Regex("\"(\\w+)\"\\s*:\\s*\"?([^\",}\\]\\[{]+)\"?", RegexOptions.Compiled);

        public static PlayerData ParsePlayer(string obj)
        {
            PlayerData d = new PlayerData();
            foreach (Match m in trait.Matches(obj)) Apply(d, m.Groups[1].Value, m.Groups[2].Value.Trim());
            // Plain fields (our own shape), skipping trait_type / value pairs already read.
            string plain = trait.Replace(obj, "");
            foreach (Match m in field.Matches(plain)) Apply(d, m.Groups[1].Value, m.Groups[2].Value.Trim());
            Match sk = Regex.Match(obj, "\"skills\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (sk.Success)
                foreach (Match s in Regex.Matches(sk.Groups[1].Value, "\"(\\w+)\""))
                    if (Enum.TryParse(s.Groups[1].Value, true, out SkillId id) && id != SkillId.None && !d.skills.Contains(id)) d.skills.Add(id);
            return d;
        }

        static readonly HashSet<string> abilityKeys = new HashSet<string>
        {
            "speed", "acceleration", "agility", "ballcontrol", "shortpass", "longpass", "shotpower",
            "finishing", "tackling", "goalkeeping", "weakfoot", "weakfootrating"
        };

        static void Apply(PlayerData d, string key, string value)
        {
            string k = key.Replace(" ", "").Replace("_", "").ToLowerInvariant();
            switch (k)
            {
                case "name": d.name = value; return;
                case "tokenid": case "id": d.tokenId = value; return;
                case "element": d.element = value; return;
                case "number":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num) && num > 0 && num < 100) d.number = num;
                    return;
                case "rarity":
                    if (Enum.TryParse(value, true, out Rarity r)) { d.rarity = r; d.hasRarity = true; }
                    return;
                case "skill":
                    if (Enum.TryParse(value, true, out SkillId s) && s != SkillId.None && !d.skills.Contains(s)) d.skills.Add(s);
                    return;
                case "preferredfoot": case "foot":
                    d.traits["preferredfoot"] = value.StartsWith("L", StringComparison.OrdinalIgnoreCase) ? -1f : 1f;
                    return;
            }
            if (abilityKeys.Contains(k) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                d.traits[k] = f;
        }
    }

    // ------------------------------------------------------------------------------------- skills

    /// <summary>
    /// Extra skills for high-rarity players. A skill only tightens or shortens what the player can
    /// already do (accuracy, reaction time, tackle timing); it never adds speed beyond the player's
    /// body, more reach, or ball physics that the force model does not allow.
    /// </summary>
    public enum SkillId
    {
        None,
        PinpointPass,     // pass error x0.8
        CoolFinisher,     // shot error x0.8 (also under pressure)
        CatReflexes,      // keeper reaction time -0.03 s
        IronWall,         // standing tackle success x1.1 (still needs the foot to reach the ball)
        WeakFootMaster,   // weak foot penalty halved
    }

    /// <summary>Skill rules: how many slots each rarity has, and what each skill changes.</summary>
    public static class Skills
    {
        /// <summary>Common 0, Rare 0, Epic 1, Mythic 2, Legendary 3.</summary>
        public static int Slots(Rarity r) => r >= Rarity.Epic ? (int)r - 1 : 0;

        /// <summary>Keeps only as many skills as the rarity allows (first ones win).</summary>
        public static List<SkillId> Clamp(Rarity r, List<SkillId> wanted)
        {
            List<SkillId> list = new List<SkillId>();
            if (wanted == null) return list;
            int n = Slots(r);
            for (int i = 0; i < wanted.Count && list.Count < n; i++)
                if (wanted[i] != SkillId.None && !list.Contains(wanted[i])) list.Add(wanted[i]);
            return list;
        }

        public static float PassError(PlayerController p) => p.HasSkill(SkillId.PinpointPass) ? 0.8f : 1f;
        public static float ShotError(PlayerController p) => p.HasSkill(SkillId.CoolFinisher) ? 0.8f : 1f;
        public static float ReactionBonus(PlayerController p) => p.HasSkill(SkillId.CatReflexes) ? 0.03f : 0f;
        public static float TackleFactor(PlayerController p) => p.HasSkill(SkillId.IronWall) ? 1.1f : 1f;
        public static bool WeakFootMaster(PlayerController p) => p.HasSkill(SkillId.WeakFootMaster);
    }
}
