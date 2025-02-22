namespace GA.Business.Core.Tonal.Primitives;

public readonly record struct GenericScaleDegree(int Value) : IValueObject<GenericScaleDegree>
{
    public static GenericScaleDegree FromValue(int value) => new() { Value = value };
    
    public static implicit operator GenericScaleDegree(int value) => FromValue(value);
    public static implicit operator int(GenericScaleDegree degree) => degree.Value;
}