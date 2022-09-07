namespace ScriptsBase.Utilities;

using System;
using Models;

public static class CompressionTypeHelpers
{
    public static string CompressedExtension(this CompressionType type)
    {
        switch (type)
        {
            case CompressionType.TarLZip:
                return ".tar.lz";
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}
