namespace GA.Data.MongoDB.Models;

public interface IMusicalObjectsService
{
    // Notes
    Task<List<NoteDocument>> GetAllNotesAsync();
    Task<bool> SyncNotesAsync();
    
    // Intervals
    Task<List<IntervalDocument>> GetAllIntervalsAsync();
    Task<bool> SyncIntervalsAsync();
    
    // Keys
    Task<List<KeyDocument>> GetAllKeysAsync();
    Task<bool> SyncKeysAsync();
    
    // Scales
    Task<List<ScaleDocument>> GetAllScalesAsync();
    Task<bool> SyncScalesAsync();
    
    // Pitch Classes
    Task<List<PitchClassDocument>> GetAllPitchClassesAsync();
    Task<bool> SyncPitchClassesAsync();
    
    // Full sync
    Task<SyncResult> SyncAllAsync();
}