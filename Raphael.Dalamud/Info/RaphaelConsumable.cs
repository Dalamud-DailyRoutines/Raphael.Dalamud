namespace Raphael.Dalamud.Info;

internal readonly struct RaphaelConsumable
(
    uint itemID,
    bool isHQ
)
{
    public uint ItemID { get; } = itemID;
    public bool IsHQ   { get; } = isHQ;
}
