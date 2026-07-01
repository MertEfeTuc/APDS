namespace APDS.Models
{
    public record VersionCompareRow(string Label, string? OldValue, string? NewValue)
    {
        public bool Changed => OldValue != NewValue;
    }
}