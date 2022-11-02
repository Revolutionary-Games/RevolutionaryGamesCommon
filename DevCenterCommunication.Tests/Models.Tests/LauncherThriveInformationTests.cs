namespace DevCenterCommunication.Tests.Models.Tests;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DevCenterCommunication.Models;
using RecursiveDataAnnotationsValidation;
using SharedBase.Models;
using Xunit;

public class LauncherThriveInformationTests
{
    [Fact]
    public void ThriveInformation_RecursiveValidationCanFail()
    {
        var model = new LauncherThriveInformation(new LauncherVersionInfo("1.0.0"), 1,
            new List<ThriveVersionLauncherInfo>
            {
                new(1, "1.0.0", new Dictionary<PackagePlatform, DownloadableInfo>()),
            }, new Dictionary<string, DownloadMirrorInfo>
            {
                { "mirror", new DownloadMirrorInfo(new Uri("https://download.url"), "Example mirror") },
            });

        var validationResult = new List<ValidationResult>();
        var validator = new RecursiveDataAnnotationValidator();

        Assert.False(validator.TryValidateObjectRecursive(model, new ValidationContext(model), validationResult));
        Assert.NotEmpty(validationResult);
        Assert.Contains($"{nameof(LauncherThriveInformation.Versions)}.{nameof(ThriveVersionLauncherInfo.Platforms)}",
            validationResult[0].MemberNames);
    }

    [Fact]
    public void ThriveInformation_PassesValidation()
    {
        var model = new LauncherThriveInformation(new LauncherVersionInfo("1.0.0"), 1,
            new List<ThriveVersionLauncherInfo>
            {
                new(1, "1.0.0", new Dictionary<PackagePlatform, DownloadableInfo>
                {
                    {
                        PackagePlatform.Linux, new DownloadableInfo(
                            "12345678910",
                            "Thrive_1.0.0.0_linux",
                            new Dictionary<string, Uri>
                            {
                                {
                                    "mirror",
                                    new Uri("https://download.url")
                                },
                            })
                    },
                }),
            }, new Dictionary<string, DownloadMirrorInfo>
            {
                { "mirror", new DownloadMirrorInfo(new Uri("https://download.url"), "Example mirror") },
            });

        var validationResult = new List<ValidationResult>();
        var validator = new RecursiveDataAnnotationValidator();

        Assert.True(validator.TryValidateObjectRecursive(model, new ValidationContext(model), validationResult));
        Assert.Empty(validationResult);

        // Just for fun, also test that the inbuilt validation passes
        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), validationResult));
    }
}
