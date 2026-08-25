using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RatnaBay.Client;

/// <summary>
/// What this build has been told it may send, and where.
///
/// Held in one place so the answer to "what leaves this machine" can be read in one screen
/// rather than assembled from call sites.
/// </summary>
public static class Telemetry
{
    /// <summary>
    /// Where recordings go. Empty switches uploading off entirely, build and all.
    ///
    /// A write-only endpoint on a server we run. Self-hosting is not the convenient choice, it
    /// is the private one: every hosted sink sees the tester's IP and promises not to keep it,
    /// whereas here not writing it down is a line of our own code rather than someone's policy.
    /// </summary>
    public const string Endpoint = "https://datathecodie.com/ratnabay/upload.php";

    /// <summary>
    /// A per-build token, sent as a header so a passing scanner cannot fill the disk.
    ///
    /// Not a secret and not treated as one — anything shipped in a binary can be read out of
    /// it. It is a doorbell, not a lock: the endpoint is write-only, rate limited, and there is
    /// nothing in a payload worth stealing. Change it each build so an old one can be retired.
    /// </summary>
    public const string BuildToken = "alpha-2026-08";

    /// <summary>Largest recording that will ever be sent. A run makes a few hundred KB.</summary>
    public const int MaxUploadBytes = 4 * 1024 * 1024;
}

/// <summary>
/// Whether this player has agreed to send recordings, and the anonymous name they go under.
///
/// Asked once, in plain words, before anything is sent. A game that uploads by default and
/// mentions it in a settings menu is technically disclosed and practically not; the tester who
/// finds out later is right to be annoyed, and one annoyed tester in thirty is a bad trade for
/// data nobody needed that badly.
/// </summary>
public sealed class TelemetryConsent
{
    private const string FileName = "telemetry.json";

    public bool Asked { get; set; }
    public bool Allowed { get; set; }

    /// <summary>
    /// A random name for this installation, so ten hesitations by one tester can be told from
    /// one hesitation by ten testers — a distinction that decides what a small sample means.
    ///
    /// Generated with <see cref="Guid.NewGuid"/> and never derived from anything about the
    /// machine. No hardware id, no user name, no path: a fresh install is a new person, and
    /// deleting the file is all it takes to stop being the old one.
    /// </summary>
    public string InstallId { get; set; } = string.Empty;

    public static string Path => System.IO.Path.Combine(GameSession.SaveDirectory, FileName);

    public static TelemetryConsent Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var loaded = JsonSerializer.Deserialize<TelemetryConsent>(File.ReadAllText(Path));
                if (loaded is not null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.InstallId))
                        loaded.InstallId = Guid.NewGuid().ToString("N");

                    return loaded;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException
            or UnauthorizedAccessException)
        {
            // An unreadable preference is treated as no preference, which means asking again
            // rather than assuming permission.
        }

        return new TelemetryConsent { InstallId = Guid.NewGuid().ToString("N") };
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(GameSession.SaveDirectory);
            File.WriteAllText(Path, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Failing to remember the answer is not worth interrupting anybody over. The
            // question is simply asked again next launch.
        }
    }
}

/// <summary>
/// Sends finished recordings, and gets out of the way.
///
/// Three rules, all of them about not being noticed:
///
/// It never touches the game loop. Everything happens on a background task with a static
/// client and a hard timeout, so a server that is down or slow costs nothing on screen.
///
/// It never throws. Every failure path swallows, because a telemetry bug that crashes a
/// playtest destroys the very session it was there to record.
///
/// The disk is the queue. A recording that fails to send keeps no marker and is simply tried
/// again next launch, which means offline testers are handled without any queue code at all.
/// </summary>
public sealed class TelemetryUploader
{
    /// <summary>Written beside a recording once it is safely delivered.</summary>
    private const string SentSuffix = ".sent";

    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        // Long-lived client, but connections recycled so a changed DNS record is picked up.
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private readonly TelemetryConsent _consent;
    private int _running;

    public TelemetryUploader(TelemetryConsent consent) => _consent = consent;

    /// <summary>The last thing that happened, for the settings screen. Never shown mid-run.</summary>
    public string Status { get; private set; } = string.Empty;

    public bool CanSend =>
        _consent.Allowed && !string.IsNullOrWhiteSpace(Telemetry.Endpoint);

    /// <summary>
    /// Send anything not yet sent, in the background.
    ///
    /// Safe to call as often as it is convenient: it returns immediately if a sweep is already
    /// running, and a recording already delivered is skipped by its marker.
    /// </summary>
    public void SendPending()
    {
        if (!CanSend) return;
        if (Interlocked.Exchange(ref _running, 1) == 1) return;

        _ = Task.Run(async () =>
        {
            try { await SweepAsync().ConfigureAwait(false); }
            catch (Exception exception) { Status = $"upload failed: {exception.Message}"; }
            finally { Interlocked.Exchange(ref _running, 0); }
        });
    }

    private async Task SweepAsync()
    {
        var directory = PlayRecorder.Directory;
        if (!Directory.Exists(directory)) return;

        var sent = 0;
        foreach (var path in Directory.GetFiles(directory, "play_*.json"))
        {
            if (File.Exists(path + SentSuffix)) continue;

            var info = new FileInfo(path);
            if (info.Length == 0 || info.Length > Telemetry.MaxUploadBytes) continue;

            // The newest file is the session in progress and is still being written to. Only
            // finished recordings are sent, which is why this runs on launch rather than on
            // the way out.
            if (DateTime.UtcNow - info.LastWriteTimeUtc < TimeSpan.FromMinutes(2)) continue;

            if (await SendAsync(path).ConfigureAwait(false)) sent++;
        }

        Status = sent == 0 ? "nothing new to send" : $"sent {sent} recording(s)";
    }

    private async Task<bool> SendAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);

            // Named by a fresh identifier rather than by the file. Recordings are named with
            // local time, so the filename alone leaks roughly which part of the world a tester
            // is in; the UTC timestamp inside the payload says everything we actually need.
            var body = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, Telemetry.Endpoint)
            {
                Content = body
            };

            request.Headers.TryAddWithoutValidation("X-Ratnabay-Build", Telemetry.BuildToken);
            request.Headers.TryAddWithoutValidation("X-Ratnabay-Install", _consent.InstallId);
            request.Headers.TryAddWithoutValidation("X-Ratnabay-Upload",
                Guid.NewGuid().ToString("N"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("RatnaBay", "alpha"));

            using var response = await Client.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            // Only marked once the server has said yes. A failed send leaves no marker and is
            // retried next launch, which is the entire offline queue.
            await File.WriteAllTextAsync(path + SentSuffix,
                DateTime.UtcNow.ToString("O")).ConfigureAwait(false);

            return true;
        }
        catch (Exception exception) when (exception is IOException or HttpRequestException
            or TaskCanceledException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Recordings on disk that have not been delivered, for the settings screen.</summary>
    public int PendingCount()
    {
        try
        {
            if (!Directory.Exists(PlayRecorder.Directory)) return 0;

            var pending = 0;
            foreach (var path in Directory.GetFiles(PlayRecorder.Directory, "play_*.json"))
                if (!File.Exists(path + SentSuffix)) pending++;

            return pending;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
