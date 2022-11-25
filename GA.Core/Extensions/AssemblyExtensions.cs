using System.Reflection;
using System.Runtime.CompilerServices;

namespace GA.Core.Extensions;

public static class AssemblyExtensions
{
    public static ImmutableList<Type> MethodOverrideTypes(this Assembly assembly, string methodName)
    {
        var list = new List<Type>();
        var types =
                assembly.GetTypes().Where(type => !type.IsAbstract && !type.ContainsGenericParameters).ToImmutableList();
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<CompilerGeneratedAttribute>();
            if (attr != null) continue;

            var toStringMethods = type.GetMethods()
                .Where(info => string.Equals(info.Name, methodName, StringComparison.OrdinalIgnoreCase)).ToImmutableList();
            if (!toStringMethods.Any()) continue;
            var toStringMethod = toStringMethods.FirstOrDefault();
            if (toStringMethod == null || !IsOverride(toStringMethod)) continue; ;
            list.Add(type);
        }

        return list.ToImmutableList();

        static bool IsOverride(MethodInfo method) => method.GetBaseDefinition().DeclaringType != method.DeclaringType;
    }
}

