namespace GA.Business.Core.Fretboard.Fingering;

using System.ComponentModel;

public enum Finger
{
    [Description("X")]
    Muted,

    [Description("O")]
    Open,

    [Description("T")]
    Thumb,

    [Description("1")]
    Index,

    [Description("2")]
    Middle,

    [Description("3")]
    Ring,

    [Description("4")]
    Pinky
}