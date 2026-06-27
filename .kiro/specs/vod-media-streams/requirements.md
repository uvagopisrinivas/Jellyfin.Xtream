# Requirements Document

## Introduction

VOD (Video On-Demand) movies in the Jellyfin Xtream plugin currently lack video/audio track information on the detail page. Users cannot see codec details, select audio tracks, or identify available languages before or during playback. The SeriesChannel already populates this data correctly by passing `videoInfo` and `audioInfo` to `GetMediaSourceInfo()`, but VodChannel does not. This feature brings VOD media stream population to parity with Series by leveraging the existing `GetMediaSourceInfo()` infrastructure, and disables unreliable remote FFprobe probing once track info is available.

## Glossary

- **VodChannel**: The Jellyfin channel plugin component that serves VOD movie items to clients during browsing
- **XtreamVodProvider**: The Jellyfin metadata provider that fetches detailed VOD information (plot, duration, TMDB ID, video/audio codec details) from the Xtream API per item during scheduled metadata refresh
- **StreamService**: The shared service class containing `GetMediaSourceInfo()` which builds `MediaSourceInfo` objects including `MediaStreams` from `VideoInfo` and `AudioInfo`
- **MediaStreams**: The list of `MediaStream` objects on a `MediaSourceInfo` that describe video, audio, and subtitle tracks to Jellyfin clients
- **MediaSourceInfo**: The Jellyfin model that describes a playable media source including its URL, container, runtime, and associated MediaStreams
- **VodStreamInfo**: The Xtream API response model for a single VOD item, containing `VodInfo` (with `VideoInfo` and `AudioInfo`) and `StreamInfo` (movie data)
- **GetVodInfoAsync**: The Xtream client API call that retrieves detailed VOD info including video/audio codec details for a single stream
- **ParseLanguagesFromName**: The existing method in `StreamService` that extracts language names from stream titles and maps them to ISO 639-2 codes
- **SupportsProbing**: A flag on `MediaSourceInfo` that tells Jellyfin whether to perform remote FFprobe on the stream URL to discover codecs

## Requirements

### Requirement 1: Pass Video and Audio Info to MediaSourceInfo for VOD

**User Story:** As a Jellyfin user, I want VOD movies to display video and audio track information on the detail page, so that I can see codec details and available audio languages before starting playback.

#### Acceptance Criteria

1. WHEN VodChannel creates a channel item for a VOD stream that has been previously refreshed with video and audio info, THE VodChannel SHALL pass the VideoInfo and AudioInfo from the cached VodStreamInfo to GetMediaSourceInfo
2. WHEN VideoInfo is available and has a non-empty codec name, THE StreamService SHALL produce a video MediaStream containing codec name, resolution (width and height), aspect ratio, color properties, pixel format, profile, and level
3. WHEN AudioInfo is available and has a non-empty codec name, THE StreamService SHALL produce one or more audio MediaStream entries with codec, channels, sample rate, and bitrate
4. WHEN VodStreamInfo contains null or empty VideoInfo, THE StreamService SHALL omit the video MediaStream from the result rather than producing an incomplete entry
5. WHEN VodStreamInfo contains null or empty AudioInfo and no languages are parsed from the stream name, THE StreamService SHALL produce no audio MediaStreams

### Requirement 2: Parse Audio Languages from VOD Stream Names

**User Story:** As a Jellyfin user browsing multi-language VOD content, I want each audio language to appear as a separate selectable track, so that I can choose my preferred language before or during playback.

#### Acceptance Criteria

1. WHEN a VOD stream name contains language identifiers (e.g., "Movie Telugu + Tamil + Hindi"), THE StreamService SHALL create a separate audio MediaStream for each recognized language
2. WHEN multiple audio languages are parsed, THE StreamService SHALL assign sequential stream indices starting after the video track index
3. WHEN the configured PreferredAudioLanguage matches one of the parsed languages, THE StreamService SHALL mark that audio track as the default stream
4. WHEN no language identifiers are found in the stream name and AudioInfo is available, THE StreamService SHALL fall back to a single audio MediaStream using the AudioInfo codec details

### Requirement 3: Disable Probing for VOD When Track Info Is Populated

**User Story:** As a server administrator, I want VOD items with populated track information to skip remote FFprobe probing, so that playback is faster and not blocked by unreliable remote stream probing.

#### Acceptance Criteria

1. WHEN a VOD MediaSourceInfo has a non-empty MediaStreams list containing at least one video or audio track, THE StreamService SHALL set SupportsProbing to false
2. WHEN a VOD MediaSourceInfo has an empty MediaStreams list (no video or audio info was available), THE StreamService SHALL set SupportsProbing to true to allow fallback discovery

### Requirement 4: Set Default Audio Stream Index

**User Story:** As a Jellyfin user, I want the player to automatically select my preferred audio track when starting VOD playback, so that I do not have to manually switch languages each time.

#### Acceptance Criteria

1. WHEN the MediaStreams list contains audio tracks and one matches the configured PreferredAudioLanguage, THE MediaSourceInfo SHALL set DefaultAudioStreamIndex to the index of that preferred track
2. WHEN the MediaStreams list contains audio tracks but none match the preferred language, THE MediaSourceInfo SHALL set DefaultAudioStreamIndex to the index of the first audio track

### Requirement 5: Respect Existing Concurrency Controls

**User Story:** As a server administrator, I want the VOD metadata refresh to respect the VodMaxConcurrency setting, so that the Xtream API is not overwhelmed with concurrent requests.

#### Acceptance Criteria

1. WHILE fetching VOD stream details, THE XtreamVodProvider SHALL acquire the VodMaxConcurrency semaphore before making API calls
2. THE XtreamVodProvider SHALL release the semaphore after the API call completes, regardless of success or failure

### Requirement 6: Graceful Handling of Missing or Malformed API Data

**User Story:** As a Jellyfin user, I want VOD playback to remain functional even when the Xtream API returns incomplete or malformed video/audio data, so that my library is not broken by bad upstream data.

#### Acceptance Criteria

1. IF the GetVodInfoAsync call returns a VodStreamInfo with a null Info property, THEN THE XtreamVodProvider SHALL skip media stream population and continue without error
2. IF the VideoInfo codec name is null or empty, THEN THE StreamService SHALL exclude the video MediaStream from the result
3. IF the AudioInfo codec name is null or empty and no languages are parsed from the name, THEN THE StreamService SHALL produce an empty MediaStreams list for audio
4. IF the GetVodInfoAsync call throws an exception, THEN THE XtreamVodProvider SHALL log the error and continue without crashing the metadata refresh
