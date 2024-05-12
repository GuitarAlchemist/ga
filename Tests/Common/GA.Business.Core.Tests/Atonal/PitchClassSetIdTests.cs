namespace GA.Business.Core.Tests.Atonal
{
    using Extensions;
    using GA.Business.Core.Atonal;
    using GA.Business.Core.Notes;

    public class PitchClassSetIdTests
    {
        [Test(TestOf = typeof(PitchClassSet))]
        public void Test_PitchClassSetId_Complement()
        {
            // Arrange
            const string sMajorTriadInput = "C E G";
            var majorTriadNotes = AccidentedNoteCollection.Parse(sMajorTriadInput);
            var majorTriadPcs = majorTriadNotes.ToPitchClassSet();
        
            // Act
            var id = majorTriadPcs.Id; // 145 value / 000010010001 binary value
            var complementId = id.Complement;
        
            // Assert
            Assert.That(complementId.BinaryValue, Is.EqualTo("111101101110"));
            Assert.That(complementId.Value, Is.EqualTo(3950));
        }            
    }
}
