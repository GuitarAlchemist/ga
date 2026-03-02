namespace GA.InteractiveExtension.ExtensionMethods;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

[PublicAPI]
public static class ResearchExtensions
{
    extension<T>(IEnumerable<T> items)
    {
        /// <summary>
        /// Displays the collection as a table in the notebook.
        /// </summary>
        public void DisplayTable() => items.Display();
    }

    extension<T>([DisallowNull] T item)
    {
        /// <summary>
        /// Displays the item in the notebook.
        /// </summary>
        public void DisplayItem() => item.Display();
    }
}
