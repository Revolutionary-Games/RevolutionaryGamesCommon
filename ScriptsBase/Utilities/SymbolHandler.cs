﻿namespace ScriptsBase.Utilities;

using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SharedBase.Utilities;

/// <summary>
///   Helpers for handling symbol files in compiled binaries
/// </summary>
public class SymbolHandler
{
    public const string SYMBOLS_FOLDER = "symbols";

    public const string PDB_EXTENSION = ".pdb";

    private const string SYMBOL_EXTRACTOR_PATH_BASE = "../breakpad/build";
    private const string SYMBOL_EXTRACTOR_LINUX = "src/tools/linux/dump_syms/dump_syms";
    private const string SYMBOL_EXTRACTOR_MAC = "dump_syms";

    private const string SYMBOL_EXTRACTOR_WINDOWS = "../src/src/tools/windows/Release/dump_syms.exe";

    /// <summary>
    ///   At tool that is needed to be in path on Windows. Assumes Visual Studio 2019 community with default install
    ///   path. TODO: make configurable
    /// </summary>
    private const string WINDOWS_DIA_PATH =
        "C:/Program Files (x86)/Microsoft Visual Studio/2019/Community/Common7/IDE/Remote Debugger/x64";

    private const string MINGW_SYMBOL_EXTRACTOR_PATH_BASE = "../breakpad-mingw";

    private const string MINGW_EXTRACTOR_LINUX = "src/tools/windows/dump_syms_dwarf/dump_syms";

    private static readonly Regex BreakpadSymbolInfoRegex = new(@"MODULE\s(\w+)\s(\w+)\s(\w+)\s(\S+)");

    private readonly string? overwriteLinuxName;
    private readonly string? overwriteWindowsName;
    private readonly bool usesRename;

    public SymbolHandler(string? overwriteLinuxName, string? overwriteWindowsName)
    {
        this.overwriteLinuxName = overwriteLinuxName;
        this.overwriteWindowsName = overwriteWindowsName;

        usesRename = !string.IsNullOrEmpty(overwriteLinuxName) || !string.IsNullOrEmpty(overwriteWindowsName);
    }

    /// <summary>
    ///   Extracts symbols from a combined binary for the specified architecture (arm64 or x86_64). Used on Mac with
    ///   lipo-ed libraries.
    /// </summary>
    public string? ExtractSpecificArchitecture { get; set; }

    public async Task<bool> ExtractSymbols(string file, string outputFolder, bool mingw,
        CancellationToken cancellationToken)
    {
        var startInfo = StartInfoForSymbolExtractor(mingw);

        ColourConsole.WriteDebugLine($"Trying to run symbol extractor: {startInfo.FileName}");

        if (!string.IsNullOrEmpty(ExtractSpecificArchitecture))
        {
            ColourConsole.WriteDebugLine($"Extracting specific architecture symbols: {ExtractSpecificArchitecture}");
            startInfo.ArgumentList.Add("-a");
            startInfo.ArgumentList.Add(ExtractSpecificArchitecture);
        }

        startInfo.ArgumentList.Add(Path.GetFullPath(file));

        var result = await ProcessRunHelpers.RunProcessAsync(startInfo, cancellationToken, true, false);

        if (result.ExitCode != 0 || result.StdOut.Length < 1)
        {
            ColourConsole.WriteNormalLine($"Extractor was running on: {file}");
            ColourConsole.WriteErrorLine($"Failed to run extractor: {result.FullOutput.Truncate(1000)}");
            return false;
        }

        // Place it correctly (this makes local dumping work, but when sending to a server this doesn't really matter)
        var (platform, arch, hash, name) = ParseBreakpadSymbolInfo(result.Output);

        ColourConsole.WriteDebugLine($"Symbol info: platform: {platform}, arch: {arch}, hash: {hash}, name: {name}");

        if (usesRename)
        {
            // Because the exported game executable is renamed, we need to use those names here.
            // Except on windows that doesn't seem to apply (when extracted from pdb).

            if (file.EndsWith(PDB_EXTENSION))
            {
                ColourConsole.WriteNormalLine(
                    $"Special handling of .pdb extracted symbols, keeping original name in name ({name})");
            }
            else
            {
                var downCasedPlatform = platform.ToLowerInvariant();

                if (downCasedPlatform.Contains("linux"))
                {
                    name = overwriteLinuxName;
                }
                else if (downCasedPlatform.Contains("windows"))
                {
                    name = overwriteWindowsName;
                }
                else
                {
                    // TODO: add mac handling here once Breakpad in Godot is setup to be used with it
                    ColourConsole.WriteWarningLine($"No special 'Thrive' symbol name known for platform: {platform}");
                }
            }
        }

        if (string.IsNullOrEmpty(name))
            throw new InvalidOperationException($"Symbol file name ended up being empty ({hash})");

        Directory.CreateDirectory(Path.Join(outputFolder, SYMBOLS_FOLDER, name, hash));

        // The .pdb extension is always removed in the final file name if present
        string fileName;
        if (name.EndsWith(PDB_EXTENSION))
        {
            fileName = name.Substring(0, name.Length - PDB_EXTENSION.Length);
        }
        else
        {
            fileName = name;
        }

        await File.WriteAllTextAsync(Path.Join(outputFolder, SYMBOLS_FOLDER, name, hash, fileName + ".sym"),
            result.Output, cancellationToken);

        return true;
    }

    /// <summary>
    ///   Parses symbol info from a Breakpad .sym file
    /// </summary>
    /// <param name="symbolFileContents">The file contents to parse</param>
    /// <returns>The parsed info</returns>
    /// <exception cref="ArgumentException">If parsing fails</exception>
    private static (string Platform, string Arch, string Hash, string Name) ParseBreakpadSymbolInfo(
        string symbolFileContents)
    {
        var match = BreakpadSymbolInfoRegex.Match(symbolFileContents);

        if (match.Success)
        {
            if (match.Groups.Count != 5)
                throw new Exception("Programming error in regex matching");

            return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
        }

        throw new ArgumentException("Failed to read symbol info");
    }

    private static ProcessStartInfo StartInfoForSymbolExtractor(bool mingw)
    {
        if (mingw)
            return new ProcessStartInfo(Path.Join(MINGW_SYMBOL_EXTRACTOR_PATH_BASE, MINGW_EXTRACTOR_LINUX));

        if (OperatingSystem.IsWindows())
        {
            var startInfo = new ProcessStartInfo(Path.Join(SYMBOL_EXTRACTOR_PATH_BASE, SYMBOL_EXTRACTOR_WINDOWS));

            if (!Directory.Exists(WINDOWS_DIA_PATH))
            {
                // ReSharper disable once StringLiteralTypo
                throw new Exception($"Expected Visual Studio msdia dll path doesn't exist: {WINDOWS_DIA_PATH}");
            }

            if (ProcessRunHelpers.AddToPathInStartInfo(startInfo, WINDOWS_DIA_PATH))
            {
                // ReSharper disable once StringLiteralTypo
                ColourConsole.WriteNormalLine($"Adding {WINDOWS_DIA_PATH} to PATH for msdia dll files");
            }

            return startInfo;
        }

        if (OperatingSystem.IsMacOS())
        {
            // Breakpad needs to be compiled with XCode project files so the end result ends up being quite different
            // And the final copy is also annoying
            var extractor = ExecutableFinder.Which(SYMBOL_EXTRACTOR_MAC);
            if (string.IsNullOrEmpty(extractor))
            {
                ExecutableFinder.PrintPathInfo(Console.Out);

                ColourConsole.WriteNormalLine("Mac compile command: xcodebuild " +
                    "-workspace dump_syms.xcodeproj/project.xcworkspace -scheme dump_syms install");
                ColourConsole.WriteNormalLine("That command is to be ran in the breakpad/src/src/tools/mac/dump_syms " +
                    "folder");
                ColourConsole.WriteNormalLine("Then copy the result (as for some reason Breakpad build doesn't work " +
                    "with proper destination): from '/Users/LONG_INTERMEDIATE_PATH/Build/Intermediates.noindex/" +
                    "ArchiveIntermediates/dump_syms/InstallationBuildProductsLocation/usr/local/bin/dump_syms' " +
                    $"to a folder that is in your PATH, for example ~/bin/{SYMBOL_EXTRACTOR_MAC}");

                throw new Exception(
                    "Expected Mac symbol dumper not found, please 'install' it with xcodebuild " +
                    $"(not found: {SYMBOL_EXTRACTOR_MAC}");
            }

            return new ProcessStartInfo(extractor);
        }

        return new ProcessStartInfo(Path.Join(SYMBOL_EXTRACTOR_PATH_BASE, SYMBOL_EXTRACTOR_LINUX));
    }
}
