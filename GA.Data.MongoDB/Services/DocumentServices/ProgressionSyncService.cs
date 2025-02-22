namespace GA.Data.MongoDB.Services.DocumentServices;

using Models;
using Microsoft.Extensions.Logging;

[UsedImplicitly]
public class ProgressionSyncService(ILogger<ProgressionSyncService> logger, MongoDbService mongoDb) : ISyncService<ProgressionDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var documents = new List<ProgressionDocument>
            {
                new()
                {
                    Name = "I-IV-V",
                    Key = "C",
                    Chords = ["C", "F", "G"],
                    RomanNumerals = ["I", "IV", "V"],
                    Category = "Pop",
                    Description = "Most common progression in popular music",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "ii-V-I",
                    Key = "C",
                    Chords = ["Dm", "G", "C"],
                    RomanNumerals = ["ii", "V", "I"],
                    Category = "Jazz",
                    Description = "Common jazz progression",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-V-vi-IV",
                    Key = "C",
                    Chords = ["C", "G", "Am", "F"],
                    RomanNumerals = ["I", "V", "vi", "IV"],
                    Category = "Pop",
                    Description = "Popular progression used in many contemporary songs",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "vi-IV-I-V",
                    Key = "C",
                    Chords = ["Am", "F", "C", "G"],
                    RomanNumerals = ["vi", "IV", "I", "V"],
                    Category = "Pop",
                    Description = "Variation of I-V-vi-IV, starting from vi",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-vi-IV-V",
                    Key = "C",
                    Chords = ["C", "Am", "F", "G"],
                    RomanNumerals = ["I", "vi", "IV", "V"],
                    Category = "Pop",
                    Description = "50s progression, common in doo-wop",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "ii-V-I-vi",
                    Key = "C",
                    Chords = ["Dm", "G", "C", "Am"],
                    RomanNumerals = ["ii", "V", "I", "vi"],
                    Category = "Jazz",
                    Description = "Extended jazz progression with vi",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-V-vi-iii-IV-I-IV-V",
                    Key = "C",
                    Chords = ["C", "G", "Am", "Em", "F", "C", "F", "G"],
                    RomanNumerals = ["I", "V", "vi", "iii", "IV", "I", "IV", "V"],
                    Category = "Pop",
                    Description = "Extended progression used in many ballads",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-VI-III-VII",
                    Key = "Am",
                    Chords = ["Am", "F", "C", "G"],
                    RomanNumerals = ["i", "VI", "III", "VII"],
                    Category = "Rock",
                    Description = "Common minor key progression in rock music",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-vi-ii-V",
                    Key = "C",
                    Chords = ["C", "Am", "Dm", "G"],
                    RomanNumerals = ["I", "vi", "ii", "V"],
                    Category = "Jazz",
                    Description = "Common turnaround in jazz standards",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-V-vi-IV (Canon)",
                    Key = "C",
                    Chords = ["C", "G", "Am", "F"],
                    RomanNumerals = ["I", "V", "vi", "IV"],
                    Category = "Classical",
                    Description = "Pachelbel's Canon progression, widely used in classical and pop music",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-VII-VI-V",
                    Key = "Am",
                    Chords = ["Am", "G", "F", "E"],
                    RomanNumerals = ["i", "VII", "VI", "V"],
                    Category = "Rock",
                    Description = "Common descending minor progression in rock and metal",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-vi-IV-V7",
                    Key = "C",
                    Chords = ["C", "Am", "F", "G7"],
                    RomanNumerals = ["I", "vi", "IV", "V7"],
                    Category = "Blues",
                    Description = "Common progression in early rock and roll",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "ii7-V7-I",
                    Key = "C",
                    Chords = ["Dm7", "G7", "C"],
                    RomanNumerals = ["ii7", "V7", "I"],
                    Category = "Jazz",
                    Description = "Essential jazz progression with seventh chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-♭VII-♭VI-V",
                    Key = "C",
                    Chords = ["C", "B♭", "A♭", "G"],
                    RomanNumerals = ["I", "♭VII", "♭VI", "V"],
                    Category = "Rock",
                    Description = "Common in rock music, featuring borrowed chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭VI-♭III-♭VII",
                    Key = "Am",
                    Chords = ["Am", "F", "C", "G"],
                    RomanNumerals = ["i", "♭VI", "♭III", "♭VII"],
                    Category = "Metal",
                    Description = "Common in metal and hard rock",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-IV-♭VII-IV",
                    Key = "C",
                    Chords = ["C", "F", "B♭", "F"],
                    RomanNumerals = ["I", "IV", "♭VII", "IV"],
                    Category = "Rock",
                    Description = "Common in classic rock with mixolydian influence",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭VII-♭VI-V",
                    Key = "Am",
                    Chords = ["Am", "G", "F", "E"],
                    RomanNumerals = ["i", "♭VII", "♭VI", "V"],
                    Category = "Metal",
                    Description = "Andalusian cadence, common in metal and flamenco",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-iii-IV-iv",
                    Key = "C",
                    Chords = ["C", "Em", "F", "Fm"],
                    RomanNumerals = ["I", "iii", "IV", "iv"],
                    Category = "Jazz",
                    Description = "Jazz progression with minor subdominant",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-iv-v-i",
                    Key = "Am",
                    Chords = ["Am", "Dm", "Em", "Am"],
                    RomanNumerals = ["i", "iv", "v", "i"],
                    Category = "Classical",
                    Description = "Natural minor progression",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-V7-vi-IV (Pop Punk)",
                    Key = "C",
                    Chords = ["C", "G7", "Am", "F"],
                    RomanNumerals = ["I", "V7", "vi", "IV"],
                    Category = "Rock",
                    Description = "Common in pop punk and alternative rock",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-VI-III-VII (Minor Pop)",
                    Key = "Am",
                    Chords = ["Am", "F", "C", "G"],
                    RomanNumerals = ["i", "VI", "III", "VII"],
                    Category = "Pop",
                    Description = "Popular minor key progression in modern pop",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-iv-v7-i (Minor Blues)",
                    Key = "Am",
                    Chords = ["Am", "Dm", "E7", "Am"],
                    RomanNumerals = ["i", "iv", "v7", "i"],
                    Category = "Blues",
                    Description = "Basic minor blues progression",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I7-IV7-V7 (12-Bar Blues)",
                    Key = "C",
                    Chords = ["C7", "F7", "G7"],
                    RomanNumerals = ["I7", "IV7", "V7"],
                    Category = "Blues",
                    Description = "Core of 12-bar blues progression",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭VII-♭VI-i (Doom)",
                    Key = "Am",
                    Chords = ["Am", "G", "F", "Am"],
                    RomanNumerals = ["i", "♭VII", "♭VI", "i"],
                    Category = "Metal",
                    Description = "Common in doom metal and stoner rock",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭III-♭VII-i (Epic)",
                    Key = "Em",
                    Chords = ["Em", "G", "D", "Em"],
                    RomanNumerals = ["i", "♭III", "♭VII", "i"],
                    Category = "Metal",
                    Description = "Common in epic and power metal",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-♭III-♭VII (Modal)",
                    Key = "C",
                    Chords = ["C", "E♭", "B♭"],
                    RomanNumerals = ["I", "♭III", "♭VII"],
                    Category = "Rock",
                    Description = "Common in modal rock, uses mixolydian and dorian modes",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "iiø7-V7-i (Minor ii-V-i)",
                    Key = "Am",
                    Chords = ["Bø7", "E7", "Am"],
                    RomanNumerals = ["iiø7", "V7", "i"],
                    Category = "Jazz",
                    Description = "Common minor key jazz progression with half-diminished chord",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-vi-ii7-V7 (Circle)",
                    Key = "C",
                    Chords = ["C", "Am", "Dm7", "G7"],
                    RomanNumerals = ["I", "vi", "ii7", "V7"],
                    Category = "Jazz",
                    Description = "Circle progression, common in jazz standards",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭VI-♭VII (Aeolian)",
                    Key = "Am",
                    Chords = ["Am", "F", "G"],
                    RomanNumerals = ["i", "♭VI", "♭VII"],
                    Category = "Rock",
                    Description = "Common in aeolian mode rock songs",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-V-vi-iii-IV-I-V-V (Extended Pop)",
                    Key = "C",
                    Chords = ["C", "G", "Am", "Em", "F", "C", "G", "G"],
                    RomanNumerals = ["I", "V", "vi", "iii", "IV", "I", "V", "V"],
                    Category = "Pop",
                    Description = "Extended pop progression with repeated dominant",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭VI-♭III-♭VII-i (Epic Minor)",
                    Key = "Am",
                    Chords = ["Am", "F", "C", "G", "Am"],
                    RomanNumerals = ["i", "♭VI", "♭III", "♭VII", "i"],
                    Category = "Metal",
                    Description = "Extended minor progression common in symphonic metal",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-IV-♭VII-IV (Mixolydian)",
                    Key = "D",
                    Chords = ["D", "G", "C", "G"],
                    RomanNumerals = ["I", "IV", "♭VII", "IV"],
                    Category = "Rock",
                    Description = "Common in folk rock and modal rock",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-VI-III-VII-i (Extended Minor)",
                    Key = "Dm",
                    Chords = ["Dm", "B♭", "F", "C", "Dm"],
                    RomanNumerals = ["i", "VI", "III", "VII", "i"],
                    Category = "Pop",
                    Description = "Extended minor progression common in modern pop",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "IVmaj7-iii7-ii7-I (Jazz Pop)",
                    Key = "C",
                    Chords = ["Fmaj7", "Em7", "Dm7", "C"],
                    RomanNumerals = ["IVmaj7", "iii7", "ii7", "I"],
                    Category = "Jazz",
                    Description = "Sophisticated pop progression with seventh chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-♭VII-v-iv (Phrygian)",
                    Key = "Em",
                    Chords = ["Em", "D", "Bm", "Am"],
                    RomanNumerals = ["i", "♭VII", "v", "iv"],
                    Category = "Metal",
                    Description = "Common in metal using phrygian mode",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Folk Progressions
                new()
                {
                    Name = "I-V-IV-I (Folk)",
                    Key = "G",
                    Chords = ["G", "D", "C", "G"],
                    RomanNumerals = ["I", "V", "IV", "I"],
                    Category = "Folk",
                    Description = "Common folk progression, foundation of many traditional songs",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-ii-IV-I (Folk Ballad)",
                    Key = "G",
                    Chords = ["G", "Am", "C", "G"],
                    RomanNumerals = ["I", "ii", "IV", "I"],
                    Category = "Folk",
                    Description = "Gentle progression common in folk ballads",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "vi-IV-I-V (Modern Folk)",
                    Key = "C",
                    Chords = ["Am", "F", "C", "G"],
                    RomanNumerals = ["vi", "IV", "I", "V"],
                    Category = "Folk",
                    Description = "Contemporary folk progression with minor start",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Rock Progressions
                new()
                {
                    Name = "I-♭VII-IV (Power Rock)",
                    Key = "E",
                    Chords = ["E", "D", "A"],
                    RomanNumerals = ["I", "♭VII", "IV"],
                    Category = "Rock",
                    Description = "Common in hard rock, often with power chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I-♭VI-♭VII (Heavy Rock)",
                    Key = "E",
                    Chords = ["E", "C", "D"],
                    RomanNumerals = ["I", "♭VI", "♭VII"],
                    Category = "Rock",
                    Description = "Heavy rock progression with borrowed chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i-VII-v-iv (Dark Rock)",
                    Key = "Am",
                    Chords = ["Am", "G", "Em", "Dm"],
                    RomanNumerals = ["i", "VII", "v", "iv"],
                    Category = "Rock",
                    Description = "Minor progression common in alternative rock",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Blues Progressions
                new()
                {
                    Name = "I7-IV7-V7-IV7 (Quick Change)",
                    Key = "A",
                    Chords = ["A7", "D7", "E7", "D7"],
                    RomanNumerals = ["I7", "IV7", "V7", "IV7"],
                    Category = "Blues",
                    Description = "Blues progression with quick IV chord change",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I7-IV9-V13 (Jazz Blues)",
                    Key = "C",
                    Chords = ["C7", "F9", "G13"],
                    RomanNumerals = ["I7", "IV9", "V13"],
                    Category = "Blues",
                    Description = "Jazz-influenced blues with extended chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "i7-iv7-V7 (Minor Blues)",
                    Key = "Am",
                    Chords = ["Am7", "Dm7", "E7"],
                    RomanNumerals = ["i7", "iv7", "V7"],
                    Category = "Blues",
                    Description = "Minor blues progression with seventh chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                // Jazz Progressions
                new()
                {
                    Name = "ii7-V7-iii7-VI7 (Bird Changes)",
                    Key = "C",
                    Chords = ["Dm7", "G7", "Em7", "A7"],
                    RomanNumerals = ["ii7", "V7", "iii7", "VI7"],
                    Category = "Jazz",
                    Description = "Bebop progression inspired by Charlie Parker",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "imaj7-vi7-ii7-V7 (Modal Jazz)",
                    Key = "C",
                    Chords = ["Cmaj7", "Am7", "Dm7", "G7"],
                    RomanNumerals = ["imaj7", "vi7", "ii7", "V7"],
                    Category = "Jazz",
                    Description = "Modal jazz progression with seventh chords",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "iiø7-V7alt-i (Altered Jazz)",
                    Key = "Am",
                    Chords = ["Bø7", "E7alt", "Am"],
                    RomanNumerals = ["iiø7", "V7alt", "i"],
                    Category = "Jazz",
                    Description = "Jazz progression with altered dominant",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                new()
                {
                    Name = "I6-vi7-ii7-V7 (Standards)",
                    Key = "C",
                    Chords = ["C6", "Am7", "Dm7", "G7"],
                    RomanNumerals = ["I6", "vi7", "ii7", "V7"],
                    Category = "Jazz",
                    Description = "Common in jazz standards with sixth chord",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await mongoDb.Progressions.DeleteManyAsync(Builders<ProgressionDocument>.Filter.Empty);
            await mongoDb.Progressions.InsertManyAsync(documents);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing progressions");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.Progressions.CountDocumentsAsync(Builders<ProgressionDocument>.Filter.Empty);
}