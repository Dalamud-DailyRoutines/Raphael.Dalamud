using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Raphael.Dalamud.Info;

[StructLayout(LayoutKind.Explicit, Size = 0x400)]
internal unsafe struct RecipeNoteRecipeEntry
{
    [FieldOffset(0x000)] public fixed byte Ingredients[6 * 0x88];

    public Span<RecipeNoteIngredientEntry> IngredientsSpan => 
        new(Unsafe.AsPointer(ref Ingredients[0]), 6);

    [FieldOffset(0x3B2)] public ushort RecipeId;
    [FieldOffset(0x3D7)] public byte   CraftType;
        
}
