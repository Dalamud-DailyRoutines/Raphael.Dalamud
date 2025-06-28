using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using Raphael.Dalamud.Info;
using Raphael.Dalamud.Utility;

namespace Raphael.Dalamud;

internal class RaphaelBinding : IDisposable
{
    private static unsafe SolveArgs*                          args;
    private static        TaskCompletionSource<List<string>>? tcs;
    private static unsafe bool*                               Cancel;

    public unsafe RaphaelBinding()
    {
        if (args == null)
            args = (SolveArgs*)Marshal.AllocHGlobal(sizeof(SolveArgs));
    }

    internal async Task<List<string>?> GenerateSolutionAsync(
        RaphaelCraftState       craftState,
        RaphaelGenerationConfig config,
        int                     maxThreads        = 0,
        TimeSpan?               timeout           = null,
        CancellationToken       cancellationToken = default)
    {
        if (!LuminaGetter.TryGetRow(craftState.RecipeID, out Recipe recipeData))
            throw new Exception("Recipe not found / ");
        
        unsafe
        {
            *args = new SolveArgs
            {
                on_start            = &OnStart,
                on_finish           = &OnFinish,
                on_suggest_solution = &OnSuggestSolution,
                on_progress         = &OnProgress,
                on_log              = &OnLog,
                log_level           = LevelFilter.Info,
                thread_count        = (ushort)maxThreads,
                action_mask         = (ulong)ActionMask.All,
                progress            = (ushort)craftState.CraftProgress,
                quality             = (ushort)craftState.CraftQualityMax,
                base_progress       = (ushort)recipeData.GetBaseProgress((uint)craftState.StatCraftsmanship, (byte)craftState.CraftLevel),
                base_quality        = (ushort)recipeData.GetBaseQuality((uint)craftState.StatControl, (byte)craftState.CraftLevel),
                cp                  = (ushort)craftState.StatCP,
                durability          = (ushort)craftState.CraftDurability,
                // 设置无用 因为是 base_progress 和 base_quality 所需 参见 RecipeExtensions.GetBaseProgress
                job_level           = (byte)craftState.CraftLevel,
                adversarial         = false,
                backload_progress   = config.BackloadProgress
            };
            
            timeout ??= TimeSpan.FromMinutes(1);
            tcs     =   new TaskCompletionSource<List<string>>();
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.Token.Register(() =>
            {
                *Cancel = true;
                tcs.TrySetCanceled();
            });
            cts.CancelAfter(timeout.Value);
            
            Task.Run(() => { NativeMethods.Solve(args); }, cts.Token);
        }

        return await tcs.Task;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnLog(byte* arg1, nuint arg2) { }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnProgress(nuint obj) { }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnSuggestSolution(Action* arg1, nuint arg2)
    {
        /*
         * 当可能的手法
         */
        var list = new List<Action>();
        for (var i = 0u; i < arg2; i++) 
            list.Add(arg1[i]);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnFinish(Action* arg1, nuint arg2)
    {
        /*
         * 最后的手法
         */
        var list = new List<string>();
        for (var i = 0u; i < arg2; i++) list.Add(arg1[i].ToString());
        tcs?.SetResult(list);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnStart(bool* obj) => Cancel = obj;

    public void Dispose() { }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SolveArgs
    {
        public delegate* unmanaged[Cdecl]<bool*, void>          on_start;
        public delegate* unmanaged[Cdecl]<Action*, nuint, void> on_finish;
        public delegate* unmanaged[Cdecl]<Action*, nuint, void> on_suggest_solution;
        public delegate* unmanaged[Cdecl]<nuint, void>          on_progress;
        public delegate* unmanaged[Cdecl]<byte*, nuint, void>   on_log;
        public LevelFilter                                      log_level;
        public ushort                                           thread_count;
        public ulong                                            action_mask;
        public ushort                                           progress;
        public ushort                                           quality;
        public ushort                                           base_progress;
        public ushort                                           base_quality;
        public ushort                                           cp;
        public ushort                                           durability;
        public byte                                             job_level;

        [MarshalAs(UnmanagedType.U1)]
        public bool adversarial;

        [MarshalAs(UnmanagedType.U1)]
        public bool backload_progress;
    }

    [Flags]
    public enum ActionMask : ulong
    {
        BasicSynthesis     = 1,
        BasicTouch         = 1ul << Action.BasicTouch,
        MasterMend         = 1ul << Action.MasterMend,
        Observe            = 1ul << Action.Observe,
        TricksOfTheTrade   = 1ul << Action.TricksOfTheTrade,
        WasteNot           = 1ul << Action.WasteNot,
        Veneration         = 1ul << Action.Veneration,
        StandardTouch      = 1ul << Action.StandardTouch,
        GreatStrides       = 1ul << Action.GreatStrides,
        Innovation         = 1ul << Action.Innovation,
        WasteNot2          = 1ul << Action.WasteNot2,
        ByregotsBlessing   = 1ul << Action.ByregotsBlessing,
        PreciseTouch       = 1ul << Action.PreciseTouch,
        MuscleMemory       = 1ul << Action.MuscleMemory,
        CarefulSynthesis   = 1ul << Action.CarefulSynthesis,
        Manipulation       = 1ul << Action.Manipulation,
        PrudentTouch       = 1ul << Action.PrudentTouch,
        AdvancedTouch      = 1ul << Action.AdvancedTouch,
        Reflect            = 1ul << Action.Reflect,
        PreparatoryTouch   = 1ul << Action.PreparatoryTouch,
        Groundwork         = 1ul << Action.Groundwork,
        DelicateSynthesis  = 1ul << Action.DelicateSynthesis,
        IntensiveSynthesis = 1ul << Action.IntensiveSynthesis,
        TrainedEye         = 1ul << Action.TrainedEye,
        HeartAndSoul       = 1ul << Action.HeartAndSoul,
        PrudentSynthesis   = 1ul << Action.PrudentSynthesis,
        TrainedFinesse     = 1ul << Action.TrainedFinesse,
        RefinedTouch       = 1ul << Action.RefinedTouch,
        QuickInnovation    = 1ul << Action.QuickInnovation,
        ImmaculateMend     = 1ul << Action.ImmaculateMend,
        TrainedPerfection  = 1ul << Action.TrainedPerfection,
        All                = ulong.MaxValue
    }

    public enum Action : byte
    {
        BasicSynthesis,
        BasicTouch,
        MasterMend,
        Observe,
        TricksOfTheTrade,
        WasteNot,
        Veneration,
        StandardTouch,
        GreatStrides,
        Innovation,
        WasteNot2,
        ByregotsBlessing,
        PreciseTouch,
        MuscleMemory,
        CarefulSynthesis,
        Manipulation,
        PrudentTouch,
        AdvancedTouch,
        Reflect,
        PreparatoryTouch,
        Groundwork,
        DelicateSynthesis,
        IntensiveSynthesis,
        TrainedEye,
        HeartAndSoul,
        PrudentSynthesis,
        TrainedFinesse,
        RefinedTouch,
        QuickInnovation,
        ImmaculateMend,
        TrainedPerfection
    }

    public enum LevelFilter : byte
    {
        Off,
        Error,
        Warn,
        Info,
        Debug,
        Trace
    }
    
    private static unsafe class NativeMethods
    {
        private const string DllName = "raphael_bindings";
        
        [DllImport(DllName, EntryPoint = "solve", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Solve(SolveArgs* args);

        static NativeMethods()
        {
            var path = Path.Combine(Plugin.PI.AssemblyLocation.DirectoryName!, "raphael_bindings.dll");
            NativeLibrary.Load(path);
        }
    }
}
