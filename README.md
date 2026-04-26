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
#!/bin/bash
VERSION="0.9.6"
PLUGIN_DIR="/srv/nvme-appdata/configs/jellyfin/config/plugins/Jellyfin.Xtream_5d774c35-8567-46d3-a950-9bb8227a0c5d"

cd /tmp
rm -f Jellyfin.Xtream.dll
wget "https://github.com/uvagopisrinivas/Jellyfin.Xtream/releases/download/v${VERSION}/Jellyfin.Xtream.dll"
docker stop jellyfin
rm -rf "$PLUGIN_DIR"
mkdir -p "$PLUGIN_DIR"
cp /tmp/Jellyfin.Xtream.dll "$PLUGIN_DIR/"
cat > "$PLUGIN_DIR/meta.json" << EOF
{"Name": "Jellyfin Xtream","Guid": "5d774c35-8567-46d3-a950-9bb8227a0c5d","Version": "${VERSION}.0","TargetAbi": "10.11.0.0","Framework": "net9.0","Overview": "Stream content from an Xtream-compatible server.","Description": "Stream Live IPTV, Video On-Demand, and Series from an Xtream-compatible server using this plugin.","Category": "LiveTV","Owner": "uvagopisrinivas"}
EOF
chown -R 1000:100 "$PLUGIN_DIR"
docker start jellyfin
sleep 20
docker logs jellyfin --tail 50 | grep "Jellyfin Xtream"
```

> **Note:** Adjust `PLUGIN_DIR` to match your Docker volume mapping. The `meta.json` is required for Jellyfin to recognize the plugin.

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

Update version in three places before release:

1. `Jellyfin.Xtream/Jellyfin.Xtream.csproj` - `<AssemblyVersion>` and `<FileVersion>`
2. `build.yaml` - `version: "0.9.X.0"`
3. `README.md` - `VERSION="0.9.X"` in deploy script, and add entry to Version History

### Creating a Release

Every release requires updating code, creating a GitHub Release, updating the plugin repository manifest, and deploying to your server. Follow all steps below.

#### Step 1: Update Version Numbers

Update version in three places:

1. `Jellyfin.Xtream/Jellyfin.Xtream.csproj` - `<AssemblyVersion>` and `<FileVersion>`
2. `build.yaml` - `version: "0.9.X.0"`
3. `README.md` - `VERSION="0.9.X"` in deploy script, and add entry to Version History

#### Step 2: Build, Commit, Tag, and Push

```bash
dotnet build -c Release Jellyfin.Xtream/Jellyfin.Xtream.csproj

git add -A
git commit -m "v0.9.X - Description of changes"
git tag v0.9.X
git push origin master --tags
```

#### Step 3: Create GitHub Release

```bash
gh release create v0.9.X Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll \
  --title "v0.9.X" --notes "Description of changes" --latest
```

> **Important:** Pushing a git tag alone does NOT create a GitHub Release.
> You must run `gh release create` (or create it via the GitHub web UI) for
> the release to appear on the Releases page and be marked as "Latest".

#### Step 4: Update repository.json on gh-pages

The plugin manifest at `https://uvagopisrinivas.github.io/Jellyfin.Xtream/repository.json` must be updated for Jellyfin's plugin catalog to see the new version. The `publish.yaml` workflow attempts this automatically, but often fails — always verify and update manually if needed.

```bash
# Get the checksum of the built DLL
shasum -a 256 Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll

# Stash any uncommitted work and switch to gh-pages
git stash
git checkout gh-pages
```

Edit `repository.json` and add a new entry at the **top** of the `versions` array (newest first):

```json
{
  "version": "0.9.X.0",
  "changelog": "Description of changes",
  "targetAbi": "10.11.0.0",
  "sourceUrl": "https://github.com/uvagopisrinivas/Jellyfin.Xtream/releases/download/v0.9.X/Jellyfin.Xtream.dll",
  "checksum": "<sha256 checksum from above>",
  "timestamp": "2026-XX-XXTXX:XX:XXZ"
}
```

Then push and return to master:

```bash
git add repository.json
git commit -m "Update repository.json for v0.9.X"
git push origin gh-pages
git checkout master
git stash pop
```

Verify the manifest is live: `curl https://uvagopisrinivas.github.io/Jellyfin.Xtream/repository.json`

#### Step 5: Deploy to Docker Server

```bash
VERSION="0.9.X"
PLUGIN_DIR="/srv/nvme-appdata/configs/jellyfin/config/plugins/Jellyfin.Xtream_5d774c35-8567-46d3-a950-9bb8227a0c5d"

# Copy DLL to server
scp Jellyfin.Xtream/bin/Release/net9.0/Jellyfin.Xtream.dll yourserver:/tmp/

# On the server:
docker stop jellyfin
rm -rf "$PLUGIN_DIR"
mkdir -p "$PLUGIN_DIR"
cp /tmp/Jellyfin.Xtream.dll "$PLUGIN_DIR/"
cat > "$PLUGIN_DIR/meta.json" << EOF
{"Name": "Jellyfin Xtream","Guid": "5d774c35-8567-46d3-a950-9bb8227a0c5d","Version": "${VERSION}.0","TargetAbi": "10.11.0.0","Framework": "net9.0","Overview": "Stream content from an Xtream-compatible server.","Description": "Stream Live IPTV, Video On-Demand, and Series from an Xtream-compatible server using this plugin.","Category": "LiveTV","Owner": "uvagopisrinivas"}
EOF
chown -R 1000:100 "$PLUGIN_DIR"
docker start jellyfin

# Verify
sleep 20
docker logs jellyfin --tail 50 | grep "Jellyfin Xtream"
```

#### Step 6: Verify

1. Open Jellyfin Dashboard → Plugins → confirm correct version
2. Check Jellyfin plugin catalog shows the new version (may take a few minutes for GitHub Pages cache)
3. Check logs: `docker logs jellyfin --tail 200 | grep -i xtream`

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
VERSION="0.9.0"
PLUGIN_DIR="/srv/nvme-appdata/configs/jellyfin/config/plugins/Jellyfin.Xtream_5d774c35-8567-46d3-a950-9bb8227a0c5d"

cd /tmp
rm -f Jellyfin.Xtream.dll
wget "https://github.com/uvagopisrinivas/Jellyfin.Xtream/releases/download/v${VERSION}/Jellyfin.Xtream.dll"
docker stop jellyfin
rm -rf "$PLUGIN_DIR"
mkdir -p "$PLUGIN_DIR"
cp /tmp/Jellyfin.Xtream.dll "$PLUGIN_DIR/"
cat > "$PLUGIN_DIR/meta.json" << EOF
{"Name": "Jellyfin Xtream","Guid": "5d774c35-8567-46d3-a950-9bb8227a0c5d","Version": "${VERSION}.0","TargetAbi": "10.11.0.0","Framework": "net9.0","Overview": "Stream content from an Xtream-compatible server.","Description": "Stream Live IPTV, Video On-Demand, and Series from an Xtream-compatible server using this plugin.","Category": "LiveTV","Owner": "uvagopisrinivas"}
EOF
chown -R 1000:100 "$PLUGIN_DIR"
docker start jellyfin
```

## Version History

- **v0.9.6** - Parse audio languages from stream titles to populate multi-track audio in player UI. Restored IDisableMediaSourceDisplay to prevent probe errors.
- **v0.9.5** - Enable audio track discovery for VOD/Series: removed IDisableMediaSourceDisplay so Jellyfin probes remote streams and discovers all audio languages. Added diagnostic logging for Xtream API audio info.
- **v0.9.4** - Audio language track discovery: leave MediaStreams empty for VOD/Series so Jellyfin probes all audio tracks.
- **v0.9.3** - Add per-item error handling across all channels to prevent plugin crashes from malformed API data
- **v0.9.2** - Add TMDB provider ID to VOD channel items for better subtitle provider support
- **v0.9.1** - Fix null/empty string handling for images, subtitles, and metadata across all channels
- **v0.9.0** - Allow SDK roll forward to latest major version
- **v0.8.9** - Add ImageUrl and SeriesName to episodes and seasons for TV app compatibility
- **v0.8.6** - Fixed empty seasons on TV apps (lazy evaluation issue)
- **v0.8.5** - Fixed Season model mapping and series episode access
- **v0.8.4** - Fixed series seasons filtering (regression fix)
- **v0.8.3** - Fixed JSON deserialization for non-standard providers
- **v0.8.2** - Series episodes and VOD duration fixes
- **v0.8.1** - User agent updates and account expiry fixes

## Technical Details

### Issues Fixed

#### Empty Seasons on TV Apps (v0.8.6)
**Problem:** Series episodes showed correctly on browser and phone apps, but TV apps (Android TV, etc.) showed empty seasons.

**Root Cause:** Lazy evaluation of IEnumerable - TV apps may not properly enumerate deferred execution results.

**Solution:** Added `.ToList()` calls to force immediate evaluation in `GetSeasons()` and `GetEpisodes()`.

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
