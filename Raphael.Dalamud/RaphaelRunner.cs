using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Raphael.Dalamud.Info;

namespace Raphael.Dalamud;

internal sealed class RaphaelRunner : IDisposable
{
    private readonly string cliPath;

    public RaphaelRunner(string cliExecutablePath)
    {
        if (!File.Exists(cliExecutablePath))
            throw new FileNotFoundException("未能找到 Raphael CLI 可执行文件", cliExecutablePath);

        cliPath = cliExecutablePath;
    }

    public void Dispose() { }

    internal async Task<List<uint>> GenerateSolutionAsync
    (
        RaphaelCraftState       craftState,
        RaphaelGenerationConfig config,
        CancellationToken       cancellationToken = default
    )
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(config.TimeoutSeconds ?? DEFAULT_TIMEOUT_SECONDS, 1));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var arguments = BuildArguments(craftState, config);

        Plugin.Log.Verbose($"参数: {string.Join(' ', arguments)}");

        var processStartInfo = new ProcessStartInfo
        {
            FileName               = cliPath,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8
        };

        foreach (var argument in arguments)
            processStartInfo.ArgumentList.Add(argument);

        using var process = new Process();
        process.StartInfo = processStartInfo;

        try
        {
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var errorTask  = process.StandardError.ReadToEndAsync(cts.Token);

            await process.WaitForExitAsync(cts.Token);

            var output = (await outputTask).Trim();
            var error  = await errorTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                var relevantError = error.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                                         .LastOrDefault() ??
                                    "求解器返回了无效的技能序列";

                throw new InvalidOperationException(relevantError.Trim());
            }

            var actionIDs = ParseActionIDOutput(output);
            if (actionIDs.Count == 0)
                throw new InvalidOperationException("求解器未能生成有效的技能序列");

            return actionIDs;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(true);

            throw new TimeoutException($"求解器在 {timeout.TotalSeconds:0} 秒内未返回结果");
        }
    }

    private static List<string> BuildArguments(RaphaelCraftState craft, RaphaelGenerationConfig config)
    {
        var arguments = new List<string>
        {
            "solve",
            "--custom-recipe",
            craft.RecipeLevelTableID.ToString(),
            craft.CraftProgress.ToString(),
            GetTargetQuality(craft, config).ToString(),
            craft.CraftDurability.ToString(),
            craft.IsExpert ? "1" : "0",
            "--level",
            craft.StatLevel.ToString(),
            "--stats",
            craft.StatCraftsmanship.ToString(),
            craft.StatControl.ToString(),
            craft.StatCP.ToString(),
            "--initial-quality",
            Math.Max(config.InitialQuality ?? craft.InitialQuality, 0).ToString(),
            "--stellar-steady-hand",
            GetStellarSteadyHand(craft, config).ToString()
        };

        AddConsumable(arguments, "--food",   GetFood(craft, config));
        AddConsumable(arguments, "--potion", GetPotion(craft, config));

        if (craft.UnlockedManipulation)
            arguments.Add("--manipulation");
        if (config.EnsureReliability.GetValueOrDefault(false))
            arguments.Add("--adversarial");
        if (config.BackloadProgress.GetValueOrDefault(true))
            arguments.Add("--backload-progress");
        if (config.AllowHeartAndSoul.GetValueOrDefault(craft.IsSpecialist))
            arguments.Add("--heart-and-soul");
        if (config.AllowQuickInnovation.GetValueOrDefault(craft.IsSpecialist))
            arguments.Add("--quick-innovation");

        if (config.MaxThreads is > 0)
        {
            arguments.Add("--threads");
            arguments.Add(config.MaxThreads.Value.ToString());
        }

        arguments.Add("--output-variables");
        arguments.Add("action_ids");

        return arguments;
    }

    private static RaphaelConsumable? GetFood(RaphaelCraftState craft, RaphaelGenerationConfig config)
    {
        if (config.FoodItemID is { } itemID)
            return new RaphaelConsumable(itemID, config.FoodHQ.GetValueOrDefault());

        return craft.Food;
    }

    private static RaphaelConsumable? GetPotion(RaphaelCraftState craft, RaphaelGenerationConfig config)
    {
        if (config.PotionItemID is { } itemID)
            return new RaphaelConsumable(itemID, config.PotionHQ.GetValueOrDefault());

        return craft.Potion;
    }

    private static int GetStellarSteadyHand(RaphaelCraftState craft, RaphaelGenerationConfig config)
    {
        var charges = config.StellarSteadyHand ?? craft.StellarSteadyHand;
        return Math.Clamp(charges, 0, MAX_STELLAR_STEADY_HAND);
    }

    private static int GetTargetQuality(RaphaelCraftState craft, RaphaelGenerationConfig config)
    {
        if (config.TargetQuality is { } targetQuality)
            return Math.Clamp(targetQuality, 0, craft.CraftQualityMax);

        return craft.TargetQuality > 0 ? craft.TargetQuality : craft.CraftQualityMax;
    }

    private static void AddConsumable(List<string> arguments, string option, RaphaelConsumable? consumable)
    {
        if (consumable is not { ItemID: > 0 } value)
            return;

        arguments.Add(option);
        arguments.Add(value.IsHQ ? $"{value.ItemID},HQ" : value.ItemID.ToString());
    }

    private static List<uint> ParseActionIDOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return [];

        return output.Replace("[", "")
                     .Replace("]",  "")
                     .Replace("\"", "")
                     .Split([','], StringSplitOptions.RemoveEmptyEntries)
                     .Select(value => uint.TryParse(value.Trim(), out var actionID) ? actionID : 0)
                     .Where(actionID => actionID != 0)
                     .ToList();
    }

    #region Constants

    private const int DEFAULT_TIMEOUT_SECONDS = 60;
    private const int MAX_STELLAR_STEADY_HAND = 2;

    #endregion
}
