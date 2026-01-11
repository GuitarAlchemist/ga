namespace GA.Data.MongoDB.Services;

// using EntityFramework.Data.Instruments;
using Models;

public interface IInstrumentService
{
    // Task<InstrumentDocument> CreateInstrumentAsync(InstrumentsRepository.InstrumentInfo instrumentInfo);
    Task<InstrumentDocument?> GetInstrumentAsync(string name);
    Task<List<InstrumentDocument>> GetAllInstrumentsAsync();
    // Task<bool> UpdateInstrumentAsync(string name, InstrumentsRepository.InstrumentInfo instrumentInfo);
    Task<bool> DeleteInstrumentAsync(string name);
    Task<List<InstrumentDocument>> SearchInstrumentsAsync(string searchTerm);
    Task<bool> ExistsAsync(string name);
}
