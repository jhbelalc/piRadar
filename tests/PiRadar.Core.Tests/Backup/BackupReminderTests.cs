using PiRadar.Core.Backup;

namespace PiRadar.Core.Tests.Backup;

public class BackupReminderTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsDue_WhenNeverBackedUp() => Assert.True(BackupReminder.IsDue(null, Now));

    [Fact]
    public void IsDue_OnlyAfterThirtyDays()
    {
        Assert.False(BackupReminder.IsDue(Now.AddDays(-30), Now));
        Assert.True(BackupReminder.IsDue(Now.AddDays(-30).AddMinutes(-1), Now));
    }
}
