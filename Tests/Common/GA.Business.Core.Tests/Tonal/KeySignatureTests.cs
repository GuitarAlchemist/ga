using GA.Business.Core.Tonal;
using NUnit.Framework;

namespace GA.Business.Core.Tests.Tonal
{
    public class KeySignatureTests
    {
        [Test]
        public void KeySignatureTest_Sharp()
        {
            Assert.AreEqual("", KeySignature.Sharp(0).SharpNotes.ToString());
            Assert.AreEqual("F", KeySignature.Sharp(1).SharpNotes.ToString());
            Assert.AreEqual("F C", KeySignature.Sharp(2).SharpNotes.ToString());
            Assert.AreEqual("F C G", KeySignature.Sharp(3).SharpNotes.ToString());
            Assert.AreEqual("F C G D", KeySignature.Sharp(4).SharpNotes.ToString());
            Assert.AreEqual("F C G D A", KeySignature.Sharp(5).SharpNotes.ToString());
            Assert.AreEqual("F C G D A E", KeySignature.Sharp(6).SharpNotes.ToString());
            Assert.AreEqual("F C G D A E B", KeySignature.Sharp(7).SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat()
        {
            Assert.AreEqual("B", KeySignature.Flat(1).FlatNotes.ToString());
            Assert.AreEqual("B E", KeySignature.Flat(2).FlatNotes.ToString());
            Assert.AreEqual("B E A", KeySignature.Flat(3).FlatNotes.ToString());
            Assert.AreEqual("B E A D", KeySignature.Flat(4).FlatNotes.ToString());
            Assert.AreEqual("B E A D G", KeySignature.Flat(5).FlatNotes.ToString());
            Assert.AreEqual("B E A D G C", KeySignature.Flat(6).FlatNotes.ToString());
            Assert.AreEqual("B E A D G C F", KeySignature.Flat(7).FlatNotes.ToString());
        }
    }
}