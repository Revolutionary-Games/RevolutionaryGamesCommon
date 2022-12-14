namespace DevCenterCommunication.Tests.Models.Tests;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DevCenterCommunication.Models;
using RecursiveDataAnnotationsValidation;
using SharedBase.Models;
using Xunit;

public class ThriveVersionLauncherInfoTests
{
    [Fact]
    public void ThriveVersion_PlatformsNotEmpty()
    {
        var model = new ThriveVersionLauncherInfo(1, "1.0.0", new Dictionary<PackagePlatform, DownloadableInfo>());

        var validationResult = new List<ValidationResult>();
        var validator = new RecursiveDataAnnotationValidator();

        Assert.False(validator.TryValidateObjectRecursive(model, new ValidationContext(model), validationResult));
        Assert.NotEmpty(validationResult);
        Assert.Contains(nameof(ThriveVersionLauncherInfo.Platforms), validationResult[0].MemberNames);
    }

    [Fact]
    public void ThriveVersion_TooShortVersion()
    {
        var model = new ThriveVersionLauncherInfo(1, "1", new Dictionary<PackagePlatform, DownloadableInfo>
        {
            {
                PackagePlatform.Linux, new DownloadableInfo("12345678910", "test", new Dictionary<string, Uri>
                {
                    { "test", new Uri("https://example.com") },
                })
            },
        });

        var validationResult = new List<ValidationResult>();
        var validator = new RecursiveDataAnnotationValidator();

        Assert.False(validator.TryValidateObjectRecursive(model, new ValidationContext(model), validationResult));
        Assert.NotEmpty(validationResult);
        Assert.Contains(nameof(ThriveVersionLauncherInfo.ReleaseNumber), validationResult[0].MemberNames);
    }

    [Fact]
    public void ThriveVersion_ValidPassesValidation()
    {
        var model = new ThriveVersionLauncherInfo(1, "1.0.0", new Dictionary<PackagePlatform, DownloadableInfo>
        {
            {
                PackagePlatform.Linux, new DownloadableInfo("12345678910", "test", new Dictionary<string, Uri>
                {
                    { "test", new Uri("https://example.com") },
                })
            },
        });

        var validationResult = new List<ValidationResult>();
        var validator = new RecursiveDataAnnotationValidator();

        Assert.True(validator.TryValidateObjectRecursive(model, new ValidationContext(model), validationResult));
        Assert.Empty(validationResult);
    }
}
