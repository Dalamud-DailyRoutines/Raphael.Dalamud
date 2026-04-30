using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;
using Raphael.Dalamud.Utility;

namespace Raphael.Dalamud.Info;

internal static class ActionMap
{
    private static readonly Dictionary<uint, string> ActionNames = BuildActionNames();

    public static uint GetActionID(uint baseActionID)
    {
        if (IsFixedAction(baseActionID))
            return baseActionID;

        if (!ActionNames.TryGetValue(baseActionID, out var actionName))
            return 0;

        var job = GetJob();
        if (job is CrafterJob.Unknown)
            return 0;

        return baseActionID >= CRAFT_ACTION_ID_START
                   ? GetCraftActionID(job, actionName)
                   : GetRegularActionID(job, actionName);
    }

    private static uint GetCraftActionID(CrafterJob job, string actionName) =>
        LuminaGetter.Get<CraftAction>()
                    .FirstOrDefault
                    (row => IsCrafterCategory(row.ClassJobCategory.Value, job) &&
                            row.Name.ToString().Trim() == actionName
                    )
                    .RowId;

    private static uint GetRegularActionID(CrafterJob job, string actionName) =>
        LuminaGetter.Get<Action>()
                    .FirstOrDefault
                    (row => row.ClassJob.RowId         == (uint)job &&
                            row.Name.ToString().Trim() == actionName
                    )
                    .RowId;

    private static bool IsCrafterCategory(ClassJobCategory category, CrafterJob job) =>
        job switch
        {
            CrafterJob.CRP => category.CRP,
            CrafterJob.BSM => category.BSM,
            CrafterJob.ARM => category.ARM,
            CrafterJob.GSM => category.GSM,
            CrafterJob.LTW => category.LTW,
            CrafterJob.WVR => category.WVR,
            CrafterJob.ALC => category.ALC,
            CrafterJob.CUL => category.CUL,
            _              => false
        };

    private static Dictionary<uint, string> BuildActionNames()
    {
        var actionIDs = new[]
        {
            100001u,
            100002u,
            100003u,
            100010u,
            100371u,
            4631u,
            19297u,
            100004u,
            260u,
            19004u,
            19012u,
            4639u,
            100339u,
            100128u,
            100379u,
            100203u,
            4574u,
            100227u,
            100411u,
            100387u,
            100299u,
            100403u,
            100323u,
            100315u,
            100283u,
            100419u,
            100427u,
            100435u,
            100459u,
            100467u,
            100475u,
            100443u,
            100363u,
            100355u,
            100451u
        };

        return actionIDs.ToDictionary(actionID => actionID, GetActionName);
    }

    private static string GetActionName(uint actionID)
    {
        if (actionID >= CRAFT_ACTION_ID_START)
            return LuminaGetter.Get<CraftAction>().GetRow(actionID).Name.ToString().Trim();

        return LuminaGetter.Get<Action>().GetRow(actionID).Name.ToString().Trim();
    }

    private static bool IsFixedAction(uint actionID) =>
        actionID is STELLAR_STEADY_HAND_ID;

    private static unsafe CrafterJob GetJob()
    {
        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null)
            return CrafterJob.Unknown;

        var job = (CrafterJob)localPlayer->ClassJob;
        return job is >= CrafterJob.CRP and <= CrafterJob.CUL ? job : CrafterJob.Unknown;
    }

    #region Constants

    private const uint CRAFT_ACTION_ID_START  = 100000;
    private const uint STELLAR_STEADY_HAND_ID = 46843;

    #endregion
}
