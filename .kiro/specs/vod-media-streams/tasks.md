# Implementation Plan: VOD Media Streams

## Overview

Populate MediaStreams for VOD items by caching VodStreamInfo during metadata refresh and using it at browse time. Update SupportsProbing to skip remote FFprobe when track info is available.

## Tasks

- [ ] 1. Add VodInfoCache to Plugin.cs
  - Add `public ConcurrentDictionary<int, VodStreamInfo> VodInfoCache { get; } = new();` property
  - Add required `using` for `System.Collections.Concurrent` and `Jellyfin.Xtream.Client.Models`
  - _Requirements: 1.1_

- [ ] 2. Update XtreamVodProvider to cache VodStreamInfo and handle errors
  - [ ] 2.1 Wrap GetVodInfoAsync in try/catch and cache the result
    - In `FetchCoreAsync`, wrap the `GetVodInfoAsync` call in try/catch
    - On success, store result in `Plugin.Instance.VodInfoCache[id] = vod`
    - On exception, log error with stream ID and return `ItemUpdateType.None`
    - Add info log: "Cached VOD info for stream {Id}: video={HasVideo}, audio={HasAudio}"
    - Add warning log when `vod.Info` is null: "VOD stream {Id}: Info is null, skipping media stream population"
    - _Requirements: 5.1, 5.2, 6.1, 6.4_

- [ ] 3. Update VodChannel.CreateChannelItemInfo to use cached data
  - [ ] 3.1 Look up VodInfoCache and pass video/audio info to GetMediaSourceInfo
    - Before building `MediaSourceInfo`, check `Plugin.Instance.VodInfoCache.TryGetValue(stream.StreamId, ...)`
    - If cache hit with non-null Info, extract `videoInfo`, `audioInfo`, and `durationSecs`
    - Pass these to `GetMediaSourceInfo()` along with existing `stream.Name` and `stream.ContainerExtension`
    - Add debug log on cache hit: "VOD stream {StreamId}: using cached video/audio info"
    - Add debug log on cache miss: "VOD stream {StreamId}: no cached info available, media streams will be empty"
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.4, 4.1, 4.2_

- [ ] 4. Update SupportsProbing logic in StreamService.GetMediaSourceInfo
  - [ ] 4.1 Change SupportsProbing to be false when MediaStreams are populated for VOD/Series
    - Replace `bool shouldProbe = isLive ? !hasLanguageTracks : true;`
    - With `bool hasMediaStreams = mediaStreams.Any(s => s.Type == MediaStreamType.Video || s.Type == MediaStreamType.Audio);`
    - And `bool shouldProbe = isLive ? !hasLanguageTracks : !hasMediaStreams;`
    - Add debug log: "MediaSource for stream {id}: {mediaStreams.Count} streams, SupportsProbing={shouldProbe}"
    - _Requirements: 3.1, 3.2_

- [ ] 5. Checkpoint
  - Build the project with `dotnet build` to verify no compile errors
  - Review log output format is consistent with existing logging patterns
  - Ensure all error paths log appropriately and don't crash the refresh pipeline
