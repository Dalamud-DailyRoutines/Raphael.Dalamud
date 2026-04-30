namespace Raphael.Dalamud.Info;

internal class RaphaelCraftState
{
    public uint RecipeID           { get; set; }
    public int  RecipeLevelTableID { get; set; }

    public uint ItemID          { get; set; }
    public int  CraftLevel      { get; set; }
    public int  CraftDurability { get; set; }
    public int  CraftProgress   { get; set; }
    public int  CraftQualityMax { get; set; }
    public int  TargetQuality   { get; set; }
    public int  RequiredQuality { get; set; }
    public bool IsCollectable   { get; set; }
    public bool IsExpert        { get; set; }

    public int  StatLevel            { get; set; }
    public int  StatCraftsmanship    { get; set; }
    public int  StatControl          { get; set; }
    public int  StatCP               { get; set; }
    public bool IsSpecialist         { get; set; }
    public bool UnlockedManipulation { get; set; }
    public int  InitialQuality       { get; set; } = 0;
    public int  StellarSteadyHand    { get; set; }

    public RaphaelConsumable? Food   { get; set; }
    public RaphaelConsumable? Potion { get; set; }
}
