namespace GaApi.Components.Pages;

using GaApi.GraphQL.Types;
using MongoDB.Bson;
using MongoDB.Driver;
using MudBlazor;
using GA.Business.Core.Atonal;
using GA.Business.Core.Unified;

public partial class Documents
{
    [Microsoft.AspNetCore.Components.Inject] private IUnifiedModeService UnifiedModeService { get; set; } = null!;

    private List<ProcessedDocumentType> _documents = [];
    private List<ProcessedDocumentType> _searchResults = [];
    private ProcessedDocumentType? _selectedDocument;
    private bool _loading;
    private bool _uploading;
    private bool _searching;
    private bool _showDocumentDialog;
    private string _searchText = string.Empty;
    private string _filterSourceType = string.Empty;
    private string _uploadUrl = string.Empty;
    private string _uploadSourceType = "YouTube";
    private string _uploadTitle = string.Empty;
    private string _uploadContent = string.Empty;
    private string _uploadTextSourceType = "Markdown";
    private string _semanticSearchQuery = string.Empty;

    // Unified Mode quick summary
    private string _unifiedInput = "0,2,4,5,7,9,11"; // default Ionian
    private UnifiedModeDescription? _unifiedDesc;

    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };

    protected override async Task OnInitializedAsync()
    {
        await LoadDocuments();
    }

    private async Task LoadDocuments()
    {
        _loading = true;
        try
        {
            var collection = MongoDb.ProcessedDocuments;
            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Empty;

            // Apply source type filter
            if (!string.IsNullOrEmpty(_filterSourceType))
            {
                filter &= filterBuilder.Eq("sourceType", _filterSourceType);
            }

            // Apply text search filter
            if (!string.IsNullOrEmpty(_searchText))
            {
                var textFilter = filterBuilder.Or(
                    filterBuilder.Regex("title", new BsonRegularExpression(_searchText, "i")),
                    filterBuilder.Regex("summary", new BsonRegularExpression(_searchText, "i"))
                );
                filter &= textFilter;
            }

            var documents = await collection
                .Find(filter)
                .Sort(Builders<BsonDocument>.Sort.Descending("processedAt"))
                .Limit(100)
                .ToListAsync();

            _documents = [.. documents.Select(MapBsonToProcessedDocument)];
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading documents: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SearchDocuments()
    {
        await LoadDocuments();
    }

    private void ViewDocument(ProcessedDocumentType document)
    {
        _selectedDocument = document;
        _showDocumentDialog = true;
    }

    private void CloseDocumentDialog()
    {
        _showDocumentDialog = false;
        _selectedDocument = null;
    }

    private async Task DeleteDocument(string id)
    {
        try
        {
            var collection = MongoDb.ProcessedDocuments;
            var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(id));
            await collection.DeleteOneAsync(filter);

            Snackbar.Add("Document deleted successfully", Severity.Success);
            await LoadDocuments();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error deleting document: {ex.Message}", Severity.Error);
        }
    }

    private Task UploadFromUrl()
    {
        if (string.IsNullOrWhiteSpace(_uploadUrl))
        {
            Snackbar.Add("Please enter a URL", Severity.Warning);
            return Task.CompletedTask;
        }

        _uploading = true;
        try
        {
            // TODO: Call GraphQL mutation to upload document
            // For now, just show a success message
            Snackbar.Add("Document upload started. This feature is coming soon!", Severity.Info);
            _uploadUrl = string.Empty;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error uploading document: {ex.Message}", Severity.Error);
        }
        finally
        {
            _uploading = false;
        }

        return Task.CompletedTask;
    }

    private Task UploadTextContent()
    {
        if (string.IsNullOrWhiteSpace(_uploadTitle) || string.IsNullOrWhiteSpace(_uploadContent))
        {
            Snackbar.Add("Please enter both title and content", Severity.Warning);
            return Task.CompletedTask;
        }

        _uploading = true;
        try
        {
            // TODO: Call GraphQL mutation to upload document
            // For now, just show a success message
            Snackbar.Add("Document upload started. This feature is coming soon!", Severity.Info);
            _uploadTitle = string.Empty;
            _uploadContent = string.Empty;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error uploading document: {ex.Message}", Severity.Error);
        }
        finally
        {
            _uploading = false;
        }

        return Task.CompletedTask;
    }

    private Task PerformSemanticSearch()
    {
        if (string.IsNullOrWhiteSpace(_semanticSearchQuery))
        {
            Snackbar.Add("Please enter a search query", Severity.Warning);
            return Task.CompletedTask;
        }

        _searching = true;
        try
        {
            // TODO: Call GraphQL query for semantic search
            // For now, just show a message
            Snackbar.Add("Semantic search is coming soon!", Severity.Info);
            _searchResults = [];
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error performing search: {ex.Message}", Severity.Error);
        }
        finally
        {
            _searching = false;
        }

        return Task.CompletedTask;
    }

    private static Color GetSourceTypeColor(string sourceType)
    {
        return sourceType switch
        {
            "YouTube" => Color.Error,
            "PDF" => Color.Primary,
            "Markdown" => Color.Info,
            "Text" => Color.Secondary,
            "Web" => Color.Success,
            _ => Color.Default
        };
    }

    private static ProcessedDocumentType MapBsonToProcessedDocument(BsonDocument doc)
    {
        return new ProcessedDocumentType
        {
            Id = doc["_id"].AsObjectId.ToString(),
            Title = doc.GetValue("title", "Untitled").AsString,
            Summary = doc.GetValue("summary", "").AsString,
            SourceType = doc.GetValue("sourceType", "Unknown").AsString,
            SourceUrl = doc.GetValue("sourceUrl", "").AsString,
            ProcessedAt = doc.GetValue("processedAt", DateTime.UtcNow).ToUniversalTime(),
            Metadata = doc.Contains("metadata") ? new DocumentMetadataType
            {
                VideoId = doc["metadata"].AsBsonDocument.GetValue("videoId", "").AsString,
                ChannelName = doc["metadata"].AsBsonDocument.GetValue("channelName", "").AsString,
                Duration = TimeSpan.FromSeconds(doc["metadata"].AsBsonDocument.GetValue("duration", 0).ToInt32()),
                ViewCount = doc["metadata"].AsBsonDocument.GetValue("viewCount", 0).ToInt32(),
                QualityScore = doc["metadata"].AsBsonDocument.GetValue("qualityScore", 0.0).ToDouble()
            } : null,
            ExtractedKnowledge = doc.Contains("extractedKnowledge") ? new ExtractedKnowledgeType
            {
                ChordProgressions = [.. doc["extractedKnowledge"].AsBsonDocument.GetValue("chordProgressions", new BsonArray())
                    .AsBsonArray.Select(x => x.AsString)],
                Scales = [.. doc["extractedKnowledge"].AsBsonDocument.GetValue("scales", new BsonArray())
                    .AsBsonArray.Select(x => x.AsString)],
                Techniques = [.. doc["extractedKnowledge"].AsBsonDocument.GetValue("techniques", new BsonArray())
                    .AsBsonArray.Select(x => x.AsString)],
                Concepts = [.. doc["extractedKnowledge"].AsBsonDocument.GetValue("concepts", new BsonArray())
                    .AsBsonArray.Select(x => x.AsString)]
            } : null
        };
    }

    private Task AnalyzeUnified()
    {
        try
        {
            var parts = _unifiedInput
                .Split(new[] { ',', ' ', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var v) ? v : -1)
                .Where(v => v >= 0 && v <= 11)
                .Distinct()
                .OrderBy(v => v)
                .ToArray();

            if (parts.Length == 0)
            {
                Snackbar.Add("Enter 0–11 pitch classes (e.g., 0,2,4,5,7,9,11)", Severity.Warning);
                _unifiedDesc = null;
                return Task.CompletedTask;
            }

            var pcs = new PitchClassSet([.. parts.Select(PitchClass.FromValue)]);
            var inst = UnifiedModeService.FromPitchClassSet(pcs, PitchClass.C);
            _unifiedDesc = UnifiedModeService.Describe(inst);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Unified summary failed: {ex.Message}", Severity.Error);
            _unifiedDesc = null;
        }

        return Task.CompletedTask;
    }
}

