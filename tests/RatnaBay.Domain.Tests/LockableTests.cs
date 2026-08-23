namespace RatnaBay.Domain.Tests;

public sealed class LockableTests
{
    [Test]
    public void RestoreOpenedBypassesARepeatSkillCheck()
    {
        var door = new Lockable(locked: true, difficulty: 100f);

        door.RestoreOpened();

        Assert.Multiple(() =>
        {
            Assert.That(door.IsLocked, Is.False);
            Assert.That(door.IsOpen, Is.True);
        });
    }
}
