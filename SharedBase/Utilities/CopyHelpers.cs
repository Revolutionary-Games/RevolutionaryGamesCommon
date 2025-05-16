namespace SharedBase.Utilities;

using System;
using System.IO;
using System.Runtime.Versioning;

public static class CopyHelpers
{
    /// <summary>
    ///   Copies a file to a folder. Unlike the inbuilt copy this doesn't need the destination file name as it is
    ///   automatically calculated.
    /// </summary>
    /// <param name="file">The file to copy</param>
    /// <param name="folder">Where to copy the file</param>
    /// <param name="overwrite">If true will overwrite already existing files</param>
    [UnsupportedOSPlatform("browser")]
    public static void CopyToFolder(string file, string folder, bool overwrite = true)
    {
        File.Copy(file, Path.Join(folder, Path.GetFileName(file)), overwrite);
    }

    /// <summary>
    ///   Copies folders and files recursively while preserving symlinks
    /// </summary>
    /// <param name="fromFolder">Where to copy from</param>
    /// <param name="targetFolder">Target folder to copy to</param>
    /// <param name="overwrite">If true overwriting files happens silently</param>
    [UnsupportedOSPlatform("browser")]
    public static void CopyFoldersRecursivelyWithSymlinks(string fromFolder, string targetFolder,
        bool overwrite = false)
    {
        if (!Directory.Exists(fromFolder))
            throw new ArgumentException("copy from must be an existing folder", nameof(fromFolder));

        Directory.CreateDirectory(targetFolder);

        foreach (var file in Directory.GetFiles(fromFolder))
        {
            var info = new FileInfo(file);

            var target = Path.Join(targetFolder, Path.GetFileName(file));

            if (info.LinkTarget != null)
            {
                // Handle link copying
                File.CreateSymbolicLink(target, info.LinkTarget);
            }
            else
            {
                info.CopyTo(target, overwrite);
            }
        }

        foreach (var directory in Directory.GetDirectories(fromFolder))
        {
            CopyFoldersRecursivelyWithSymlinks(directory, Path.Join(targetFolder, Path.GetFileName(directory)));
        }
    }

    [UnsupportedOSPlatform("browser")]
    public static void MoveToFolder(string file, string folder, bool overwrite = true)
    {
        File.Move(file, Path.Join(folder, Path.GetFileName(file)), overwrite);
    }

    /// <summary>
    ///   Moves files and folders from one folder to another
    /// </summary>
    /// <param name="fromFolder">The folder to move everything from</param>
    /// <param name="targetFolder">Target folder to put the moved files and folders in</param>
    /// <param name="overwrite">If true, overwriting files happens silently</param>
    [UnsupportedOSPlatform("browser")]
    public static void MoveFolderContents(string fromFolder, string targetFolder, bool overwrite = false)
    {
        foreach (var fileSystemEntry in Directory.EnumerateFileSystemEntries(fromFolder))
        {
            if (Directory.Exists(fileSystemEntry))
            {
                var finalTarget = Path.Join(targetFolder, Path.GetFileName(fileSystemEntry));

                if (Directory.Exists(finalTarget))
                {
                    Directory.Delete(finalTarget, true);
                }
                else if (File.Exists(finalTarget))
                {
                    File.Delete(finalTarget);
                }

                Directory.Move(fileSystemEntry, finalTarget);
            }
            else
            {
                MoveToFolder(fileSystemEntry, targetFolder, overwrite);
            }
        }
    }
}
