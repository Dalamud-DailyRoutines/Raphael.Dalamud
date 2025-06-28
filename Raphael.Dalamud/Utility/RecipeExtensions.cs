using Lumina.Excel.Sheets;

namespace Raphael.Dalamud.Utility;

internal static class RecipeExtensions
{
    public static float GetBaseProgress(this Recipe recipe, uint craftsmanship, byte jobLvl)
    {
        var lvl          = recipe.RecipeLevelTable.Value;
        var baseProgress = (craftsmanship * 10f / lvl.ProgressDivider) + 2.0f;
        if (jobLvl <= lvl.ClassJobLevel)
            baseProgress *= lvl.ProgressModifier / 100f;
        return baseProgress;
    }

    public static float GetBaseQuality(this Recipe recipe, uint control, byte jobLvl)
    {
        var lvl          = recipe.RecipeLevelTable.Value;
        var baseProgress = (control * 10f / lvl.QualityDivider) + 35.0f;
        if (jobLvl <= lvl.ClassJobLevel)
            baseProgress *= lvl.QualityModifier / 100f;
        return baseProgress;
    }
}
