using System.Runtime.InteropServices;

namespace Raphael.Dalamud.Info;

[StructLayout(LayoutKind.Explicit, Size = 0x88)]
internal struct RecipeNoteIngredientEntry
{
    [FieldOffset(0x04)] public ushort NumAvailableNQ;
    [FieldOffset(0x06)] public ushort NumAvailableHQ;
    [FieldOffset(0x08)] public byte   NumAssignedNQ;
    [FieldOffset(0x09)] public byte   NumAssignedHQ;
    [FieldOffset(0x78)] public uint   ItemId;
    [FieldOffset(0x82)] public byte   NumTotal;
}
