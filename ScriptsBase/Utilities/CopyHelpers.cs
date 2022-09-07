namespace ScriptsBase.Utilities;

using System.IO;

public static class CopyHelpers
{
    /// <summary>
    ///   Copies a file to a folder. Unlike the inbuilt copy this doesn't need the destination file name as it is
    ///   automatically calculated.
    /// </summary>
    /// <param name="file">The file to copy</param>
    /// <param name="folder">Where to copy the file</param>
    /// <param name="overwrite">If true will overwrite already existing files</param>
    public static void CopyToFolder(string file, string folder, bool overwrite = true)
    {
        File.Copy(file, Path.Join(folder, Path.GetFileName(file)), overwrite);
    }
}
