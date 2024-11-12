// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.NET.Build.Containers.Logging;
using Microsoft.NET.Build.Containers.Resources;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.NET.Build.Containers.Tasks;

public sealed partial class CreateImageIndex : Microsoft.Build.Utilities.Task, ICancelableTask, IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public void Cancel() => _cancellationTokenSource.Cancel();

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }

    public override bool Execute()
    {
        try
        {
            Task.Run(() => ExecuteAsync(_cancellationTokenSource.Token)).GetAwaiter().GetResult();
        }
        catch (TaskCanceledException ex)
        {
            Log.LogWarningFromException(ex);
        }
        catch (OperationCanceledException ex)
        {
            Log.LogWarningFromException(ex);
        }
        return !Log.HasLoggedErrors;
    }

    internal async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using MSBuildLoggerProvider loggerProvider = new(Log);
        ILoggerFactory msbuildLoggerFactory = new LoggerFactory([loggerProvider]);
        ILogger logger = msbuildLoggerFactory.CreateLogger<CreateImageIndex>();

        DestinationImageReference destinationImageReference = DestinationImageReference.CreateFromSettings(
            Repository,
            ImageTags,
            msbuildLoggerFactory,
            null,
            OutputRegistry,
            LocalRegistry);

        switch (destinationImageReference.Kind)
        {
            case DestinationImageReferenceKind.LocalRegistry:
                return await CreateImageIndexLocally(destinationImageReference.LocalRegistry!, logger, cancellationToken);
            case DestinationImageReferenceKind.RemoteRegistry:
                return await CreateImageIndexRemotely(destinationImageReference.RemoteRegistry!, logger, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException();
        } 
    }

    private async Task<bool> CreateImageIndexLocally(ILocalRegistry localRegistry, ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (LocalRegistry == "Docker")
        {
            Log.LogError(Strings.DockerImageIndexCreationNotSupported);
            return false;
        }

        try
        {
            // Building with Podman
            string[] imageIds = GetImageIds();

            logger.LogInformation(Strings.BuildingImageIndexLocally, GetRepositoryAndTagsString(), string.Join(", ", imageIds));

            // to be able to create manifest locally with Podman, we need to prefix the image ids with containers-storage
            string[] containersStorageImageIds = new string[imageIds.Length];
            for (int i = 0; i < imageIds.Length; i++)
            {
                containersStorageImageIds[i] = $"containers-storage:{imageIds[i]}";
            }

            foreach (var tag in ImageTags)
            {
                // TODO: first remove manifest

                string imageIndexName = $"{Repository}:{tag}";
                await localRegistry.CreateManifestAsync(imageIndexName, containersStorageImageIds, cancellationToken);

                logger.LogInformation(Strings.ContainerBuilder_ImageIndexUploadedToLocalDaemon, imageIndexName, LocalRegistry);
            }
        }
        catch (Exception ex)
        {
            Log.LogErrorFromException(ex);
        }

        return !Log.HasLoggedErrors;
    }

    private string[] GetImageIds()
    {
        var imageIds = new string[GeneratedContainers.Length];

        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            imageIds[i] = GeneratedContainers[i].GetMetadata("ImageId");
            if (string.IsNullOrEmpty(imageIds[i]))
            {
                // TODO: add new error for nly image ids
                Log.LogError(Strings.InvalidImageMetadata, GeneratedContainers[i].ItemSpec);
                break;
            }
        }

        return imageIds;
    }

    private async Task<bool> CreateImageIndexRemotely(Registry registry, ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var images = ParseImages();
        if (Log.HasLoggedErrors)
        {
            return false;
        }

        logger.LogInformation(Strings.BuildingImageIndex, GetRepositoryAndTagsString(), string.Join(", ", images.Select(i => i.ManifestDigest)));

        try
        {
            (string imageIndex, string mediaType) = ImageIndexGenerator.GenerateImageIndex(images);
            GeneratedImageIndex = imageIndex;

            await PushToRemoteRegistry(registry, GeneratedImageIndex, mediaType, logger, cancellationToken);
        }
        catch (ContainerHttpException e)
        {
            if (BuildEngine != null)
            {
                Log.LogErrorFromException(e, true);
            }
        }
        catch (ArgumentException ex)
        {
            Log.LogErrorFromException(ex);
        }

        return !Log.HasLoggedErrors;
    }

    private ImageInfo[] ParseImages()
    {
        var images = new ImageInfo[GeneratedContainers.Length];

        for (int i = 0; i < GeneratedContainers.Length; i++)
        {
            var unparsedImage = GeneratedContainers[i];

            string config = unparsedImage.GetMetadata("Configuration");
            string manifestDigest = unparsedImage.GetMetadata("ManifestDigest");
            string manifest = unparsedImage.GetMetadata("Manifest");
            string manifestMediaType = unparsedImage.GetMetadata("ManifestMediaType");

            if (string.IsNullOrEmpty(config) || string.IsNullOrEmpty(manifestDigest) || string.IsNullOrEmpty(manifest))
            {
                Log.LogError(Strings.InvalidImageMetadata, unparsedImage.ItemSpec);
                break;
            }

            images[i] = new ImageInfo
            {
                Config = config,
                ManifestDigest = manifestDigest,
                Manifest = manifest,
                ManifestMediaType = manifestMediaType
            };
        }

        return images;
    }

    private async Task PushToRemoteRegistry(Registry registry, string manifestList, string mediaType, ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Debug.Assert(ImageTags.Length > 0);
        await registry.PushManifestListAsync(Repository, ImageTags, manifestList, mediaType, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(Strings.ImageIndexUploadedToRegistry, GetRepositoryAndTagsString(), OutputRegistry);
    }

    private string? _repositoryAndTagsString = null;

    private string GetRepositoryAndTagsString()
    {
        _repositoryAndTagsString ??= $"{Repository}:{string.Join(", ", ImageTags)}";
        return _repositoryAndTagsString;
    }
}
