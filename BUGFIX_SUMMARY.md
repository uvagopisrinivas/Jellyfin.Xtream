# Series Episode and VOD Duration Bug Fixes

## Issues Fixed

### Issue #1: Zero Episodes in Single-Season Series
**Problem:** When the last episode of a single-season series is watched, the season folder becomes empty and episodes disappear for all users.

**Root Cause:** The `GetSeasons()` method was using `series.Episodes.Keys` to determine which seasons exist. When the Xtream API filters out watched episodes, the Episodes dictionary loses keys, causing seasons to disappear.

**Fix:** Changed `GetSeasons()` to use the `series.Seasons` list as the primary source of truth, with Episodes dictionary keys as a fallback.

### Issue #2: All Seasons Marked as Watched
**Problem:** When the first season of a multi-season series is completed, all seasons appear as watched and show zero episodes.

**Root Cause:** Same as Issue #1 - the Episodes dictionary was being used to determine available seasons, and the API was filtering episodes based on watched status.

**Fix:** Same fix as Issue #1 - using the Seasons list ensures all seasons remain visible regardless of watched status.

### Issue #3: Crashes on Missing Season Data
**Problem:** Direct dictionary access `series.Episodes[seasonId]` could throw `KeyNotFoundException` when the season key doesn't exist.

**Root Cause:** No defensive programming - the code assumed the Episodes dictionary always contains all season keys.

**Fix:** Added `TryGetValue()` check before accessing the Episodes dictionary, returning an empty list gracefully if the season is not found.

### Issue #4: VOD Movies Marked as Complete After Few Seconds
**Problem:** When playing a VOD movie, even after watching only a few seconds, Jellyfin marks it as "complete" and finished watching.

**Root Cause:** The `VodChannel.CreateChannelItemInfo()` method was not setting `RunTimeTicks` on the `ChannelItemInfo`, and the `MediaSourceInfo` also didn't include duration. Without duration information, Jellyfin treats the video as 0 seconds long, so any playback progress marks it as 100% complete.

**Fix:** 
1. Added `durationSecs` parameter to `GetMediaSourceInfo()` method
2. Set `RunTimeTicks` in `MediaSourceInfo` based on duration in seconds
3. Modified `VodChannel.CreateChannelItemInfo()` to fetch detailed VOD info (including duration) from the API
4. Set `RunTimeTicks` on the `ChannelItemInfo` for VOD items
5. Updated `SeriesChannel` to pass episode duration to `GetMediaSourceInfo()`

## Code Changes

### File: `Jellyfin.Xtream/Service/StreamService.cs`

#### Method: `GetSeasons()`
**Before:**
```csharp
return series.Episodes.Keys.Select((int seasonId) => new Tuple<SeriesStreamInfo, int>(series, seasonId));
```

**After:**
```csharp
// Use Seasons list as the source of truth instead of Episodes dictionary keys
// This prevents seasons from disappearing when the API filters watched episodes
if (series.Seasons != null && series.Seasons.Count > 0)
{
    return series.Seasons.Select((Season season) => new Tuple<SeriesStreamInfo, int>(series, season.SeasonId));
}

// Fallback to Episodes dictionary keys if Seasons list is empty
return series.Episodes.Keys.Select((int seasonId) => new Tuple<SeriesStreamInfo, int>(series, seasonId));
```

#### Method: `GetEpisodes()`
**Before:**
```csharp
return series.Episodes[seasonId].Select((Episode episode) => new Tuple<SeriesStreamInfo, Season?, Episode>(series, season, episode));
```

**After:**
```csharp
// Check if the season exists in the Episodes dictionary before accessing
if (!series.Episodes.TryGetValue(seasonId, out ICollection<Episode>? episodes) || episodes == null)
{
    // Return empty list if season not found instead of crashing
    return new List<Tuple<SeriesStreamInfo, Season?, Episode>>();
}

return episodes.Select((Episode episode) => new Tuple<SeriesStreamInfo, Season?, Episode>(series, season, episode));
```

#### Method: `GetMediaSourceInfo()` - Added Duration Support
**Before:**
```csharp
public MediaSourceInfo GetMediaSourceInfo(
    StreamType type,
    int id,
    string? extension = null,
    bool restream = false,
    DateTime? start = null,
    int durationMinutes = 0,
    VideoInfo? videoInfo = null,
    AudioInfo? audioInfo = null)
{
    // ...
    return new MediaSourceInfo()
    {
        Container = extension,
        // ... other properties
        IsRemote = true,
        MediaStreams = [...]
    };
}
```

**After:**
```csharp
public MediaSourceInfo GetMediaSourceInfo(
    StreamType type,
    int id,
    string? extension = null,
    bool restream = false,
    DateTime? start = null,
    int durationMinutes = 0,
    int? durationSecs = null,  // NEW PARAMETER
    VideoInfo? videoInfo = null,
    AudioInfo? audioInfo = null)
{
    // ...
    return new MediaSourceInfo()
    {
        Container = extension,
        // ... other properties
        IsRemote = true,
        RunTimeTicks = durationSecs.HasValue ? durationSecs.Value * TimeSpan.TicksPerSecond : null,  // NEW
        MediaStreams = [...]
    };
}
```

### File: `Jellyfin.Xtream/VodChannel.cs`

#### Constructor - Added IXtreamClient Dependency
**Before:**
```csharp
public class VodChannel(ILogger<VodChannel> logger) : IChannel, IDisableMediaSourceDisplay
```

**After:**
```csharp
public class VodChannel(ILogger<VodChannel> logger, IXtreamClient xtreamClient) : IChannel, IDisableMediaSourceDisplay
```

#### Method: `CreateChannelItemInfo()` - Fetch Duration
**Before:**
```csharp
private Task<ChannelItemInfo> CreateChannelItemInfo(StreamInfo stream)
{
    // ...
    List<MediaSourceInfo> sources =
    [
        Plugin.Instance.StreamService.GetMediaSourceInfo(
            StreamType.Vod,
            stream.StreamId,
            stream.ContainerExtension)
    ];

    ChannelItemInfo result = new ChannelItemInfo()
    {
        // ... properties without RunTimeTicks
    };

    return Task.FromResult(result);
}
```

**After:**
```csharp
private async Task<ChannelItemInfo> CreateChannelItemInfo(StreamInfo stream)
{
    // ...
    
    // Fetch detailed VOD info to get duration and other metadata
    VodStreamInfo? vodInfo = null;
    try
    {
        vodInfo = await xtreamClient.GetVodInfoAsync(Plugin.Instance.Creds, stream.StreamId, CancellationToken.None).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to fetch VOD info for stream {StreamId}", stream.StreamId);
    }

    List<MediaSourceInfo> sources =
    [
        Plugin.Instance.StreamService.GetMediaSourceInfo(
            StreamType.Vod,
            stream.StreamId,
            stream.ContainerExtension,
            durationSecs: vodInfo?.Info?.DurationSecs,
            videoInfo: vodInfo?.Info?.Video,
            audioInfo: vodInfo?.Info?.Audio)
    ];

    ChannelItemInfo result = new ChannelItemInfo()
    {
        // ... other properties
        RunTimeTicks = vodInfo?.Info?.DurationSecs * TimeSpan.TicksPerSecond,  // NEW
        // ...
    };

    return result;
}
```

### File: `Jellyfin.Xtream/SeriesChannel.cs`

#### Method: `CreateChannelItemInfo()` - Pass Duration
**Before:**
```csharp
List<MediaSourceInfo> sources =
[
    Plugin.Instance.StreamService.GetMediaSourceInfo(
        StreamType.Series,
        episode.EpisodeId,
        episode.ContainerExtension,
        videoInfo: episode.Info?.Video,
        audioInfo: episode.Info?.Audio)
];
```

**After:**
```csharp
List<MediaSourceInfo> sources =
[
    Plugin.Instance.StreamService.GetMediaSourceInfo(
        StreamType.Series,
        episode.EpisodeId,
        episode.ContainerExtension,
        durationSecs: episode.Info?.DurationSecs,  // NEW
        videoInfo: episode.Info?.Video,
        audioInfo: episode.Info?.Audio)
];
```

## Why This Fixes the Issues

### Series Issues (1-3):
1. **Seasons remain visible:** By using the `Seasons` list instead of `Episodes.Keys`, seasons are always displayed even when the API filters episodes based on watched status.

2. **No crashes:** The `TryGetValue()` check prevents `KeyNotFoundException` and handles missing data gracefully.

3. **Works across all platforms:** The fix addresses the root cause, so it works consistently on TV apps, phones, and browsers.

4. **Backward compatible:** The fallback to `Episodes.Keys` ensures the code still works if the Seasons list is empty (for older API responses).

### VOD Issue (4):
1. **Proper duration tracking:** By setting `RunTimeTicks` on both `ChannelItemInfo` and `MediaSourceInfo`, Jellyfin now knows the actual video duration.

2. **Accurate playback progress:** With correct duration, Jellyfin can calculate playback percentage accurately (e.g., 5 minutes watched out of 120 minutes = 4% complete, not 100%).

3. **Better metadata:** Fetching detailed VOD info also provides video/audio codec information for better playback compatibility.

4. **Graceful fallback:** If fetching VOD info fails, the video still plays but without duration information (same as before).

## Performance Considerations

**VOD Channel Loading:**
- The fix adds an API call per VOD item to fetch detailed info
- This may slow down initial VOD category browsing
- Consider implementing caching if performance becomes an issue
- Alternative: Jellyfin's metadata provider already fetches this info, so duration may be populated later

## Testing Recommendations

### Series Testing:
1. **Single-season series:** Watch all episodes and verify the season remains visible
2. **Multi-season series:** Complete the first season and verify other seasons remain accessible
3. **TV apps:** Test on Google TV, Apple TV, and Firestick to ensure episodes load correctly
4. **Mark as unplayed:** Verify the workaround still works (marking as unplayed on phone/browser)

### VOD Testing:
1. **Short playback:** Play a VOD movie for 10-30 seconds, stop, and verify it's NOT marked as complete
2. **Resume playback:** Verify you can resume from where you stopped
3. **Progress tracking:** Check that the progress bar shows correct percentage
4. **Complete playback:** Watch to the end and verify it's marked as complete
5. **Different durations:** Test with short videos (< 30 min) and long movies (> 2 hours)

## Build Requirements

- .NET 9.0 SDK
- Jellyfin.Controller 10.11.0
- Jellyfin.Model 10.11.0

## Build Command

```bash
dotnet build Jellyfin.Xtream.sln --configuration Release
```

The output DLL will be in: `Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll`
