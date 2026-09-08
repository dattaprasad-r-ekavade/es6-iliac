using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

/// <summary>
/// What a recording says it is.
///
/// This field exists because a guard failed twice in the same way. Scripted runs were excluded
/// from uploading, then written anyway and swept up by the next launch that flushed the queue;
/// --faces and --sprites were missed entirely, because they bail out of LoadContent after
/// Initialize has already sent. Both leaks were invisible in the data: 82 of the first 180
/// recordings are gate runs, and nothing in any of them says so.
///
/// So these tests are about the two properties that make the field worth having at all -- that
/// an old recording does not claim to be real play, and that an unrecognised kind is not
/// mistaken for one.
/// </summary>
public class PlayModeTests
{
    [Test]
    public void OnlyPlayIsWorthRecording()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PlayMode.IsWorthRecording(PlayMode.Play), Is.True);
            Assert.That(PlayMode.IsWorthRecording(PlayMode.Script), Is.False);
            Assert.That(PlayMode.IsWorthRecording(PlayMode.Capture), Is.False);
            Assert.That(PlayMode.IsWorthRecording(PlayMode.Tool), Is.False);
        });
    }

    /// <summary>
    /// The 180 recordings already on the server predate this field and deserialise with it
    /// empty. Counting them as play would bake in the exact claim the field exists to prevent
    /// anybody making -- that every recording in the corpus is a sitting somebody played.
    /// </summary>
    [Test]
    public void ARecordingFromBeforeTheFieldExistedIsNotCountedAsPlay()
    {
        var older = new PlayRecording();

        Assert.Multiple(() =>
        {
            Assert.That(older.Mode, Is.EqualTo(PlayMode.Unknown));
            Assert.That(PlayMode.IsWorthRecording(older.Mode), Is.False);
        });
    }

    /// <summary>
    /// The failure mode to design against is a launch path nobody has thought of yet. Twice the
    /// new path was the one that leaked, so an unrecognised kind has to fall on the safe side
    /// rather than default into the corpus.
    /// </summary>
    [Test]
    public void AKindNobodyHasHeardOfIsNotCountedAsPlay()
    {
        Assert.That(PlayMode.IsWorthRecording("benchmark"), Is.False);
    }

    /// <summary>
    /// A mode that does not survive being written and read back is no better than no mode, and
    /// the reader may well be an older or newer build than the writer.
    /// </summary>
    [Test]
    public void TheModeSurvivesARoundTrip()
    {
        var written = new PlayRecording { Mode = PlayMode.Script, Build = "alpha-test" };

        var json = PlayRecording.Serialize(written);
        var path = Path.Combine(Path.GetTempPath(), $"ratnabay-mode-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);

        try
        {
            Assert.That(PlayRecording.TryLoad(path, out var read, out var error), Is.True, error);
            Assert.That(read!.Mode, Is.EqualTo(PlayMode.Script));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
