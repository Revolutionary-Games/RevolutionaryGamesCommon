namespace TestUtilities.Utilities;

using NSubstitute;

public static class ArgExtension
{
    /// <summary>
    ///   Matches any value that is not null
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="Arg"/> to match</typeparam>
    /// <returns>Whatever the underlying arg call returns</returns>
    public static T IsNotNull<T>()
    {
        return Arg.Is<T>(v => !ReferenceEquals(null, v));
    }
}
