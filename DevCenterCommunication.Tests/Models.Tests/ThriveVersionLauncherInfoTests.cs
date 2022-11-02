namespace DevCenterCommunication.Tests.Models.Tests;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DevCenterCommunication.Models;
using SharedBase.Models;
using Xunit;

public class ThriveVersionLauncherInfoTests
{
    [Fact]
    public void ThriveVersion_PlatformsNotEmpty()
    {
        var model = new ThriveVersionLauncherInfo(1, "1.0.0", new Dictionary<PackagePlatform, DownloadableInfo>());

        var validationResult = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), validationResult, true));
        Assert.NotEmpty(validationResult);
        Assert.Contains(nameof(ThriveVersionLauncherInfo.Platforms), validationResult[0].MemberNames);
    }

    [Fact]
    public void ThriveVersion_TooShortVersion()
    {
        var model = new ThriveVersionLauncherInfo(1, "1", new Dictionary<PackagePlatform, DownloadableInfo>
        {
            {
                PackagePlatform.Linux, new DownloadableInfo("1234", "test", new Dictionary<string, Uri>
                {
                    { "test", new Uri("https://example.com") },
                })
            },
        });

        var validationResult = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), validationResult, true));
        Assert.NotEmpty(validationResult);
        Assert.Contains(nameof(ThriveVersionLauncherInfo.ReleaseNumber), validationResult[0].MemberNames);
    }

    [Fact]
    public void ThriveVersion_ValidPassesValidation()
    {
        var model = new ThriveVersionLauncherInfo(1, "1.0.0", new Dictionary<PackagePlatform, DownloadableInfo>
        {
            {
                PackagePlatform.Linux, new DownloadableInfo("1234", "test", new Dictionary<string, Uri>
                {
                    { "test", new Uri("https://example.com") },
                })
            },
        });

        var validationResult = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(model, new ValidationContext(model), validationResult, true));
        Assert.Empty(validationResult);
    }
}
