// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.Tasks;

partial class CreateImageIndex
{
    /// <summary>
    /// Manifests to include in the image index.
    /// </summary>
    [Required]
    public ITaskItem[] GeneratedContainers { get; set; }

    /// <summary>
    /// The registry to push the image index to.
    /// </summary>
    public string OutputRegistry { get; set; }

    /// <summary>
    /// The kind of local registry to use, if any.
    /// </summary>
    public string LocalRegistry { get; set; }

    /// <summary>
    /// The name of the output image index (manifest list) that will be pushed to the registry.
    /// </summary>
    [Required]
    public string Repository { get; set; }

    /// <summary>
    /// The tag to associate with the new image index (manifest list).
    /// </summary>
    [Required]
    public string[] ImageTags { get; set; }

    /// <summary>
    /// The generated image index (manifest list) in JSON format.
    /// </summary>
    /// <remarks>This is not empty string only when <see cref="OutputRegistry"/> is provided.</remarks>
    [Output]
    public string GeneratedImageIndex { get; set; }

    public CreateImageIndex()
    {
        GeneratedContainers = Array.Empty<ITaskItem>();
        OutputRegistry = string.Empty;
        LocalRegistry = string.Empty;
        Repository = string.Empty;
        ImageTags = Array.Empty<string>();
        GeneratedImageIndex = string.Empty;
    }
}
