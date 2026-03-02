namespace GA.InteractiveExtension.Ga;

using Core.Extensions;

public static class GaFormattingExtensions
{
    extension(Assembly assembly)
    {
        public ImmutableList<Type> RegisterFormatters()
        {
            ArgumentNullException.ThrowIfNull(assembly);

            var result = assembly.MethodOverrideTypes("toString");
            foreach (var type in result)
            {
                Formatter.Register(
                    type,
                    (value, textWriter) => textWriter.Write(value?.ToString()));
            }

            return result;
        }
    }
}
