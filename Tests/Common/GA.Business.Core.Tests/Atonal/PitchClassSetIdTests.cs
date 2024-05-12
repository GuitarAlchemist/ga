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
            var id = majorTriadPcs.Id;
            var complementId = id.Complement;
        
            // Assert
        }            
    }
}
