namespace ScriptsBase.Utilities;

using System;
using Models;

public static class CompressionTypeHelpers
{
    public static string CompressedExtension(this CompressionType type)
    {
        switch (type)
        {
            case CompressionType.P7Zip:
                return ".7z";
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}
