using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Raphael.Dalamud.Info;
using Raphael.Dalamud.Utility;

namespace Raphael.Dalamud;

public sealed class Plugin : IDalamudPlugin
{
    internal static IDalamudPluginInterface PI   { get; private set; } = null!;
    internal static IPluginLog              Log  { get; private set; } = null!;
    internal static IChatGui                Chat { get; private set; } = null!;
    internal static IDataManager            Data { get; private set; } = null!;

    private ICallGateProvider<uint>                                          startCalculationProvider           = null!;
    private ICallGateProvider<uint, uint>                                    startCalculationWithRecipeProvider = null!;
    private ICallGateProvider<uint, string, uint>                            startCalculationWithConfigProvider = null!;
    private ICallGateProvider<uint, Tuple<uint, string, string, List<uint>>> getStatusProvider                  = null!;
    private ICallGateProvider<uint>                                          getCurrentRecipeIDProvider         = null!;

    private readonly ConcurrentDictionary<uint, CalculationRequest> activeRequests = [];
    private          uint                                           nextRequestID  = 1;

    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog log, IChatGui chat, IDataManager data)
    {
        PI   = pluginInterface;
        Log  = log;
        Chat = chat;
        Data = data;

        SetupIPC();
    }

    public void Dispose()
    {
        startCalculationProvider.UnregisterFunc();
        startCalculationWithRecipeProvider.UnregisterFunc();
        startCalculationWithConfigProvider.UnregisterFunc();
        getStatusProvider.UnregisterFunc();
        getCurrentRecipeIDProvider.UnregisterFunc();
    }

    private void SetupIPC()
    {
        startCalculationProvider = PI.GetIpcProvider<uint>("Raphael.Dalamud.StartCalculation");
        startCalculationProvider.RegisterFunc(StartCalculation);

        startCalculationWithRecipeProvider = PI.GetIpcProvider<uint, uint>("Raphael.Dalamud.StartCalculationWithRecipe");
        startCalculationWithRecipeProvider.RegisterFunc(StartCalculationWithRecipe);

        startCalculationWithConfigProvider = PI.GetIpcProvider<uint, string, uint>("Raphael.Dalamud.StartCalculationWithConfig");
        startCalculationWithConfigProvider.RegisterFunc(StartCalculationWithConfig);

        getStatusProvider = PI.GetIpcProvider<uint, Tuple<uint, string, string, List<uint>>>("Raphael.Dalamud.GetCalculationStatus");
        getStatusProvider.RegisterFunc(GetCalculationStatus);

        getCurrentRecipeIDProvider = PI.GetIpcProvider<uint>("Raphael.Dalamud.GetCurrentRecipeID");
        getCurrentRecipeIDProvider.RegisterFunc(GetCurrentRecipeID);
    }

    /// <summary>
    ///     启动新的计算请求
    /// </summary>
    /// <returns>返回唯一的请求ID</returns>
    private uint StartCalculation() =>
        QueueCalculation(0, string.Empty, true);

    /// <summary>
    ///     启动新的计算请求，使用指定的配方ID
    /// </summary>
    /// <param name="recipeId">配方ID</param>
    /// <returns>返回唯一的请求ID</returns>
    private uint StartCalculationWithRecipe(uint recipeId) =>
        QueueCalculation(recipeId, string.Empty, false);

    private uint StartCalculationWithConfig(uint recipeId, string configJson) =>
        QueueCalculation(recipeId, configJson, recipeId == 0);

    private uint QueueCalculation(uint recipeId, string configJson, bool useCurrentRecipe)
    {
        var requestID = nextRequestID++;
        var request = new CalculationRequest
        {
            RequestID = requestID,
            Status    = CalculationStatus.Idle
        };

        activeRequests[requestID] = request;
        CleanupOldRequests();

        _ = Task.Run
        (async () =>
            {
                try
                {
                    var config     = ParseGenerationConfig(configJson);
                    var craftState = useCurrentRecipe ? GetCurrentCraftState() : GetCraftStateByRecipeID(recipeId);
                    if (craftState is null)
                        throw new Exception(useCurrentRecipe ? "获取当前配方数据失败" : $"无法获取配方 ID {recipeId} 的数据");

                    ApplyConfigOverrides(craftState, config);
                    ApplyDefaultConfig(craftState, config);

                    Log.Debug($"已启动计算, 请求 ID: {requestID}, 配方 ID: {craftState.RecipeID}");
                    await RunSolverAsync(requestID, craftState, config);
                }
                catch (Exception e)
                {
                    if (activeRequests.TryGetValue(requestID, out var failedRequest))
                    {
                        failedRequest.Status       = CalculationStatus.Failed;
                        failedRequest.ErrorMessage = e.Message;
                    }

                    Log.Error(e, $"IPC 启动计算时抛出异常: {e.Message}");
                }
            }
        );

        return requestID;
    }

    /// <summary>
    ///     获取指定请求的计算状态和结果
    /// </summary>
    /// <param name="requestId">请求ID</param>
    /// <returns>包含状态和结果的元组 (RequestId, Status, ErrorMessage, ResultActionIDs)</returns>
    private Tuple<uint, string, string, List<uint>> GetCalculationStatus(uint requestId)
    {
        if (activeRequests.TryGetValue(requestId, out var request))
        {
            return new Tuple<uint, string, string, List<uint>>
            (
                request.RequestID,
                request.Status.ToString(),
                request.ErrorMessage,
                request.ResultActionIDs
            );
        }

        return new Tuple<uint, string, string, List<uint>>
        (
            requestId,
            nameof(CalculationStatus.Failed),
            "Request not found / 未找到指定请求",
            []
        );
    }

    private void CleanupOldRequests()
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-5);
        var keysToRemove = activeRequests
                           .Where(kvp => kvp.Value.CreatedTime < cutoffTime)
                           .Select(kvp => kvp.Key)
                           .ToList();

        foreach (var key in keysToRemove)
            activeRequests.TryRemove(key, out _);
    }

    private async Task RunSolverAsync(uint requestId, RaphaelCraftState craftState, RaphaelGenerationConfig generationConfig)
    {
        if (!activeRequests.TryGetValue(requestId, out var request))
        {
            Log.Warning($"Request {requestId} not found during execution / 执行期间未找到请求 {requestId}");
            return;
        }

        try
        {
            request.Status       = CalculationStatus.Calculating;
            request.ErrorMessage = string.Empty;
            request.ResultActionIDs.Clear();

            Log.Debug($"Starting Raphael Solver for request {requestId} / 正在启动新一轮求解");

            using var runner          = new RaphaelRunner(Path.Join(PI.AssemblyLocation.DirectoryName!, "raphael-cli.exe"));
            var       actionIDs       = await runner.GenerateSolutionAsync(craftState, generationConfig);
            var       resultActionIds = actionIDs.Select(ActionMap.GetActionID).Where(id => id != 0).ToList();

            if (resultActionIds.Count != actionIDs.Count)
                throw new InvalidOperationException("未能将一个或多个技能 ID 映射到当前职业技能");

            request.Status          = CalculationStatus.Success;
            request.ErrorMessage    = string.Empty;
            request.ResultActionIDs = resultActionIds;

            Log.Debug($"Calculation {requestId} Finished / 求解完成");
        }
        catch (Exception e)
        {
            request.Status       = CalculationStatus.Failed;
            request.ErrorMessage = e.Message;

            var message = $"Calculation {requestId} Failed / 求解失败: {request.ErrorMessage}";
            Log.Error(message);
            Chat.PrintError(message);
        }
    }

    private static unsafe RaphaelCraftState? GetCraftStateByRecipeID(uint recipeId)
    {
        if (!LuminaGetter.TryGetRow(recipeId, out Recipe recipe))
        {
            Log.Warning($"Recipe not found / 未找到配方: {recipeId}");
            return null;
        }

        var localPlayer = Control.GetLocalPlayer();

        if (localPlayer == null)
        {
            Log.Warning("Local player not found / 未找到本地玩家");
            return null;
        }

        return BuildCraftState(recipe, localPlayer->Level, null);
    }

    private static unsafe RaphaelCraftState? GetCurrentCraftState()
    {
        var entry = GetSelectedRecipeEntry();
        if (entry == null) return null;
        if (!LuminaGetter.TryGetRow(entry->RecipeId, out Recipe recipe)) return null;

        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null) return null;

        var state = BuildCraftState(recipe, localPlayer->Level, entry);
        if (state == null) return null;

        Log.Debug($"配方 ID: {state.RecipeID} / 耐久: {state.CraftDurability} / 进展: {state.CraftProgress} / 品质: {state.CraftQualityMax}");
        return state;
    }

    private static unsafe RaphaelCraftState? BuildCraftState(Recipe recipe, int playerLevel, RecipeNote.RecipeEntry* entry)
    {
        var localPlayer = Control.GetLocalPlayer();

        if (localPlayer == null)
        {
            Log.Warning("Local player not found / 未找到本地玩家");
            return null;
        }

        var job             = (Job)localPlayer->ClassJob;
        var levelTable      = GetRecipeLevelTable(recipe, playerLevel);
        var craftProgress   = entry == null ? RecipeDifficulty(recipe, levelTable) : entry->Difficulty;
        var craftQualityMax = entry == null ? RecipeMaxQuality(recipe, levelTable) : (int)entry->Quality;
        var craftDurability = entry == null ? RecipeDurability(recipe, levelTable) : entry->Durability;
        var targetQuality   = GetDefaultTargetQuality(recipe, levelTable, craftQualityMax);

        return new RaphaelCraftState
        {
            RecipeID             = recipe.RowId,
            RecipeLevelTableID   = (int)levelTable.RowId,
            ItemID               = recipe.ItemResult.RowId,
            CraftLevel           = levelTable.ClassJobLevel,
            CraftDurability      = craftDurability,
            CraftProgress        = craftProgress,
            CraftQualityMax      = craftQualityMax,
            TargetQuality        = targetQuality,
            RequiredQuality      = (int)recipe.RequiredQuality,
            IsCollectable        = recipe.ItemResult.Value.AlwaysCollectable,
            IsExpert             = recipe.IsExpert,
            StatLevel            = localPlayer->Level,
            StatCraftsmanship    = PlayerState.Instance()->Attributes[70],
            StatControl          = PlayerState.Instance()->Attributes[71],
            StatCP               = localPlayer->MaxCraftingPoints,
            IsSpecialist         = IsSpecialist(job),
            UnlockedManipulation = IsManipulationUnlocked(job),
            InitialQuality       = GetInitialQuality(entry),
            StellarSteadyHand    = Math.Min(GetStellarSteadyHandCharges(), DEFAULT_MAX_STELLAR_STEADY_HAND),
            Food                 = GetActiveConsumable(FOOD_STATUS_ID),
            Potion               = GetActiveConsumable(POTION_STATUS_ID)
        };
    }

    private static RecipeLevelTable GetRecipeLevelTable(Recipe recipe, int playerLevel) =>
        recipe.Number == 0 && playerLevel < 100
            ? LuminaGetter.Get<RecipeLevelTable>().First(x => x.ClassJobLevel == playerLevel)
            : recipe.RecipeLevelTable.Value;

    private static RaphaelGenerationConfig ParseGenerationConfig(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return new RaphaelGenerationConfig();

        try
        {
            return JsonSerializer.Deserialize<RaphaelGenerationConfig>
                   (
                       configJson,
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                   ) ??
                   new RaphaelGenerationConfig();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"配置 JSON 无效: {ex.Message}", nameof(configJson));
        }
    }

    private static void ApplyConfigOverrides(RaphaelCraftState craftState, RaphaelGenerationConfig config)
    {
        if (config.TargetQuality is { } targetQuality)
            craftState.TargetQuality = Math.Clamp(targetQuality, 0, craftState.CraftQualityMax);
        if (config.InitialQuality is { } initialQuality)
            craftState.InitialQuality = Math.Clamp(initialQuality, 0, craftState.CraftQualityMax);
        if (config.StellarSteadyHand is { } stellarSteadyHand)
            craftState.StellarSteadyHand = Math.Clamp(stellarSteadyHand, 0, DEFAULT_MAX_STELLAR_STEADY_HAND);
        if (config.FoodItemID is { } foodItemID)
            craftState.Food = new RaphaelConsumable(foodItemID, config.FoodHQ.GetValueOrDefault());
        if (config.PotionItemID is { } potionItemID)
            craftState.Potion = new RaphaelConsumable(potionItemID, config.PotionHQ.GetValueOrDefault());
    }

    private static void ApplyDefaultConfig(RaphaelCraftState craftState, RaphaelGenerationConfig config)
    {
        config.EnsureReliability    ??= false;
        config.BackloadProgress     ??= true;
        config.AllowHeartAndSoul    ??= craftState.IsSpecialist && CanUseCraftAction(BASE_HEART_AND_SOUL_ID);
        config.AllowQuickInnovation ??= craftState.IsSpecialist && CanUseCraftAction(BASE_QUICK_INNOVATION_ID);
    }

    private static int GetDefaultTargetQuality(Recipe recipe, RecipeLevelTable levelTable, int craftQualityMax)
    {
        if (recipe.RequiredQuality > 0)
            return (int)recipe.RequiredQuality;

        if (recipe.ItemResult.Value.AlwaysCollectable)
            return GetCollectableTargetQuality(recipe, levelTable, craftQualityMax);

        return craftQualityMax;
    }

    private static int GetCollectableTargetQuality(Recipe recipe, RecipeLevelTable levelTable, int craftQualityMax) =>
        recipe.CollectableMetadataKey switch
        {
            2 => GetHWDCollectableTargetQuality(recipe)             ?? craftQualityMax,
            3 => GetSatisfactionCollectableTargetQuality(recipe)    ?? craftQualityMax,
            4 => GetSharlayanCollectableTargetQuality(recipe)       ?? craftQualityMax,
            6 => GetBankaCollectableTargetQuality(recipe)           ?? craftQualityMax,
            7 => GetWKSCollectableTargetQuality(recipe, levelTable) ?? craftQualityMax,
            _ => GetShopCollectableTargetQuality(recipe)            ?? craftQualityMax
        };

    private static int? GetHWDCollectableTargetQuality(Recipe recipe)
    {
        foreach (var row in LuminaGetter.Get<HWDCrafterSupply>())
        {
            var supply = row.HWDCrafterSupplyParams.FirstOrDefault(x => x.ItemTradeIn.RowId == recipe.ItemResult.RowId);
            if (supply.ItemTradeIn.RowId == recipe.ItemResult.RowId)
                return supply.HighCollectableRating * 10;
        }

        return null;
    }

    private static int? GetSatisfactionCollectableTargetQuality(Recipe recipe)
    {
        foreach (var row in LuminaGetter.GetSub<SatisfactionSupply>().SelectMany(x => x))
        {
            if (row.Item.RowId == recipe.ItemResult.RowId)
                return row.CollectabilityHigh * 10;
        }

        return null;
    }

    private static int? GetSharlayanCollectableTargetQuality(Recipe recipe)
    {
        foreach (var row in LuminaGetter.Get<SharlayanCraftWorksSupply>())
        {
            var supply = row.Item.FirstOrDefault(x => x.ItemId.RowId == recipe.ItemResult.RowId);
            if (supply.ItemId.RowId == recipe.ItemResult.RowId)
                return supply.CollectabilityHigh * 10;
        }

        return null;
    }

    private static int? GetBankaCollectableTargetQuality(Recipe recipe)
    {
        foreach (var row in LuminaGetter.Get<BankaCraftWorksSupply>())
        {
            var supply = row.Item.FirstOrDefault(x => x.ItemId.RowId == recipe.ItemResult.RowId);
            if (supply.ItemId.RowId == recipe.ItemResult.RowId && supply.Collectability.IsValid)
                return supply.Collectability.Value.CollectabilityHigh * 10;
        }

        return null;
    }

    private static int? GetWKSCollectableTargetQuality(Recipe recipe, RecipeLevelTable levelTable)
    {
        if (!LuminaGetter.TryGetRow(recipe.CollectableMetadata.RowId, out WKSMissionToDoEvalutionRefin row))
            return null;

        var scale = levelTable.Quality * ((double)recipe.QualityFactor / 100) / 1000;
        return (int)Math.Floor(row.Unknown2 * scale) * 10;
    }

    private static int? GetShopCollectableTargetQuality(Recipe recipe)
    {
        foreach (var row in LuminaGetter.GetSub<CollectablesShopItem>().SelectMany(x => x))
        {
            if (row.Item.RowId == recipe.ItemResult.RowId && row.CollectablesShopRefine.IsValid)
                return row.CollectablesShopRefine.Value.HighCollectability * 10;
        }

        return null;
    }

    private static unsafe bool TryGetCurrentRecipe(out Recipe recipe)
    {
        recipe = default;

        var entry = GetSelectedRecipeEntry();
        if (entry == null) return false;

        return LuminaGetter.TryGetRow(entry->RecipeId, out recipe);
    }

    private static int RecipeDurability(Recipe recipe, RecipeLevelTable levelTable) =>
        (int)(levelTable.Durability * (float)recipe.DurabilityFactor / 100f);

    private static int RecipeDifficulty(Recipe recipe, RecipeLevelTable levelTable) =>
        (int)(levelTable.Difficulty * (float)recipe.DifficultyFactor / 100f);

    private static int RecipeMaxQuality(Recipe recipe, RecipeLevelTable levelTable) =>
        (int)(levelTable.Quality * (float)recipe.QualityFactor / 100f);

    private static unsafe RecipeNote.RecipeEntry* GetSelectedRecipeEntry() =>
        RecipeNote.Instance()->RecipeList->SelectedRecipe;

    private static uint GetCurrentRecipeID() =>
        TryGetCurrentRecipe(out var recipe) ? recipe.RowId : 0;

    private static unsafe int GetInitialQuality(RecipeNote.RecipeEntry* entry) =>
        entry == null ? 0 : Math.Clamp(GetAssignedHQIngredientQuality(entry), 0, (int)entry->Quality);

    private static unsafe int GetAssignedHQIngredientQuality(RecipeNote.RecipeEntry* entry)
    {
        var maxItemLevel      = 0u;
        var providedItemLevel = 0u;

        for (var i = 0; i < MAX_INGREDIENT_COUNT; i++)
        {
            var ingredient = entry->Ingredients[i];
            if (ingredient.ItemId == 0 || ingredient.Amount == 0)
                continue;

            if (!LuminaGetter.TryGetRow(ingredient.ItemId, out Item item) || !item.CanBeHq)
                continue;

            maxItemLevel      += item.LevelItem.RowId * ingredient.Amount;
            providedItemLevel += item.LevelItem.RowId * ingredient.HQCount;
        }

        if (maxItemLevel == 0)
            return 0;

        return (int)(entry->Quality * entry->MaterialQualityFactor * providedItemLevel / maxItemLevel / 100);
    }

    private static unsafe int GetStellarSteadyHandCharges()
    {
        var dutyActionManager = DutyActionManager.GetInstanceIfReady();
        if (dutyActionManager == null)
            return 0;

        for (var i = 0; i < DUTY_ACTION_SLOT_COUNT; i++)
            if (dutyActionManager->ActionId[i] == STELLAR_STEADY_HAND_ID)
                return dutyActionManager->CurCharges[i];

        return 0;
    }

    private static unsafe RaphaelConsumable? GetActiveConsumable(uint statusID)
    {
        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null)
            return null;

        for (var i = 0; i < localPlayer->StatusManager.NumValidStatuses; i++)
        {
            var status = localPlayer->StatusManager.Status[i];
            if (status.StatusId == statusID && status.Param != 0)
                return FindConsumableByStatusParam(statusID, status.Param);
        }

        return null;
    }

    private static RaphaelConsumable? FindConsumableByStatusParam(uint statusID, ushort param)
    {
        foreach (var item in LuminaGetter.Get<Item>())
        {
            if (!item.ItemAction.IsValid)
                continue;

            var action = item.ItemAction.Value;
            if (action.Data[0] == statusID && action.Data[1] == param)
                return new RaphaelConsumable(item.RowId, false);
            if (action.DataHQ[0] == statusID && action.DataHQ[1] + HQ_STATUS_PARAM_OFFSET == param)
                return new RaphaelConsumable(item.RowId, true);
        }

        return null;
    }

    private static unsafe bool IsSpecialist(Job job)
    {
        if (job is < Job.CRP or > Job.CUL)
            return false;

        return PlayerState.Instance()->IsMeisterFlagAndHasSoulStoneEquipped((uint)job);
    }

    private static unsafe bool CanUseCraftAction(uint baseActionID)
    {
        var actionID = ActionMap.GetActionID(baseActionID);
        if (actionID == 0)
            return false;

        return ActionManager.Instance()->GetActionStatus(ActionType.CraftAction, actionID) == 0;
    }

    private static bool IsQuestUnlocked(int v) =>
        QuestManager.IsQuestComplete((uint)v);

    private static bool IsManipulationUnlocked(Job job) =>
        job switch
        {
            Job.CRP => IsQuestUnlocked(67979),
            Job.BSM => IsQuestUnlocked(68153),
            Job.ARM => IsQuestUnlocked(68132),
            Job.GSM => IsQuestUnlocked(68137),
            Job.LTW => IsQuestUnlocked(68147),
            Job.WVR => IsQuestUnlocked(67969),
            Job.ALC => IsQuestUnlocked(67974),
            Job.CUL => IsQuestUnlocked(68142),
            _       => false
        };

    #region Constants

    private const int  DEFAULT_MAX_STELLAR_STEADY_HAND = 2;
    private const int  DUTY_ACTION_SLOT_COUNT          = 2;
    private const int  HQ_STATUS_PARAM_OFFSET          = 10000;
    private const int  MAX_INGREDIENT_COUNT            = 6;
    private const uint BASE_HEART_AND_SOUL_ID          = 100419;
    private const uint BASE_QUICK_INNOVATION_ID        = 100459;
    private const uint FOOD_STATUS_ID                  = 48;
    private const uint POTION_STATUS_ID                = 49;
    private const uint STELLAR_STEADY_HAND_ID          = 46843;

    #endregion
}
