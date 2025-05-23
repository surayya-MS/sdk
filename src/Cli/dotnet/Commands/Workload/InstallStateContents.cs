// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CS8632

namespace Microsoft.DotNet.Cli.Commands.Workload;

internal class InstallStateContents
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool? UseWorkloadSets { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Manifests { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkloadVersion { get; set; }

    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowTrailingCommas = true,
    };

    public static InstallStateContents FromString(string contents)
    {
        return JsonSerializer.Deserialize<InstallStateContents>(contents, s_options) ?? new InstallStateContents();
    }

    public static InstallStateContents FromPath(string path)
    {
        return File.Exists(path) ? FromString(File.ReadAllText(path)) : new InstallStateContents();
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this, s_options);
    }

    public bool ShouldUseWorkloadSets() => UseWorkloadSets ?? true;
}

#pragma warning restore CS8632
