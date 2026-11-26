using NUnit.Framework;
using NFWL.Web;

namespace NFWL.Tests
{
    /// <summary>Web platform bridge: the line-up JSON the website sends and the JSON writer used for events.</summary>
    public class WebBridgeTests
    {
        // Same shape as web/src/lib/server/match-session.ts builds for Unity (getTeamsJson).
        const string Teams = "{\"home\":[" +
            "{\"tokenId\":\"6f1c0a3e-1111-4222-8333-944455556666\",\"name\":\"VAEL ORRIN\",\"rarity\":\"Common\",\"element\":\"AMETHYST\",\"number\":1," +
            "\"attributes\":{\"Speed\":58,\"Acceleration\":57,\"Agility\":60,\"BallControl\":55,\"ShortPass\":56,\"LongPass\":59,\"ShotPower\":54,\"Finishing\":50,\"Tackling\":52,\"Goalkeeping\":65,\"PreferredFoot\":\"Left\",\"WeakFoot\":2},\"skills\":[]}," +
            "{\"tokenId\":\"b\",\"name\":\"ZEPHYR KAEL\",\"rarity\":\"Epic\",\"element\":\"VIOLET\",\"number\":10,\"attributes\":{\"Finishing\":120},\"skills\":[\"CoolFinisher\"]}" +
            "],\"away\":[{\"tokenId\":\"cpu-0\",\"name\":\"ORRIN VALE\",\"rarity\":\"Common\",\"element\":\"ICE\",\"number\":1,\"attributes\":{\"Goalkeeping\":64},\"skills\":[]}]}";

        [Test]
        public void ParsesWebLineup()
        {
            JsonPlayerSource src = JsonPlayerSource.Parse(Teams);
            Assert.AreEqual(2, src.Count(0));
            Assert.AreEqual(1, src.Count(1));
            Assert.IsTrue(src.TryGet(0, 0, out PlayerData gk));
            Assert.AreEqual("VAEL ORRIN", gk.name);
            Assert.AreEqual("AMETHYST", gk.element);
            Assert.AreEqual(1, gk.number);
            PlayerAttributes a = gk.BuildAttributes(new PlayerAttributes());
            Assert.AreEqual(65, a.Goalkeeping);
            Assert.AreEqual(58, a.Speed);
            Assert.AreEqual(PreferredFoot.Left, a.PreferredFoot);
            Assert.AreEqual(2, a.WeakFootRating);
        }

        [Test]
        public void ClampsAndReadsSkills()
        {
            JsonPlayerSource src = JsonPlayerSource.Parse(Teams);
            Assert.IsTrue(src.TryGet(0, 1, out PlayerData fw));
            Assert.AreEqual(Rarity.Epic, fw.rarity);
            Assert.AreEqual(10, fw.number);
            Assert.AreEqual(99, fw.BuildAttributes(new PlayerAttributes()).Finishing, "attributes are clamped to 40..99");
            Assert.Contains(SkillId.CoolFinisher, fw.skills);
        }

        [Test]
        public void ElementNames()
        {
            Assert.IsTrue(CrystalArt.TryElement("violet", out Element v));
            Assert.AreEqual("VIOLET", v.name);
            Assert.IsFalse(CrystalArt.TryElement("PLASMA", out _));
        }

        [Test]
        public void JsonWriterEscapes()
        {
            string s = new Json().Str("message", "a \"quoted\"\nline\\").Int("n", 3).IntOrNull("slot", null).Num("gameClock", 12.345f).ToString();
            Assert.AreEqual("{\"message\":\"a \\\"quoted\\\"\\nline\\\\\",\"n\":3,\"slot\":null,\"gameClock\":12.35}", s);
        }
    }
}
