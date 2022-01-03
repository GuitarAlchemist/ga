using GA.Business.Core.Tonal;
using NUnit.Framework;

namespace GA.Business.Core.Tests.Tonal
{
    public class KeySignatureTests
    {
        [Test]
        public void KeySignatureTest_Sharp0()
        {
            Assert.AreEqual("", KeySignature.Sharp0.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp1()
        {
            Assert.AreEqual("F", KeySignature.Sharp1.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp2()
        {
            Assert.AreEqual("F C", KeySignature.Sharp2.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp3()
        {
            Assert.AreEqual("F C G", KeySignature.Sharp3.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp4()
        {
            Assert.AreEqual("F C G D", KeySignature.Sharp4.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp5()
        {
            Assert.AreEqual("F C G D A", KeySignature.Sharp5.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp6()
        {
            Assert.AreEqual("F C G D A E", KeySignature.Sharp6.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Sharp7()
        {
            Assert.AreEqual("F C G D A E B", KeySignature.Sharp7.SharpNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat0()
        {
            Assert.AreEqual("", KeySignature.Flat0.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat1()
        {
            Assert.AreEqual("B", KeySignature.Flat1.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat2()
        {
            Assert.AreEqual("B E", KeySignature.Flat2.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat3()
        {
            Assert.AreEqual("B E A", KeySignature.Flat3.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat4()
        {
            Assert.AreEqual("B E A D", KeySignature.Flat4.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat5()
        {
            Assert.AreEqual("B E A D G", KeySignature.Flat5.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat6()
        {
            Assert.AreEqual("B E A D G C", KeySignature.Flat6.FlatNotes.ToString());
        }

        [Test]
        public void KeySignatureTest_Flat7()
        {
            Assert.AreEqual("B E A D G C F", KeySignature.Flat7.FlatNotes.ToString());
        }
    }
}