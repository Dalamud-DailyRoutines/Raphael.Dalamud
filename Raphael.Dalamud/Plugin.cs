using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    
    private ICallGateProvider<uint>                                          startCalculationProvider         = null!;
    private ICallGateProvider<uint, uint>                                    startCalculationWithRecipeProvider = null!;
    private ICallGateProvider<uint, Tuple<uint, string, string, List<uint>>> getStatusProvider                = null!;
    private ICallGateProvider<uint>                                          getCurrentRecipeIDProvider       = null!;
    
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
        getStatusProvider.UnregisterFunc();
        getCurrentRecipeIDProvider.UnregisterFunc();
    }

    private void SetupIPC()
    {
        startCalculationProvider = PI.GetIpcProvider<uint>("Raphael.Dalamud.StartCalculation");
        startCalculationProvider.RegisterFunc(StartCalculation);

        startCalculationWithRecipeProvider = PI.GetIpcProvider<uint, uint>("Raphael.Dalamud.StartCalculationWithRecipe");
        startCalculationWithRecipeProvider.RegisterFunc(StartCalculationWithRecipe);

        getStatusProvider = PI.GetIpcProvider<uint, Tuple<uint, string, string, List<uint>>>("Raphael.Dalamud.GetCalculationStatus");
        getStatusProvider.RegisterFunc(GetCalculationStatus);

        getCurrentRecipeIDProvider = PI.GetIpcProvider<uint>("Raphael.Dalamud.GetCurrentRecipeID");
        getCurrentRecipeIDProvider.RegisterFunc(GetCurrentRecipeID);
    }

    /// <summary>
    /// 启动新的计算请求
    /// </summary>
    /// <returns>返回唯一的请求ID</returns>
    private uint StartCalculation()
    {
        var requestID = nextRequestID++;
        var request = new CalculationRequest
        {
            RequestID = requestID,
            Status    = CalculationStatus.Idle
        };
        
        activeRequests[requestID] = request;
        CleanupOldRequests();

        _ = Task.Run(async () =>
        {
            try
            {
                if (GetCurrentCraftState() is not { } craftState)
                    throw new Exception("Failed to obtain game data / 获取游戏数据失败");
                
                var config = new RaphaelGenerationConfig();
                Log.Debug($"Started calculation / 已启动计算, Request ID / 请求 ID: {requestID}, Recipe ID / 配方 ID: {craftState.RecipeID}");
                await RunSolverAsync(requestID, craftState, config);
            }
            catch (Exception e)
            {
                if (activeRequests.TryGetValue(requestID, out var failedRequest))
                {
                    failedRequest.Status = CalculationStatus.Failed;
                    failedRequest.ErrorMessage = e.Message;
                }
                
                Log.Error(e, $"IPC StartCalculation: Throwing exception / IPC 启动计算时抛出异常: {e.Message}");
            }
        });

        return requestID;
    }

    /// <summary>
    /// 启动新的计算请求，使用指定的配方ID
    /// </summary>
    /// <param name="recipeId">配方ID</param>
    /// <returns>返回唯一的请求ID</returns>
    private uint StartCalculationWithRecipe(uint recipeId)
    {
        var requestID = nextRequestID++;
        var request = new CalculationRequest
        {
            RequestID = requestID,
            Status    = CalculationStatus.Idle
        };
        
        activeRequests[requestID] = request;
        CleanupOldRequests();

        _ = Task.Run(async () =>
        {
            try
            {
                if (GetCraftStateByRecipeID(recipeId) is not { } craftState)
                    throw new Exception($"Failed to obtain recipe data for ID {recipeId} / 无法获取配方 ID {recipeId} 的数据");
                
                var config = new RaphaelGenerationConfig();
                Log.Debug($"Started calculation with recipe ID / 已启动指定配方计算, Request ID / 请求 ID: {requestID}, Recipe ID / 配方 ID: {recipeId}");
                await RunSolverAsync(requestID, craftState, config);
            }
            catch (Exception e)
            {
                if (activeRequests.TryGetValue(requestID, out var failedRequest))
                {
                    failedRequest.Status = CalculationStatus.Failed;
                    failedRequest.ErrorMessage = e.Message;
                }
                
                Log.Error(e, $"IPC StartCalculationWithRecipe: Throwing exception / IPC 启动指定配方计算时抛出异常: {e.Message}");
            }
        });

        return requestID;
    }

    /// <summary>
    /// 获取指定请求的计算状态和结果
    /// </summary>
    /// <param name="requestId">请求ID</param>
    /// <returns>包含状态和结果的元组 (RequestId, Status, ErrorMessage, ResultActionIDs)</returns>
    private Tuple<uint, string, string, List<uint>> GetCalculationStatus(uint requestId)
    {
        if (activeRequests.TryGetValue(requestId, out var request))
        {
            return new Tuple<uint, string, string, List<uint>>(
                request.RequestID,
                request.Status.ToString(),
                request.ErrorMessage,
                request.ResultActionIDs
            );
        }

        return new Tuple<uint, string, string, List<uint>>(
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
        {
            activeRequests.TryRemove(key, out _);
        }
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
            request.Status = CalculationStatus.Calculating;
            request.ErrorMessage = string.Empty;
            request.ResultActionIDs.Clear();
            
            Log.Debug($"Starting Raphael Solver for request {requestId} / 正在启动新一轮求解");

            using var runner = new RaphaelRunner(Path.Join(PI.AssemblyLocation.DirectoryName!, "raphael-cli.exe"));
            if (await runner.GenerateSolutionAsync(craftState, generationConfig) is { Count: > 0 } actionList)
            {
                var resultActionIds = actionList.Select(ActionMap.GetActionID).Where(id => id != 0).ToList();

                if (resultActionIds.Count == actionList.Count)
                {
                    request.Status          = CalculationStatus.Success;
                    request.ErrorMessage    = string.Empty;
                    request.ResultActionIDs = resultActionIds;
                    
                    Log.Debug($"Calculation {requestId} Finished / 求解完成");
                }
                else
                {
                    request.Status = CalculationStatus.Failed;
                    request.ErrorMessage = "Failed to convert one or more skill names to action IDs / 未能将一个或多个技能名称映射到技能ID";

                    var message = $"Calculation {requestId} Failed / 求解失败: {request.ErrorMessage}";
                    Log.Error(message);
                    Chat.PrintError(message);
                }
            }
            else
            {
                request.Status = CalculationStatus.Failed;
                request.ErrorMessage = "Failed to generate a valid skill sequence / 求解器未能生成有效的技能序列";
                
                var message = $"Calculation {requestId} Failed / 求解失败: {request.ErrorMessage}";
                Log.Error(message);
                Chat.PrintError(message);
            }
        }
        catch (Exception e)
        {
            request.Status = CalculationStatus.Failed;
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

        var levelTable = GetRecipeLevelTable(recipe, localPlayer->Level);

        var state = new RaphaelCraftState
        {
            RecipeID             = recipe.RowId,
            ItemID               = recipe.ItemResult.RowId,
            CraftLevel           = levelTable.ClassJobLevel,
            CraftDurability      = RecipeDurability(recipe, levelTable),
            CraftProgress        = RecipeDifficulty(recipe, levelTable),
            CraftQualityMax      = RecipeMaxQuality(recipe, levelTable),
            IsExpert             = recipe.IsExpert,
            StatLevel            = localPlayer->Level,
            StatCraftsmanship    = PlayerState.Instance()->Attributes[70],
            StatControl          = PlayerState.Instance()->Attributes[71],
            StatCP               = localPlayer->MaxCraftingPoints,
            UnlockedManipulation = IsManipulationUnlocked((Job)localPlayer->ClassJob)
        };

        return state;
    }
    
    private static unsafe RaphaelCraftState? GetCurrentCraftState()
    {
        if (!TryGetCurrentRecipe(out var recipe)) return null;
        
        var localPlayer = Control.GetLocalPlayer();
        var levelTable  = GetRecipeLevelTable(recipe, localPlayer->Level);

        var state = new RaphaelCraftState
        {
            RecipeID             = recipe.RowId,
            ItemID               = recipe.ItemResult.RowId,
            CraftLevel           = levelTable.ClassJobLevel,
            CraftDurability      = RecipeDurability(recipe, levelTable),
            CraftProgress        = RecipeDifficulty(recipe, levelTable),
            CraftQualityMax      = RecipeMaxQuality(recipe, levelTable),
            IsExpert             = recipe.IsExpert,
            StatLevel            = localPlayer->Level,
            StatCraftsmanship    = PlayerState.Instance()->Attributes[70],
            StatControl          = PlayerState.Instance()->Attributes[71],
            StatCP               = localPlayer->MaxCraftingPoints,
            UnlockedManipulation = IsManipulationUnlocked((Job)localPlayer->ClassJob)
        };

        Log.Debug($"配方 ID: {state.RecipeID} / 耐久: {state.CraftDurability} / 进展: {state.CraftProgress} / 品质: {state.CraftQualityMax}");
        return state;
    }

    private static RecipeLevelTable GetRecipeLevelTable(Recipe recipe, int playerLevel) =>
        recipe.Number == 0 && playerLevel < 100
            ? LuminaGetter.Get<RecipeLevelTable>().First(x => x.ClassJobLevel == playerLevel)
            : recipe.RecipeLevelTable.Value;

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

    private static unsafe RecipeNoteRecipeEntry* GetSelectedRecipeEntry()
    {
        var recipeData = RecipeNoteRecipeData.Ptr();
        return recipeData                != null &&
               recipeData->Recipes       != null &&
               recipeData->SelectedIndex < recipeData->RecipesCount
                   ? recipeData->Recipes + recipeData->SelectedIndex
                   : null;
    }

    private static uint GetCurrentRecipeID() => 
        TryGetCurrentRecipe(out var recipe) ? recipe.RowId : 0;

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
    
}
