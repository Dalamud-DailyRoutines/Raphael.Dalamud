using System.Collections.Generic;

namespace Raphael.Dalamud.Info;

internal sealed class RaphaelSolutionResult
{
    public List<uint> ActionIDs { get; set; } = [];
    public int        Progress  { get; set; }
    public int        Quality   { get; set; }
}
