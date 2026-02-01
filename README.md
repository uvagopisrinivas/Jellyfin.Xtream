# Jellyfin.Xtream
![GitHub Downloads (all assets, all releases)](https://img.shields.io/github/downloads/uvagopisrinivas/Jellyfin.Xtream/total)
![GitHub Downloads (all assets, latest release)](https://img.shields.io/github/downloads/uvagopisrinivas/Jellyfin.Xtream/latest/total)
![GitHub commits since latest release](https://img.shields.io/github/commits-since/uvagopisrinivas/Jellyfin.Xtream/latest)
![Dynamic YAML Badge](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fuvagopisrinivas%2FJellyfin.Xtream%2Frefs%2Fheads%2Fmaster%2Fbuild.yaml&query=targetAbi&label=Jellyfin%20ABI)
![Dynamic YAML Badge](https://img.shields.io/badge/dynamic/yaml?url=https%3A%2F%2Fraw.githubusercontent.com%2Fuvagopisrinivas%2FJellyfin.Xtream%2Frefs%2Fheads%2Fmaster%2Fbuild.yaml&query=framework&label=.NET%20framework)

> **Note:** This is a fork with bug fixes for series episodes disappearing and VOD duration tracking issues. Original project by [Kevinjil](https://github.com/Kevinjil/Jellyfin.Xtream).

The Jellyfin.Xtream plugin can be used to integrate the content provided by an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/) in your [Jellyfin](https://jellyfin.org/) instance.

## Table of Contents

- [Bug Fixes in This Fork](#bug-fixes-in-this-fork)
- [Installation](#installation)
- [Configuration](#configuration)
- [Deployment (Docker)](#deployment-docker)
- [Development](#development)
- [Known Problems](#known-problems)
- [Troubleshooting](#troubleshooting)

## Bug Fixes in This Fork

This fork includes fixes for the following issues:

- **Series Episodes Disappearing**: Fixed episodes disappearing when the last episode of a season is watched
- **All Seasons Marked as Watched**: Fixed incorrect "watched" status showing for all seasons
- **VOD Duration Tracking**: Fixed VOD movies being marked as complete after only a few seconds of playback
- **Season Model Mapping**: Fixed incorrect JSON property mappings causing seasons not to load
- **JSON Deserialization**: Added support for non-standard Xtream providers that return objects instead of arrays
- **Crash Prevention**: Added defensive checks to prevent crashes from missing season data

See [BUGFIX_SUMMARY.md](BUGFIX_SUMMARY.md) for detailed technical information about the fixes.

## Installation

The plugin can be installed using a custom plugin repository.
To add the repository, follow these steps:

1. Open your admin dashboard and navigate to `Plugins`.
1. Select the `Repositories` tab on the top of the page.
1. Click the `+` symbol to add a repository.
1. Enter `Jellyfin.Xtream` as the repository name.
1. Enter `https://uvagopisrinivas.github.io/Jellyfin.Xtream/repository.json` as the repository url (or use the original: [`https://kevinjil.github.io/Jellyfin.Xtream/repository.json`](https://kevinjil.github.io/Jellyfin.Xtream/repository.json)).
1. Click save.

**Alternative:** Download the latest release directly from the [Releases page](https://github.com/uvagopisrinivas/Jellyfin.Xtream/releases) and manually install the DLL.

To install or update the plugin, follow these steps:

1. Open your admin dashboard and navigate to `Plugins`.
1. Select the `Catalog` tab on the top of the page.
1. Under `Live TV`, select `Jellyfin Xtream`.
1. (Optional) Select the desired plugin version.
1. Click `Install`.
1. Restart your Jellyfin server to complete the installation.

## Configuration

The plugin requires connection information for an [Xtream-compatible API](https://xtream-ui.org/api-xtreamui-xtreamcode/).
The following credentials should be set correctly in the `Credentials` plugin configuration tab on the admin dashboard.

| Property | Description                                                                               |
| -------- | ----------------------------------------------------------------------------------------- |
| Base URL | The URL of the API endpoint excluding the trailing slash, including protocol (http/https) |
| Username | The username used to authenticate to the API                                              |
| Password | The password used to authenticate to the API                                              |

### Live TV

1. Open the `Live TV` configuration tab.
1. Select the categories, or individual channels within categories, you want to be available.
1. Click `Save` on the bottom of the page.
1. Open the `TV Overrides` configuration tab.
1. Modify the channel numbers, names, and icons if desired.
1. Click `Save` on the bottom of the page.

### Video On-Demand

1. Open the `Video On-Demand` configuration tab.
1. Enable `Show this channel to users`.
1. Select the categories, or individual videos within categories, you want to be available.
1. Click `Save` on the bottom of the page.

### Series

1. Open the `Series` configuration tab.
1. Enable `Show this channel to users`.
1. Select the categories, or individual series within categories, you want to be available.
1. Click `Save` on the bottom of the page.

### TV Catchup
1. Open the `Live TV` configuration tab.
1. Enable `Show the catch-up channel to users`.
1. Click `Save` on the bottom of the page.

## Deployment (Docker)

If you're running Jellyfin in Docker, follow these steps to deploy the plugin:

### Prerequisites

- SSH access to your server
- Jellyfin running in Docker container
- Plugin directory mounted as volume

### Quick Deployment

```bash
# Set version
VERSION="0.8.5"

# Download and deploy
cd /tmp
wget https://github.com/uvagopisrinivas/Jellyfin.Xtream/releases/download/v${VERSION}/jellyfin-xtream-v${VERSION}.zip
python3 -m zipfile -e jellyfin-xtream-v${VERSION}.zip .

# Copy to plugin directory (adjust path to match your setup)
cp Jellyfin.Xtream.dll /path/to/jellyfin/config/plugins/Jellyfin.Xtream_5d774c35-8567-46d3-a950-9bb8227a0c5d/

# Restart Jellyfin container
docker restart jellyfin

# Cleanup
rm Jellyfin.Xtream.dll jellyfin-xtream-v${VERSION}.zip
```

### Finding Your Plugin Directory

Your plugin directory depends on your Docker volume mapping. Common locations:

```bash
# Check your container's volume mounts
docker inspect your-jellyfin-container | grep -A 10 Mounts

# Common paths:
# /config/plugins/
# /var/lib/jellyfin/plugins/
# /MediaServer/jellyfin/config/plugins/
```

### Verify Installation

1. Open Jellyfin Dashboard → Plugins
2. Check that "Jellyfin Xtream" shows the correct version
3. Check logs: `docker logs your-jellyfin-container --tail 100`

## Development

### Building from Source

**Requirements:**
- .NET 9.0 SDK
- Git

**Build Steps:**

```bash
# Clone repository
git clone https://github.com/uvagopisrinivas/Jellyfin.Xtream.git
cd Jellyfin.Xtream

# Build
dotnet build Jellyfin.Xtream.sln --configuration Release

# Output DLL location
# Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll
```

### Testing API Responses

Use the included test script to verify Xtream API connectivity:

```bash
# Edit test_xtream_api.sh and add your credentials
./test_xtream_api.sh

# Or test manually
curl "http://your-provider:port/player_api.php?username=USER&password=PASS&action=get_series_categories"
```

### Version Management

Update version in three files before release:

1. `build.yaml` - `version: "0.8.X.0"`
2. `Jellyfin.Xtream/Jellyfin.Xtream.csproj` - `<AssemblyVersion>` and `<FileVersion>`
3. Rebuild and create release package

### Creating a Release

```bash
# Build
dotnet build Jellyfin.Xtream.sln --configuration Release

# Package
zip -j jellyfin-xtream-v0.8.X.zip Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll

# Commit and tag
git add -A
git commit -m "Version 0.8.X - Description"
git tag v0.8.X
git push origin master
git push origin v0.8.X

# Create GitHub release and upload zip file
```

## Known problems

### Loss of confidentiality

Jellyfin publishes the remote paths in the API and in the default user interface.
As the Xtream format for remote paths includes the username and password, anyone that can access the library will have access to your credentials.
Use this plugin with caution on shared servers.

## Troubleshooting

### Networking Configuration

Make sure you have correctly configured your [Jellyfin networking](https://jellyfin.org/docs/general/networking/):

1. Open your admin dashboard and navigate to `Networking`.
2. Correctly configure your `Published server URIs`.
   For example: `all=https://jellyfin.example.com`

### Plugin Not Loading

```bash
# Check if plugin file exists
ls -la /path/to/jellyfin/config/plugins/Jellyfin.Xtream_*/

# Check Jellyfin logs
docker logs your-jellyfin-container --tail 200 | grep -i "xtream\|plugin"

# Verify permissions (if needed)
chown -R jellyfin:jellyfin /path/to/jellyfin/config/plugins/
```

### Series Episodes Not Showing

1. Verify series is selected in plugin configuration (Dashboard → Plugins → Jellyfin Xtream → Series tab)
2. Check provider credentials are correct
3. Force library refresh: Dashboard → Libraries → Scan Library
4. Check logs for errors: `docker logs your-jellyfin-container -f`

### JSON Deserialization Errors

If you see errors like:
```
Cannot deserialize the current JSON object into type 'System.Collections.Generic.List'
```

This fork includes fixes for non-standard Xtream providers. Make sure you're using the latest version (v0.8.3+).

### VOD Duration Issues

If VOD movies are marked as complete after a few seconds:
- Ensure you're using v0.8.2 or later
- Check that the provider returns duration information in the API
- Verify `RunTimeTicks` is set in logs

### Rollback to Previous Version

```bash
# Download previous version
wget https://github.com/uvagopisrinivas/Jellyfin.Xtream/releases/download/v0.8.X/jellyfin-xtream-v0.8.X.zip

# Extract and deploy
python3 -m zipfile -e jellyfin-xtream-v0.8.X.zip .
cp Jellyfin.Xtream.dll /path/to/plugins/Jellyfin.Xtream_5d774c35-8567-46d3-a950-9bb8227a0c5d/
docker restart your-jellyfin-container
```

## Version History

- **v0.8.5** - Fixed Season model mapping and series episode access
- **v0.8.4** - Fixed series seasons filtering (regression fix)
- **v0.8.3** - Fixed JSON deserialization for non-standard providers
- **v0.8.2** - Series episodes and VOD duration fixes
- **v0.8.1** - User agent updates and account expiry fixes

## Technical Details

### Issues Fixed

#### Series Episodes Disappearing
**Problem:** Episodes disappeared when the last episode of a season was watched.

**Root Cause:** The `GetSeasons()` method used `series.Episodes.Keys` to determine seasons. When the Xtream API filtered watched episodes, the Episodes dictionary lost keys, causing seasons to disappear.

**Solution:** Changed to use `series.Seasons` list as primary source, with Episodes.Keys as fallback. Fixed Season model JSON mapping (`SeasonId` → `Id`, `Cast` → `SeasonNumber`).

#### VOD Duration Tracking
**Problem:** VOD movies marked as complete after only a few seconds of playback.

**Root Cause:** `RunTimeTicks` was not set on `ChannelItemInfo` and `MediaSourceInfo`, causing Jellyfin to treat videos as 0 seconds long.

**Solution:** 
- Added `durationSecs` parameter to `GetMediaSourceInfo()`
- Modified `VodChannel` to fetch detailed VOD info including duration
- Set `RunTimeTicks` on both `ChannelItemInfo` and `MediaSourceInfo`

#### JSON Deserialization
**Problem:** Some Xtream providers return objects instead of arrays, causing deserialization errors.

**Solution:** Added `ObjectOrArrayConverter` to handle both formats gracefully.

#### Crash Prevention
**Problem:** Direct dictionary access could throw `KeyNotFoundException`.

**Solution:** Added `TryGetValue()` checks before accessing Episodes dictionary.

### Code Changes Summary

**StreamService.cs:**
- `GetSeasons()`: Uses Seasons list with SeasonNumber, falls back to Episodes.Keys
- `GetEpisodes()`: Added TryGetValue check for safe dictionary access
- `GetMediaSourceInfo()`: Added durationSecs parameter and RunTimeTicks setting

**VodChannel.cs:**
- Added IXtreamClient dependency injection
- Fetches detailed VOD info to get duration
- Sets RunTimeTicks on ChannelItemInfo

**SeriesChannel.cs:**
- Passes episode duration to GetMediaSourceInfo()
- Updated Season lookup to use SeasonNumber

**Season.cs:**
- Fixed JSON property mappings (id → Id, season_number → SeasonNumber)

**XtreamClient.cs:**
- Added ObjectOrArrayConverter for flexible JSON parsing

## Support & Contributing

- **Issues**: [GitHub Issues](https://github.com/uvagopisrinivas/Jellyfin.Xtream/issues)
- **Original Project**: [Kevinjil/Jellyfin.Xtream](https://github.com/Kevinjil/Jellyfin.Xtream)
- **Pull Requests**: Contributions welcome!

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.
