namespace Raphael.Dalamud.Info;

internal class RaphaelGenerationConfig
{
    public bool? EnsureReliability    { get; set; }
    public bool? BackloadProgress     { get; set; }
    public bool? AllowHeartAndSoul    { get; set; }
    public bool? AllowQuickInnovation { get; set; }
    public int?  MaxThreads           { get; set; }
    public int?  TimeoutSeconds       { get; set; }
    public int?  TargetQuality        { get; set; }
    public int?  InitialQuality       { get; set; }
    public uint? FoodItemID           { get; set; }
    public bool? FoodHQ               { get; set; }
    public uint? PotionItemID         { get; set; }
    public bool? PotionHQ             { get; set; }
    public int?  StellarSteadyHand    { get; set; }
}
