using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Raphael.Dalamud.Info;

[StructLayout(LayoutKind.Explicit, Size = 0x3B0)]
internal unsafe struct RecipeNoteRecipeData
{
    public static RecipeNoteRecipeData* Ptr() => 
        (RecipeNoteRecipeData*)RecipeNote.Instance()->RecipeList;

    [FieldOffset(0x000)] public RecipeNoteRecipeEntry* Recipes;
    [FieldOffset(0x008)] public int                    RecipesCount;
    [FieldOffset(0x438)] public ushort                 SelectedIndex;

    public RecipeNoteRecipeEntry* FindRecipeById(uint id)
    {
        if (Recipes == null)
            return null;
            
        for (var i = 0; i < RecipesCount; ++i)
        {
            var r = Recipes + i;
            if (r->RecipeId == id)
                return r;
        }
            
        return null;
    }
}
