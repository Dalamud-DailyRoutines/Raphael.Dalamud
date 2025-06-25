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

internal class RaphaelRunner : IDisposable
{
    private readonly string cliPath;

    public RaphaelRunner(string cliExecutablePath)
    {
        if (!File.Exists(cliExecutablePath))
            throw new FileNotFoundException("Raphael CLI executable not found / 未能找到 Raphael CLI 可执行文件", cliExecutablePath);

        cliPath = cliExecutablePath;
    }

    public void Dispose()
    {
        
    }

    internal async Task<List<string>?> GenerateSolutionAsync(
        RaphaelCraftState       craftState,
        RaphaelGenerationConfig config,
        int                     maxThreads        = 0,
        TimeSpan?               timeout           = null,
        CancellationToken       cancellationToken = default)
    {
        timeout ??= TimeSpan.FromMinutes(1);
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout.Value);
        
        var arguments = BuildArguments(craftState, config, new List<string> { "actions" }, maxThreads);
        
        Plugin.Log.Verbose($"[Raphael.Dalamud] Arguments / 参数: {arguments}");
        
        var processStartInfo = new ProcessStartInfo
        {
            FileName               = cliPath,
            Arguments              = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

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
                                    "Solver returned an empty or invalid macro / 求解器返回了无效的技能序列";
                
                Plugin.Log.Error($"[Raphael.Dalamud] Error / 错误: {relevantError.Trim()}");
                return null;
            }
            
            return ParseActionsOutput(output);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(true);

            Plugin.Log.Error("[Raphael.Dalamud] Error / 错误: Operation was canceled / 操作已取消");
            return null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[Raphael.Dalamud] Error / 错误: An unexpected error occurred / 发生异常 {ex.Message}");
            return null;
        }
    }

    private static string BuildArguments(RaphaelCraftState       craft, 
                                         RaphaelGenerationConfig config, 
                                         IEnumerable<string>     outputVariables, 
                                         int                     maxThreads)
    {
        var argsBuilder = new StringBuilder();

        argsBuilder.Append("solve ");
        if (craft.ItemID != 0)
            argsBuilder.Append($"--item-id {craft.ItemID} ");
        else
            argsBuilder.Append($"--recipe-id {craft.RecipeID} ");

        argsBuilder.Append(craft.UnlockedManipulation ? "--manipulation " : "");
        argsBuilder.Append($"--level {craft.StatLevel} ");
        argsBuilder.Append($"--stats {craft.StatCraftsmanship} {craft.StatControl} {craft.StatCP} ");
        argsBuilder.Append($"--initial {craft.InitialQuality} ");

        if (config.EnsureReliability)
            argsBuilder.Append("--adversarial ");
        if (config.BackloadProgress)
            argsBuilder.Append("--backload-progress ");
        if (config.AllowHeartAndSoul)
            argsBuilder.Append("--heart-and-soul ");
        if (config.AllowQuickInnovation)
            argsBuilder.Append("--quick-innovation ");
        
        if (maxThreads > 0)
            argsBuilder.Append($"--threads {maxThreads} ");

        argsBuilder.Append($"--output-variables {string.Join(" ", outputVariables)}");

        return argsBuilder.ToString();
    }

    private static List<string>? ParseActionsOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        var cleanedOutput = output.Replace("[", "").Replace("]", "").Replace("\"", "").Trim();
        if (string.IsNullOrWhiteSpace(cleanedOutput)) return null;

        return cleanedOutput.Split([','], StringSplitOptions.RemoveEmptyEntries)
                            .Select(name => name.Trim())
                            .ToList();
    }
}
