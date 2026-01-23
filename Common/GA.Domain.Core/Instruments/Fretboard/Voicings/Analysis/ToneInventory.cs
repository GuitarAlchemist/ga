namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;

public record ToneInventory(string[] Tones, bool HasGuideTones, string[] OmittedTones, string[] DoubledTones);