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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Xtream.Service;

/// <summary>
/// A live stream implementation that can be restreamed.
/// </summary>
public class Restream : ILiveStream, IDirectStreamProvider, IDisposable
{
    /// <summary>
    /// The global constant for the restream tuner host.
    /// </summary>
    public const string TunerHost = "Xtream-Restream";

    private static readonly HttpStatusCode[] _redirects = [
        HttpStatusCode.Moved,
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.PermanentRedirect,
        HttpStatusCode.Redirect,
    ];

    private readonly WrappedBufferStream _buffer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _tokenSource;
    private readonly string _url;

    private Task? _copyTask;
    private Stream? _inputStream;
    private bool _closed;

    /// <summary>
    /// Initializes a new instance of the <see cref="Restream"/> class.
    /// </summary>
    /// <param name="appHost">Instance of the <see cref="IServerApplicationHost"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger"/> interface.</param>
    /// <param name="mediaSource">The media which must be restreamed.</param>
    public Restream(IServerApplicationHost appHost, IHttpClientFactory httpClientFactory, ILogger logger, MediaSourceInfo mediaSource)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        MediaSource = mediaSource;

        _buffer = new WrappedBufferStream(16 * 1024 * 1024); // 16MiB
        _tokenSource = new CancellationTokenSource();

        OriginalStreamId = MediaSource.Id;
        UniqueId = Guid.NewGuid().ToString();

        _url = MediaSource.Path;
        string path = $"/LiveTv/LiveStreamFiles/{UniqueId}/stream.ts";
        MediaSource.Path = appHost.GetSmartApiUrl(IPAddress.Any) + path;
        MediaSource.EncoderPath = appHost.GetApiUrlForLocalAccess() + path;
        MediaSource.Protocol = MediaProtocol.Http;
    }

    /// <inheritdoc />
    public int ConsumerCount { get; set; }

    /// <inheritdoc />
    public string OriginalStreamId { get; set; }

    /// <inheritdoc />
    public string TunerHostId => TunerHost;

    /// <inheritdoc />
    public bool EnableStreamSharing => true;

    /// <inheritdoc />
    public MediaSourceInfo MediaSource { get; set; }

    /// <inheritdoc />
    public string UniqueId { get; init; }

    /// <inheritdoc />
    public async Task Open(CancellationToken openCancellationToken)
    {
        if (_copyTask != null)
        {
            _logger.LogWarning("Restream for channel {ChannelId} is already open.", MediaSource.Id);
            return;
        }

        string channelId = MediaSource.Id;
        _logger.LogInformation("Starting restream for channel {ChannelId}.", channelId);

        // Open the first source connection before returning so the buffer starts filling.
        await OpenSourceStream(openCancellationToken).ConfigureAwait(true);

        // Continuously pump the source into the buffer, reconnecting automatically
        // when the provider closes the connection (common for IPTV feeds that
        // terminate long-lived HTTP connections). This keeps live playback going
        // without the client seeing the stream end.
        _copyTask = Task.Run(() => PumpLoopAsync(_tokenSource.Token), CancellationToken.None);
    }

    private async Task OpenSourceStream(CancellationToken cancellationToken)
    {
        string channelId = MediaSource.Id;

        // Response stream is disposed manually.
        HttpResponseMessage response = await _httpClientFactory.CreateClient(NamedClient.Default)
            .GetAsync(_url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(true);
        _logger.LogDebug("Stream for channel {ChannelId} using url {Url}", channelId, _url);

        // Handle a manual redirect in the case of a HTTPS to HTTP downgrade.
        if (_redirects.Contains(response.StatusCode))
        {
            _logger.LogDebug("Stream for channel {ChannelId} redirected to url {Url}", channelId, response.Headers.Location);
            response = await _httpClientFactory.CreateClient(NamedClient.Default)
                .GetAsync(response.Headers.Location, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(true);
        }

        _inputStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task PumpLoopAsync(CancellationToken cancellationToken)
    {
        string channelId = MediaSource.Id;

        // Threshold below which a connection is considered "too short" — indicates
        // the provider is dropping us immediately (e.g. connection-limit contention),
        // not a normal periodic keepalive drop.
        TimeSpan shortConnectionThreshold = TimeSpan.FromSeconds(10);
        int shortConnections = 0;

        while (!cancellationToken.IsCancellationRequested && !_closed)
        {
            DateTime connectionStart = DateTime.UtcNow;
            bool errored = false;

            try
            {
                if (_inputStream == null)
                {
                    await OpenSourceStream(cancellationToken).ConfigureAwait(false);
                }

                // Copy until the source connection ends.
                await _inputStream!.CopyToAsync(_buffer, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Restream source for channel {ChannelId} ended; reconnecting.", channelId);
            }
            catch (OperationCanceledException)
            {
                // Restream is being closed intentionally.
                break;
            }
            catch (Exception ex)
            {
                errored = true;
                _logger.LogWarning(ex, "Restream source for channel {ChannelId} errored; reconnecting.", channelId);
            }
            finally
            {
                _inputStream?.Close();
                _inputStream = null;
            }

            if (cancellationToken.IsCancellationRequested || _closed)
            {
                break;
            }

            // Track short-lived connections. If the source keeps dying within a few
            // seconds, the provider is likely rejecting us (connection limit reached
            // by another viewer/device). Back off progressively instead of hammering.
            TimeSpan connectionDuration = DateTime.UtcNow - connectionStart;
            if (errored || connectionDuration < shortConnectionThreshold)
            {
                shortConnections++;
            }
            else
            {
                shortConnections = 0;
            }

            // Progressive backoff: normal drop = quick reconnect; repeated short
            // connections = longer waits, capped at 5 seconds.
            int delayMs = shortConnections switch
            {
                0 or 1 => 250,
                2 or 3 => 1000,
                4 or 5 => 2500,
                _ => 5000,
            };

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Restream pump loop for channel {ChannelId} exited.", channelId);
    }

    /// <inheritdoc />
    public async Task Close()
    {
        _closed = true;
        await _tokenSource.CancelAsync().ConfigureAwait(false);
        if (_copyTask != null)
        {
            await _copyTask.ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Stream GetStream()
    {
        if (_copyTask == null)
        {
            _logger.LogWarning("Restream for channel {ChannelId} was not opened.", MediaSource.Id);
            _ = Open(CancellationToken.None);
        }

        _logger.LogInformation("Opening restream {Count} for channel {ChannelId}.", ConsumerCount, MediaSource.Id);
        return new WrappedBufferReadStream(_buffer);
    }

    /// <summary>
    /// Disposes the fields.
    /// </summary>
    /// <param name="disposing">Whether or not to dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inputStream?.Dispose();
            _buffer.Dispose();
            _tokenSource.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Implement IDisposable.
        // Do not make this method virtual.
        // A derived class should not be able to override this method.
        Dispose(true);
        // This object will be cleaned up by the Dispose method.
        // Therefore, you should call GC.SuppressFinalize to
        // take this object off the finalization queue
        // and prevent finalization code for this object
        // from executing a second time.
        GC.SuppressFinalize(this);
    }
}
