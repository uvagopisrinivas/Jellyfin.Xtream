// Copyright (C) 2022  Kevin Jilissen

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Providers;
using Jellyfin.Xtream.Service;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream;

/// <summary>
/// The Xtream Codes API channel.
/// </summary>
/// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
public class VodChannel(ILogger<VodChannel> logger) : IChannel, IDisableMediaSourceDisplay
{
    /// <inheritdoc />
    public string? Name => "Xtream Video On-Demand";

    /// <inheritdoc />
    public string? Description => "Video On-Demand streamed from the Xtream-compatible server.";

    /// <inheritdoc />
    public string DataVersion => Plugin.Instance.DataVersion;

    /// <inheritdoc />
    public string HomePageUrl => string.Empty;

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new()
        {
            ContentTypes = [
                ChannelMediaContentType.Movie,
            ],
            MediaTypes = [
                ChannelMediaType.Video
            ],
        };
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        switch (type)
        {
            default:
                throw new ArgumentException("Unsupported image type: " + type);
        }
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages()
    {
        return new List<ImageType>
        {
            // ImageType.Primary
        };
    }

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(query.FolderId))
            {
                return await GetCategories(cancellationToken).ConfigureAwait(false);
            }

            Guid guid = Guid.Parse(query.FolderId);
            StreamService.FromGuid(guid, out int prefix, out int categoryId, out int _, out int _);
            if (prefix == StreamService.VodCategoryPrefix)
            {
                return await GetStreams(categoryId, cancellationToken).ConfigureAwait(false);
            }

            return new ChannelItemResult()
            {
                TotalRecordCount = 0,
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get channel items");
            return new ChannelItemResult()
            {
                TotalRecordCount = 0,
            };
        }
    }

    private static ChannelItemInfo CreateChannelItemInfo(StreamInfo stream)
    {
        ParsedName parsedName = StreamService.ParseName(stream.Name);

        DateTime? dateCreated = null;
        if (!string.IsNullOrWhiteSpace(stream.Added) && long.TryParse(stream.Added, CultureInfo.InvariantCulture, out long added) && added > 0)
        {
            dateCreated = DateTimeOffset.FromUnixTimeSeconds(added).DateTime;
        }

        // Build media source, enriching with cached video/audio info from metadata refresh
        // when available. Detailed VOD info (duration, TMDB ID, audio/video codec details) is
        // fetched by XtreamVodProvider during scheduled metadata refresh.
        VideoInfo? videoInfo = null;
        AudioInfo? audioInfo = null;
        int? durationSecs = null;

        if (Plugin.Instance.VodInfoCache.TryGetValue(stream.StreamId, out VodStreamInfo? vodInfo)
            && vodInfo.Info is VodInfo info)
        {
            videoInfo = info.Video;
            audioInfo = info.Audio;
            durationSecs = info.DurationSecs;
        }

        List<MediaSourceInfo> sources =
        [
            Plugin.Instance.StreamService.GetMediaSourceInfo(
                StreamType.Vod,
                stream.StreamId,
                stream.ContainerExtension,
                durationSecs: durationSecs,
                videoInfo: videoInfo,
                audioInfo: audioInfo,
                name: stream.Name)
        ];

        string? imageUrl = RewriteImageUrl(stream.StreamIcon);

        return new ChannelItemInfo()
        {
            ContentType = ChannelMediaContentType.Movie,
            DateCreated = dateCreated,
            Id = $"{StreamService.StreamPrefix}{stream.StreamId}",
            ImageUrl = imageUrl,
            IsLiveStream = false,
            MediaSources = sources,
            MediaType = ChannelMediaType.Video,
            Name = parsedName.Title,
            Tags = new List<string>(parsedName.Tags),
            Type = ChannelItemType.Media,
            ProviderIds = { { XtreamVodProvider.ProviderName, stream.StreamId.ToString(CultureInfo.InvariantCulture) } },
        };
    }

    private async Task<ChannelItemResult> GetCategories(CancellationToken cancellationToken)
    {
        IEnumerable<Category> categories = await Plugin.Instance.StreamService.GetVodCategories(cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = [];

        foreach (var category in categories)
        {
            try
            {
                items.Add(StreamService.CreateChannelItemInfo(StreamService.VodCategoryPrefix, category));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping VOD category {CategoryId} due to error", category.CategoryId);
            }
        }

        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    /// <summary>
    /// Rewrites image URLs from the Xtream API to use the configured BaseUrl.
    /// Provider images hosted on the Xtream server use the path /images/{hash}.ext
    /// but the host in the URL may be wrong. External CDN URLs are left unchanged.
    /// </summary>
    private static string? RewriteImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.AbsolutePath.StartsWith("/images/", StringComparison.OrdinalIgnoreCase))
        {
            string baseUrl = Plugin.Instance.Configuration.BaseUrl.TrimEnd('/');
            return $"{baseUrl}{uri.AbsolutePath}";
        }

        return url;
    }

    private async Task<ChannelItemResult> GetStreams(int categoryId, CancellationToken cancellationToken)
    {
        IEnumerable<StreamInfo> streams = await Plugin.Instance.StreamService.GetVodStreams(categoryId, cancellationToken).ConfigureAwait(false);
        List<ChannelItemInfo> items = [];

        foreach (var stream in streams)
        {
            try
            {
                items.Add(CreateChannelItemInfo(stream));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Skipping VOD stream {StreamId} in category {CategoryId} due to error", stream.StreamId, categoryId);
            }
        }

        return new()
        {
            Items = items,
            TotalRecordCount = items.Count
        };
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId)
    {
        return Plugin.Instance.Configuration.IsVodVisible;
    }
}
