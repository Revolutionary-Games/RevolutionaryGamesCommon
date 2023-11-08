namespace DevCenterCommunication.Models;

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Enums;
using SharedBase.Models;
using SharedBase.ModelVerifiers;

public class PrecompiledObjectVersionDTO : IIdentifiable
{
    /// <summary>
    ///   Id of the parent precompiled object this version is for
    /// </summary>
    public long OwnedById { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    [MayNotContain("/")]
    public string Version { get; set; } = null!;

    public PackagePlatform Platform { get; set; }

    public PrecompiledTag Tags { get; set; }

    public bool Uploaded { get; set; }

    /// <summary>
    ///   Size in bytes of this object in storage. Needs to be set when starting an upload (so the uploader needs to)
    ///   compress the item locally.
    /// </summary>
    public long Size { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? LastDownload { get; set; }

    public long StoredInItemId { get; set; }

    /// <summary>
    ///   Creator user ID. When accessing without login this is -1
    /// </summary>
    public long? CreatedById { get; set; }

    [JsonIgnore]
    public long Id => (OwnedById + ((long)Platform << 58)) ^ ((long)Tags << 32) ^ (Version.GetHashCode() << 16);
}
