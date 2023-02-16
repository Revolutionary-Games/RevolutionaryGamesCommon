namespace ScriptsBase.Utilities;

using System.Collections.Generic;

public static class LinqHelpers
{
    /// <summary>
    ///   Returns the index of an element in an enumerable
    /// </summary>
    /// <param name="enumerable">The sequence to search</param>
    /// <param name="value">The value to search for</param>
    /// <typeparam name="T">Type of the sequence</typeparam>
    /// <returns>Index of the value, -1 when not found</returns>
    public static int IndexOf<T>(this IEnumerable<T> enumerable, T value)
    {
        int index = 0;

        foreach (var current in enumerable)
        {
            if (ReferenceEquals(current, value) || (!ReferenceEquals(current, null) && current.Equals(value)))
            {
                return index;
            }

            ++index;
        }

        return -1;
    }
}
