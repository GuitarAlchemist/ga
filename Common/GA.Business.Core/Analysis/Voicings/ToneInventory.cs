namespace GA.Business.Core.Analysis.Voicings;

public record ToneInventory(string[] Tones, bool HasGuideTones, string[] OmittedTones, string[] DoubledTones);
