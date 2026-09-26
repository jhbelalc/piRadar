namespace PiRadar.Core.Backup;

/// <summary>Cuándo recordar hacer un respaldo: nunca se ha hecho o pasaron más de 30 días.</summary>
public static class BackupReminder
{
    public static readonly TimeSpan Interval = TimeSpan.FromDays(30);

    public static bool IsDue(DateTime? lastBackupUtc, DateTime nowUtc) =>
        lastBackupUtc is not { } last || nowUtc - last > Interval;
}
