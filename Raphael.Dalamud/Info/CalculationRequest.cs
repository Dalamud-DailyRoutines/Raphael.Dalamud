using System;
using System.Collections.Generic;

namespace Raphael.Dalamud.Info;

internal class CalculationRequest
{
    public uint              RequestID       { get; set; }
    public CalculationStatus Status          { get; set; } = CalculationStatus.Idle;
    public string            ErrorMessage    { get; set; } = string.Empty;
    public List<uint>        ResultActionIDs { get; set; } = [];
    public int               ResultProgress  { get; set; }
    public int               ResultQuality   { get; set; }
    public DateTime          CreatedTime     { get; set; } = DateTime.UtcNow;
}
