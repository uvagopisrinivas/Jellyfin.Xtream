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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Xtream.Client;
using Jellyfin.Xtream.Client.Models;
using Jellyfin.Xtream.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A service for dealing with stream information.
/// </summary>
/// <param name="xtreamClient">Instance of the <see cref="IXtreamClient"/> interface.</param>
public partial class StreamService(IXtreamClient xtreamClient)
{
    /// <summary>
    /// The id prefix for VOD category channel items.
    /// </summary>
    public const int VodCategoryPrefix = 0x5d774c35;

    /// <summary>
    /// The id prefix for stream channel items.
    /// </summary>
    public const int StreamPrefix = 0x5d774c36;

    /// <summary>
    /// The id prefix for series category channel items.
    /// </summary>
    public const int SeriesCategoryPrefix = 0x5d774c37;

    /// <summary>
    /// The id prefix for series category channel items.
    /// </summary>
    public const int SeriesPrefix = 0x5d774c38;

    /// <summary>
    /// The id prefix for season channel items.
    /// </summary>
    public const int SeasonPrefix = 0x5d774c39;

    /// <summary>
    /// The id prefix for season channel items.
    /// </summary>
    public const int EpisodePrefix = 0x5d774c3a;

    /// <summary>
    /// The id prefix for catchup channel items.
    /// </summary>
    public const int CatchupPrefix = 0x5d774c3b;

    /// <summary>
    /// The id prefix for catchup stream items.
    /// </summary>
    public const int CatchupStreamPrefix = 0x5d774c3c;

    /// <summary>
    /// The id prefix for media source items.
    /// </summary>
    public const int MediaSourcePrefix = 0x5d774c3d;

    /// <summary>
    /// The id prefix for Live TV items.
    /// </summary>
    public const int LiveTvPrefix = 0x5d774c3e;

    /// <summary>
    /// The id prefix for TV EPG items.
    /// </summary>
    public const int EpgPrefix = 0x5d774c3f;

    private static readonly Regex _tagRegex = TagRegex();

    /// <summary>
    /// Map of common language names and abbreviations to ISO 639-2/B codes used by Jellyfin.
    /// Includes common misspellings found in Xtream provider titles.
    /// </summary>
    private static readonly Dictionary<string, string> LanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "TELUGU", "tel" },
        { "TEL", "tel" },
        { "TELGUE", "tel" },
        { "TAMIL", "tam" },
        { "TAM", "tam" },
        { "HINDI", "hin" },
        { "HIN", "hin" },
        { "ENGLISH", "eng" },
        { "ENG", "eng" },
        { "KANNADA", "kan" },
        { "KAN", "kan" },
        { "KANNDA", "kan" },
        { "MALAYALAM", "mal" },
        { "MAL", "mal" },
        { "MALYALAM", "mal" },
        { "BENGALI", "ben" },
        { "BEN", "ben" },
        { "MARATHI", "mar" },
        { "MAR", "mar" },
        { "GUJARATI", "guj" },
        { "GUJ", "guj" },
        { "PUNJABI", "pan" },
        { "PAN", "pan" },
        { "URDU", "urd" },
        { "URD", "urd" },
        { "ODIA", "ori" },
        { "ORIYA", "ori" },
        { "ORI", "ori" },
        { "ASSAMESE", "asm" },
        { "ASM", "asm" },
        { "SPANISH", "spa" },
        { "SPA", "spa" },
        { "FRENCH", "fre" },
        { "FRE", "fre" },
        { "GERMAN", "ger" },
        { "GER", "ger" },
        { "ITALIAN", "ita" },
        { "ITA", "ita" },
        { "PORTUGUESE", "por" },
        { "POR", "por" },
        { "RUSSIAN", "rus" },
        { "RUS", "rus" },
        { "JAPANESE", "jpn" },
        { "JPN", "jpn" },
        { "KOREAN", "kor" },
        { "KOR", "kor" },
        { "CHINESE", "chi" },
        { "CHI", "chi" },
        { "ARABIC", "ara" },
        { "ARA", "ara" },
        { "THAI", "tha" },
        { "THA", "tha" },
        { "DUTCH", "dut" },
        { "DUT", "dut" },
        { "SWEDISH", "swe" },
        { "SWE", "swe" },
        { "TURKISH", "tur" },
        { "TUR", "tur" },
        { "POLISH", "pol" },
        { "POL", "pol" },
        { "PERSIAN", "per" },
        { "PER", "per" },
        { "HUNGARIAN", "hun" },
        { "HUN", "hun" },
    };

    /// <summary>
    /// Parses tags in the name of a stream entry.
    /// The name commonly contains tags of the forms:
    /// <list>
    /// <item>[TAG]</item>
    /// <item>|TAG|</item>
    /// </list>
    /// These tags are parsed and returned as separate strings.
    /// The returned title is cleaned from tags and trimmed.
    /// </summary>
    /// <param name="name">The name which should be parsed.</param>
    /// <returns>A <see cref="ParsedName"/> struct containing the cleaned title and parsed tags.</returns>
    public static ParsedName ParseName(string name)
    {
        List<string> tags = [];
        string title = _tagRegex.Replace(
            name,
            (match) =>
            {
                for (int i = 1; i < match.Groups.Count; ++i)
                {
                    Group g = match.Groups[i];
                    if (g.Success)
                    {
                        tags.Add(g.Value);
                    }
                }

                return string.Empty;
            });

        // Tag prefixes separated by the a character in the unicode Block Elements range
        int stripLength = 0;
        for (int i = 0; i < title.Length; i++)
        {
            char c = title[i];
            if (c >= '\u2580' && c <= '\u259F')
            {
                tags.Add(title[stripLength..i].Trim());
                stripLength = i + 1;
            }
        }

        return new ParsedName
        {
            Title = title[stripLength..].Trim(),
            Tags = [.. tags],
        };
    }

    private bool IsConfigured(SerializableDictionary<int, HashSet<int>> config, int category, int id)
    {
        return config.TryGetValue(category, out var values) && (values.Count == 0 || values.Contains(id));
    }

    /// <summary>
    /// Gets an async iterator for the configured channels.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<StreamInfo>> GetLiveStreams(CancellationToken cancellationToken)
    {
        PluginConfiguration config = Plugin.Instance.Configuration;

        IEnumerable<StreamInfo> streams = await xtreamClient.GetLiveStreamsAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
        return streams.Where((StreamInfo channel) => channel.CategoryId.HasValue && IsConfigured(config.LiveTv, channel.CategoryId.Value, channel.StreamId));
    }

    /// <summary>
    /// Gets an async iterator for the configured channels after applying the configured overrides.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<StreamInfo>> GetLiveStreamsWithOverrides(CancellationToken cancellationToken)
    {
        PluginConfiguration config = Plugin.Instance.Configuration;
        IEnumerable<StreamInfo> streams = await GetLiveStreams(cancellationToken).ConfigureAwait(false);
        return streams.Select((StreamInfo stream) =>
        {
            if (config.LiveTvOverrides.TryGetValue(stream.StreamId, out ChannelOverrides? overrides))
            {
                stream.Num = overrides.Number ?? stream.Num;
                stream.Name = !string.IsNullOrWhiteSpace(overrides.Name) ? overrides.Name : stream.Name;
                stream.StreamIcon = !string.IsNullOrWhiteSpace(overrides.LogoUrl) ? overrides.LogoUrl : stream.StreamIcon;
            }

            return stream;
        });
    }

    /// <summary>
    /// Gets an channel item info for the category.
    /// </summary>
    /// <param name="prefix">The channel category prefix.</param>
    /// <param name="category">The Xtream category.</param>
    /// <returns>A channel item representing the category.</returns>
    public static ChannelItemInfo CreateChannelItemInfo(int prefix, Category category)
    {
        ParsedName parsedName = ParseName(category.CategoryName);
        return new ChannelItemInfo()
        {
            Id = ToGuid(prefix, category.CategoryId, 0, 0).ToString(),
            Name = category.CategoryName,
            Tags = new List<string>(parsedName.Tags),
            Type = ChannelItemType.Folder,
        };
    }

    /// <summary>
    /// Gets an iterator for the configured VOD categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Category>> GetVodCategories(CancellationToken cancellationToken)
    {
        List<Category> categories = await xtreamClient.GetVodCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
        return categories.Where((Category category) => Plugin.Instance.Configuration.Vod.ContainsKey(category.CategoryId));
    }

    /// <summary>
    /// Gets an iterator for the configured VOD streams.
    /// </summary>
    /// <param name="categoryId">The Xtream id of the category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<StreamInfo>> GetVodStreams(int categoryId, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.Vod.ContainsKey(categoryId))
        {
            return new List<StreamInfo>();
        }

        List<StreamInfo> streams = await xtreamClient.GetVodStreamsByCategoryAsync(Plugin.Instance.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        return streams.Where((StreamInfo stream) => IsConfigured(Plugin.Instance.Configuration.Vod, categoryId, stream.StreamId));
    }

    /// <summary>
    /// Gets an iterator for the configured Series categories.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Category>> GetSeriesCategories(CancellationToken cancellationToken)
    {
        List<Category> categories = await xtreamClient.GetSeriesCategoryAsync(Plugin.Instance.Creds, cancellationToken).ConfigureAwait(false);
        return categories.Where((Category category) => Plugin.Instance.Configuration.Series.ContainsKey(category.CategoryId));
    }

    /// <summary>
    /// Gets an iterator for the configured Series.
    /// </summary>
    /// <param name="categoryId">The Xtream id of the category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Series>> GetSeries(int categoryId, CancellationToken cancellationToken)
    {
        if (!Plugin.Instance.Configuration.Series.ContainsKey(categoryId))
        {
            return new List<Series>();
        }

        List<Series> series = await xtreamClient.GetSeriesByCategoryAsync(Plugin.Instance.Creds, categoryId, cancellationToken).ConfigureAwait(false);
        return series.Where((Series series) => IsConfigured(Plugin.Instance.Configuration.Series, series.CategoryId, series.SeriesId));
    }

    /// <summary>
    /// Gets an iterator for the configured seasons in the Series.
    /// </summary>
    /// <param name="seriesId">The Xtream id of the Series.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Tuple<SeriesStreamInfo, int>>> GetSeasons(int seriesId, CancellationToken cancellationToken)
    {
        SeriesStreamInfo series = await xtreamClient.GetSeriesStreamsBySeriesAsync(Plugin.Instance.Creds, seriesId, cancellationToken).ConfigureAwait(false);
        int categoryId = series.Info.CategoryId;
        if (!IsConfigured(Plugin.Instance.Configuration.Series, categoryId, seriesId))
        {
            return new List<Tuple<SeriesStreamInfo, int>>();
        }

        // Use Seasons list as the source of truth instead of Episodes dictionary keys
        // This prevents seasons from disappearing when the API filters watched episodes
        if (series.Seasons != null && series.Seasons.Count > 0)
        {
            // Convert to list immediately to avoid lazy evaluation issues with TV apps
            return series.Seasons.Select((Season season) => new Tuple<SeriesStreamInfo, int>(series, season.SeasonNumber)).ToList();
        }

        // Fallback to Episodes dictionary keys if Seasons list is empty
        return series.Episodes.Keys.Select((int seasonId) => new Tuple<SeriesStreamInfo, int>(series, seasonId)).ToList();
    }

    /// <summary>
    /// Gets an iterator for the configured seasons in the Series.
    /// </summary>
    /// <param name="seriesId">The Xtream id of the Series.</param>
    /// <param name="seasonId">The Xtream id of the Season.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>IAsyncEnumerable{StreamInfo}.</returns>
    public async Task<IEnumerable<Tuple<SeriesStreamInfo, Season?, Episode>>> GetEpisodes(int seriesId, int seasonId, CancellationToken cancellationToken)
    {
        SeriesStreamInfo series = await xtreamClient.GetSeriesStreamsBySeriesAsync(Plugin.Instance.Creds, seriesId, cancellationToken).ConfigureAwait(false);
        Season? season = series.Seasons?.FirstOrDefault(s => s.SeasonNumber == seasonId);

        // Check if the season exists in the Episodes dictionary before accessing
        if (!series.Episodes.TryGetValue(seasonId, out ICollection<Episode>? episodes) || episodes == null || episodes.Count == 0)
        {
            // Return empty list if season not found instead of crashing
            return new List<Tuple<SeriesStreamInfo, Season?, Episode>>();
        }

        // Convert to list immediately to avoid lazy evaluation issues with TV apps
        return episodes.Select((Episode episode) => new Tuple<SeriesStreamInfo, Season?, Episode>(series, season, episode)).ToList();
    }

    private static void StoreBytes(byte[] dst, int offset, int i)
    {
        byte[] intBytes = BitConverter.GetBytes(i);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(intBytes);
        }

        Buffer.BlockCopy(intBytes, 0, dst, offset, 4);
    }

    /// <summary>
    /// Gets a GUID representing the four 32-bit integers.
    /// </summary>
    /// <param name="i0">Bytes 0-3.</param>
    /// <param name="i1">Bytes 4-7.</param>
    /// <param name="i2">Bytes 8-11.</param>
    /// <param name="i3">Bytes 12-15.</param>
    /// <returns>Guid.</returns>
    public static Guid ToGuid(int i0, int i1, int i2, int i3)
    {
        byte[] guid = new byte[16];
        StoreBytes(guid, 0, i0);
        StoreBytes(guid, 4, i1);
        StoreBytes(guid, 8, i2);
        StoreBytes(guid, 12, i3);
        return new Guid(guid);
    }

    /// <summary>
    /// Gets the four 32-bit integers represented in the GUID.
    /// </summary>
    /// <param name="id">The input GUID.</param>
    /// <param name="i0">Bytes 0-3.</param>
    /// <param name="i1">Bytes 4-7.</param>
    /// <param name="i2">Bytes 8-11.</param>
    /// <param name="i3">Bytes 12-15.</param>
    public static void FromGuid(Guid id, out int i0, out int i1, out int i2, out int i3)
    {
        byte[] tmp = id.ToByteArray();
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(tmp);
            i0 = BitConverter.ToInt32(tmp, 12);
            i1 = BitConverter.ToInt32(tmp, 8);
            i2 = BitConverter.ToInt32(tmp, 4);
            i3 = BitConverter.ToInt32(tmp, 0);
        }
        else
        {
            i0 = BitConverter.ToInt32(tmp, 0);
            i1 = BitConverter.ToInt32(tmp, 4);
            i2 = BitConverter.ToInt32(tmp, 8);
            i3 = BitConverter.ToInt32(tmp, 12);
        }
    }

    /// <summary>
    /// Gets the media source information for the given Xtream stream.
    /// </summary>
    /// <param name="type">The stream media type.</param>
    /// <param name="id">The unique identifier of the stream.</param>
    /// <param name="extension">The container extension of the stream.</param>
    /// <param name="restream">Boolean indicating whether or not restreaming is used.</param>
    /// <param name="start">The datetime representing the start time of catcup TV.</param>
    /// <param name="durationMinutes">The duration in minutes of the catcup TV stream.</param>
    /// <param name="durationSecs">The duration in seconds of the stream (for VOD/Series).</param>
    /// <param name="videoInfo">The Xtream video info if known.</param>
    /// <param name="audioInfo">The Xtream audio info if known.</param>
    /// <param name="name">The display name for the media source.</param>
    /// <returns>The media source info as <see cref="MediaSourceInfo"/> class.</returns>
    public MediaSourceInfo GetMediaSourceInfo(
        StreamType type,
        int id,
        string? extension = null,
        bool restream = false,
        DateTime? start = null,
        int durationMinutes = 0,
        int? durationSecs = null,
        VideoInfo? videoInfo = null,
        AudioInfo? audioInfo = null,
        string? name = null)
    {
        string prefix = string.Empty;
        switch (type)
        {
            case StreamType.Series:
                prefix = "/series";
                break;
            case StreamType.Vod:
                prefix = "/movie";
                break;
        }

        PluginConfiguration config = Plugin.Instance.Configuration;
        string uri = $"{config.BaseUrl}{prefix}/{config.Username}/{config.Password}/{id}";
        if (!string.IsNullOrEmpty(extension))
        {
            uri += $".{extension}";
        }

        if (type == StreamType.CatchUp)
        {
            string? startString = start?.ToString("yyyy'-'MM'-'dd':'HH'-'mm", CultureInfo.InvariantCulture);
            uri = $"{config.BaseUrl}/streaming/timeshift.php?username={config.Username}&password={config.Password}&stream={id}&start={startString}&duration={durationMinutes}";
        }

        bool isLive = type == StreamType.Live;

        List<MediaBrowser.Model.Entities.MediaStream> mediaStreams = [];
        if (videoInfo != null && !string.IsNullOrEmpty(videoInfo.CodecName))
        {
            mediaStreams.Add(new()
            {
                AspectRatio = videoInfo.AspectRatio,
                BitDepth = videoInfo.BitsPerRawSample,
                Codec = videoInfo.CodecName,
                ColorPrimaries = videoInfo.ColorPrimaries,
                ColorRange = videoInfo.ColorRange,
                ColorSpace = videoInfo.ColorSpace,
                ColorTransfer = videoInfo.ColorTransfer,
                Height = videoInfo.Height,
                Index = videoInfo.Index,
                IsAVC = videoInfo.IsAVC,
                IsInterlaced = true,
                Level = videoInfo.Level,
                PixelFormat = videoInfo.PixelFormat,
                Profile = videoInfo.Profile,
                Type = MediaStreamType.Video,
                Width = videoInfo.Width,
            });
        }

        if (!isLive && !string.IsNullOrWhiteSpace(name))
        {
            // Parse language names from the stream title (e.g. "Movie Telugu + Tamil + Hindi + Eng")
            // and create an audio MediaStream per language so the player shows track selection.
            var languages = ParseLanguagesFromName(name);
            if (languages.Count > 0)
            {
                string preferredLang = Plugin.Instance.Configuration.PreferredAudioLanguage;
                int audioIndex = videoInfo != null ? 1 : 0;

                // If preferred language is found, use its position; otherwise default to first track
                bool preferredFound = languages.Any(l => string.Equals(l.Code, preferredLang, StringComparison.OrdinalIgnoreCase));

                foreach (var (langName, isoCode) in languages)
                {
                    bool isDefault = preferredFound
                        ? string.Equals(isoCode, preferredLang, StringComparison.OrdinalIgnoreCase)
                        : audioIndex == (videoInfo != null ? 1 : 0);

                    var stream = new MediaBrowser.Model.Entities.MediaStream()
                    {
                        Codec = audioInfo?.CodecName ?? "aac",
                        Channels = audioInfo?.Channels ?? 2,
                        SampleRate = audioInfo?.SampleRate ?? 48000,
                        Index = audioIndex++,
                        Language = isoCode,
                        Title = langName,
                        Type = MediaStreamType.Audio,
                        IsDefault = isDefault,
                    };

                    if (audioInfo != null)
                    {
                        stream.BitRate = audioInfo.Bitrate;
                        stream.ChannelLayout = audioInfo.ChannelLayout;
                        stream.Profile = audioInfo.Profile;
                    }

                    mediaStreams.Add(stream);
                }
            }
            else if (audioInfo != null && !string.IsNullOrEmpty(audioInfo.CodecName))
            {
                // No languages parsed from name, fall back to single audio track from API
                mediaStreams.Add(new()
                {
                    BitRate = audioInfo.Bitrate,
                    ChannelLayout = audioInfo.ChannelLayout,
                    Channels = audioInfo.Channels,
                    Codec = audioInfo.CodecName,
                    Index = audioInfo.Index,
                    Profile = audioInfo.Profile,
                    SampleRate = audioInfo.SampleRate,
                    Type = MediaStreamType.Audio,
                });
            }
        }
        else if (audioInfo != null && !string.IsNullOrEmpty(audioInfo.CodecName))
        {
            // Live streams: use single audio track from Xtream API
            mediaStreams.Add(new()
            {
                BitRate = audioInfo.Bitrate,
                ChannelLayout = audioInfo.ChannelLayout,
                Channels = audioInfo.Channels,
                Codec = audioInfo.CodecName,
                Index = audioInfo.Index,
                Profile = audioInfo.Profile,
                SampleRate = audioInfo.SampleRate,
                Type = MediaStreamType.Audio,
            });
        }

        return new MediaSourceInfo()
        {
            Container = extension,
            EncoderProtocol = MediaProtocol.Http,
            Id = ToGuid(MediaSourcePrefix, (int)type, id, 0).ToString(),
            IsInfiniteStream = isLive,
            IsRemote = true,
            RunTimeTicks = durationSecs.HasValue ? durationSecs.Value * TimeSpan.TicksPerSecond : null,
            MediaStreams = mediaStreams,
            Name = !string.IsNullOrWhiteSpace(name) ? name : "default",
            Path = uri,
            Protocol = MediaProtocol.Http,
            RequiresClosing = restream,
            RequiresOpening = restream,
            SupportsDirectPlay = true,
            SupportsDirectStream = true,
            SupportsProbing = true,
        };
    }

    /// <summary>
    /// Parses language names from a stream title.
    /// Handles all common Xtream provider patterns:
    /// <list>
    /// <item>"Telugu + Tamil + Hindi + Eng" (plus-separated)</item>
    /// <item>"(Hindi+Kannada+Malayalam+Telugu+Tamil)" (plus inside parens)</item>
    /// <item>"(Hindi)(Kannada)(Malayalam)(Tamil)(Telugu)" (individual parens)</item>
    /// <item>"[Tam, Tel, Hin, Eng]" (brackets with commas)</item>
    /// <item>"(Tam, Tel, Hin, Eng)" (parens with commas)</item>
    /// <item>"Hindi &amp; English" (ampersand-separated)</item>
    /// <item>"(Telugu) (Tamil) (Hindi)" (spaced individual parens)</item>
    /// </list>
    /// </summary>
    /// <param name="name">The stream name/title.</param>
    /// <returns>A list of (display name, ISO 639-2 code) tuples.</returns>
    private static List<(string Name, string Code)> ParseLanguagesFromName(string name)
    {
        List<(string Name, string Code)> result = [];

        // 1. Try [Lang, Lang, ...] pattern
        var bracketMatch = BracketLanguageRegex().Match(name);
        if (bracketMatch.Success)
        {
            AddLanguagesFromDelimitedString(bracketMatch.Groups[1].Value, result);
        }

        // 2. Try (Lang+Lang) or (Lang, Lang) or (Lang & Lang) — delimiters inside single parens
        if (result.Count == 0)
        {
            var parenMatch = ParenDelimitedLanguageRegex().Match(name);
            if (parenMatch.Success)
            {
                AddLanguagesFromDelimitedString(parenMatch.Groups[1].Value, result);
            }
        }

        // 3. Try (Lang)(Lang)(Lang) or (Lang) (Lang) (Lang) — individual parens
        if (result.Count == 0)
        {
            var matches = SingleParenLanguageRegex().Matches(name);
            if (matches.Count >= 2)
            {
                foreach (Match m in matches)
                {
                    TryAddLanguage(m.Groups[1].Value.Trim(), result);
                }
            }
        }

        // 4. Try trailing "Lang + Lang + Lang" or "Lang & Lang" after year/title
        if (result.Count == 0)
        {
            var trailingMatch = TrailingLanguageRegex().Match(name);
            if (trailingMatch.Success)
            {
                AddLanguagesFromDelimitedString(trailingMatch.Groups[1].Value, result);
            }
        }

        // 5. Fallback: scan for any single language word with word boundary checks
        if (result.Count == 0)
        {
            foreach (var kvp in LanguageMap)
            {
                if (name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    int idx = name.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase);
                    bool startOk = idx == 0 || !char.IsLetterOrDigit(name[idx - 1]);
                    bool endOk = (idx + kvp.Key.Length) >= name.Length || !char.IsLetterOrDigit(name[idx + kvp.Key.Length]);

                    if (startOk && endOk && !result.Any(r => string.Equals(r.Code, kvp.Value, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add((kvp.Key, kvp.Value));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Splits a delimited string on +, &amp;, and comma, then maps each token to a language.
    /// </summary>
    private static void AddLanguagesFromDelimitedString(string input, List<(string Name, string Code)> result)
    {
        string[] parts = input.Split(['+', ',', '&'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            TryAddLanguage(part, result);
        }
    }

    /// <summary>
    /// Tries to add a single language token to the result list, avoiding duplicates.
    /// </summary>
    private static void TryAddLanguage(string token, List<(string Name, string Code)> result)
    {
        string cleaned = token.Trim().Trim('(', ')', '[', ']').Trim();
        if (LanguageMap.TryGetValue(cleaned.ToUpperInvariant(), out string? code)
            && !result.Any(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            result.Add((cleaned, code));
        }
    }

    /// <summary>Matches [content] for bracket-delimited language lists.</summary>
    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketLanguageRegex();

    /// <summary>Matches (content) where content contains +, comma, or &amp; delimiters.</summary>
    [GeneratedRegex(@"\(([^)]*[\+,&][^)]*)\)")]
    private static partial Regex ParenDelimitedLanguageRegex();

    /// <summary>Matches individual (word) tokens for patterns like (Hindi)(Telugu)(Tamil).</summary>
    [GeneratedRegex(@"\((\w+)\)")]
    private static partial Regex SingleParenLanguageRegex();

    /// <summary>Matches trailing language list after closing paren, e.g. ") Telugu + Tamil + Hindi".</summary>
    [GeneratedRegex(@"\)\s+([\w\s\+&,]+)$")]
    private static partial Regex TrailingLanguageRegex();

    [GeneratedRegex(@"\[([^\]]+)\]|\|([^\|]+)\|")]
    private static partial Regex TagRegex();
}
