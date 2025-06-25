using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Raphael.Dalamud.Info;

internal static class ActionMap
{
    private static readonly Dictionary<string, Dictionary<CrafterJob, uint>> ActionDatabase;

    static ActionMap() => 
        ActionDatabase = BuildHardcodedDatabase();

    private static unsafe CrafterJob GetJob()
    {
        var localPlayer = Control.GetLocalPlayer();
        if (localPlayer == null)
            return CrafterJob.Unknown;
        
        switch (localPlayer->ClassJob)
        {
            case 8:
                return CrafterJob.CRP;
            case 9:
                return CrafterJob.BSM;
            case 10:
                return CrafterJob.ARM;
            case 11:
                return CrafterJob.GSM;
            case 12:
                return CrafterJob.LTW;
            case 13:
                return CrafterJob.WVR;
            case 14:
                return CrafterJob.ALC;
            case 15:
                return CrafterJob.CUL;
            default:
                return CrafterJob.Unknown;
        }
    }
    
    public static uint GetActionID(string actionName)
    {
        var job = GetJob();
        
        if (ActionDatabase.TryGetValue(actionName, out var jobIds) && jobIds.TryGetValue(job, out var actionID))
            return actionID;

        return 0;
    }

    private static Dictionary<string, Dictionary<CrafterJob, uint>> BuildHardcodedDatabase()
    {
        var db = new Dictionary<string, Dictionary<CrafterJob, uint>>(StringComparer.OrdinalIgnoreCase)
        {
            ["BasicSynthesis"]     = new() { { CrafterJob.CRP, 100001 }, { CrafterJob.BSM, 100015 }, { CrafterJob.ARM, 100030 }, { CrafterJob.GSM, 100075 }, { CrafterJob.LTW, 100045 }, { CrafterJob.WVR, 100060 }, { CrafterJob.ALC, 100090 }, { CrafterJob.CUL, 100105 } },
            ["BasicTouch"]         = new() { { CrafterJob.CRP, 100002 }, { CrafterJob.BSM, 100016 }, { CrafterJob.ARM, 100031 }, { CrafterJob.GSM, 100076 }, { CrafterJob.LTW, 100046 }, { CrafterJob.WVR, 100061 }, { CrafterJob.ALC, 100091 }, { CrafterJob.CUL, 100106 } },
            ["MasterMend"]         = new() { { CrafterJob.CRP, 100003 }, { CrafterJob.BSM, 100017 }, { CrafterJob.ARM, 100032 }, { CrafterJob.GSM, 100077 }, { CrafterJob.LTW, 100047 }, { CrafterJob.WVR, 100062 }, { CrafterJob.ALC, 100092 }, { CrafterJob.CUL, 100107 } },
            ["Observe"]            = new() { { CrafterJob.CRP, 100010 }, { CrafterJob.BSM, 100023 }, { CrafterJob.ARM, 100040 }, { CrafterJob.GSM, 100082 }, { CrafterJob.LTW, 100053 }, { CrafterJob.WVR, 100070 }, { CrafterJob.ALC, 100099 }, { CrafterJob.CUL, 100113 } },
            ["TricksOfTheTrade"]   = new() { { CrafterJob.CRP, 100371 }, { CrafterJob.BSM, 100372 }, { CrafterJob.ARM, 100373 }, { CrafterJob.GSM, 100374 }, { CrafterJob.LTW, 100375 }, { CrafterJob.WVR, 100376 }, { CrafterJob.ALC, 100377 }, { CrafterJob.CUL, 100378 } },
            ["WasteNot"]           = new() { { CrafterJob.CRP, 4631 }, { CrafterJob.BSM, 4632 }, { CrafterJob.ARM, 4633 }, { CrafterJob.GSM, 4634 }, { CrafterJob.LTW, 4635 }, { CrafterJob.WVR, 4636 }, { CrafterJob.ALC, 4637 }, { CrafterJob.CUL, 4638 } },
            ["Veneration"]         = new() { { CrafterJob.CRP, 19297 }, { CrafterJob.BSM, 19298 }, { CrafterJob.ARM, 19299 }, { CrafterJob.GSM, 19300 }, { CrafterJob.LTW, 19301 }, { CrafterJob.WVR, 19302 }, { CrafterJob.ALC, 19303 }, { CrafterJob.CUL, 19304 } },
            ["StandardTouch"]      = new() { { CrafterJob.CRP, 100004 }, { CrafterJob.BSM, 100018 }, { CrafterJob.ARM, 100034 }, { CrafterJob.GSM, 100078 }, { CrafterJob.LTW, 100048 }, { CrafterJob.WVR, 100064 }, { CrafterJob.ALC, 100093 }, { CrafterJob.CUL, 100109 } },
            ["GreatStrides"]       = new() { { CrafterJob.CRP, 260 }, { CrafterJob.BSM, 261 }, { CrafterJob.ARM, 262 }, { CrafterJob.GSM, 263 }, { CrafterJob.LTW, 265 }, { CrafterJob.WVR, 264 }, { CrafterJob.ALC, 266 }, { CrafterJob.CUL, 267 } },
            ["Innovation"]         = new() { { CrafterJob.CRP, 19004 }, { CrafterJob.BSM, 19005 }, { CrafterJob.ARM, 19006 }, { CrafterJob.GSM, 19007 }, { CrafterJob.LTW, 19008 }, { CrafterJob.WVR, 19009 }, { CrafterJob.ALC, 19010 }, { CrafterJob.CUL, 19011 } },
            ["FinalAppraisal"]     = new() { { CrafterJob.CRP, 19012 }, { CrafterJob.BSM, 19013 }, { CrafterJob.ARM, 19014 }, { CrafterJob.GSM, 19015 }, { CrafterJob.LTW, 19016 }, { CrafterJob.WVR, 19017 }, { CrafterJob.ALC, 19018 }, { CrafterJob.CUL, 19019 } },
            ["WasteNot2"]          = new() { { CrafterJob.CRP, 4639 }, { CrafterJob.BSM, 4640 }, { CrafterJob.ARM, 4641 }, { CrafterJob.GSM, 4642 }, { CrafterJob.LTW, 4643 }, { CrafterJob.WVR, 4644 }, { CrafterJob.ALC, 19002 }, { CrafterJob.CUL, 19003 } },
            ["ByregotsBlessing"]   = new() { { CrafterJob.CRP, 100339 }, { CrafterJob.BSM, 100340 }, { CrafterJob.ARM, 100341 }, { CrafterJob.GSM, 100342 }, { CrafterJob.LTW, 100343 }, { CrafterJob.WVR, 100344 }, { CrafterJob.ALC, 100345 }, { CrafterJob.CUL, 100346 } },
            ["PreciseTouch"]       = new() { { CrafterJob.CRP, 100128 }, { CrafterJob.BSM, 100129 }, { CrafterJob.ARM, 100130 }, { CrafterJob.GSM, 100131 }, { CrafterJob.LTW, 100132 }, { CrafterJob.WVR, 100133 }, { CrafterJob.ALC, 100134 }, { CrafterJob.CUL, 100135 } },
            ["MuscleMemory"]       = new() { { CrafterJob.CRP, 100379 }, { CrafterJob.BSM, 100380 }, { CrafterJob.ARM, 100381 }, { CrafterJob.GSM, 100382 }, { CrafterJob.LTW, 100383 }, { CrafterJob.WVR, 100384 }, { CrafterJob.ALC, 100385 }, { CrafterJob.CUL, 100386 } },
            ["CarefulSynthesis"]   = new() { { CrafterJob.CRP, 100203 }, { CrafterJob.BSM, 100204 }, { CrafterJob.ARM, 100205 }, { CrafterJob.GSM, 100206 }, { CrafterJob.LTW, 100207 }, { CrafterJob.WVR, 100208 }, { CrafterJob.ALC, 100209 }, { CrafterJob.CUL, 100210 } },
            ["Manipulation"]       = new() { { CrafterJob.CRP, 4574 }, { CrafterJob.BSM, 4575 }, { CrafterJob.ARM, 4576 }, { CrafterJob.GSM, 4577 }, { CrafterJob.LTW, 4578 }, { CrafterJob.WVR, 4579 }, { CrafterJob.ALC, 4580 }, { CrafterJob.CUL, 4581 } },
            ["PrudentTouch"]       = new() { { CrafterJob.CRP, 100227 }, { CrafterJob.BSM, 100228 }, { CrafterJob.ARM, 100229 }, { CrafterJob.GSM, 100230 }, { CrafterJob.LTW, 100231 }, { CrafterJob.WVR, 100232 }, { CrafterJob.ALC, 100233 }, { CrafterJob.CUL, 100234 } },
            ["AdvancedTouch"]      = new() { { CrafterJob.CRP, 100411 }, { CrafterJob.BSM, 100412}, { CrafterJob.ARM, 100413 }, { CrafterJob.GSM, 100414 }, { CrafterJob.LTW, 100415 }, { CrafterJob.WVR, 100416 }, { CrafterJob.ALC, 100417 }, { CrafterJob.CUL, 100418 } },
            ["Reflect"]            = new() { { CrafterJob.CRP, 100387 }, { CrafterJob.BSM, 100388 }, { CrafterJob.ARM, 100389 }, { CrafterJob.GSM, 100390 }, { CrafterJob.LTW, 100391 }, { CrafterJob.WVR, 100392 }, { CrafterJob.ALC, 100393 }, { CrafterJob.CUL, 100394 } },
            ["PreparatoryTouch"]   = new() { { CrafterJob.CRP, 100299 }, { CrafterJob.BSM, 100300 }, { CrafterJob.ARM, 100301 }, { CrafterJob.GSM, 100302 }, { CrafterJob.LTW, 100303 }, { CrafterJob.WVR, 100304 }, { CrafterJob.ALC, 100305 }, { CrafterJob.CUL, 100306 } },
            ["Groundwork"]         = new() { { CrafterJob.CRP, 100403 }, { CrafterJob.BSM, 100404 }, { CrafterJob.ARM, 100405 }, { CrafterJob.GSM, 100406 }, { CrafterJob.LTW, 100407 }, { CrafterJob.WVR, 100408 }, { CrafterJob.ALC, 100409 }, { CrafterJob.CUL, 100410 } },
            ["DelicateSynthesis"]  = new() { { CrafterJob.CRP, 100323 }, { CrafterJob.BSM, 100324 }, { CrafterJob.ARM, 100325 }, { CrafterJob.GSM, 100326 }, { CrafterJob.LTW, 100327 }, { CrafterJob.WVR, 100328 }, { CrafterJob.ALC, 100329 }, { CrafterJob.CUL, 100330 } },
            ["IntensiveSynthesis"] = new() { { CrafterJob.CRP, 100315 }, { CrafterJob.BSM, 100316 }, { CrafterJob.ARM, 100317 }, { CrafterJob.GSM, 100318 }, { CrafterJob.LTW, 100319 }, { CrafterJob.WVR, 100320 }, { CrafterJob.ALC, 100321 }, { CrafterJob.CUL, 100322 } },
            ["TrainedEye"]         = new() { { CrafterJob.CRP, 100283 }, { CrafterJob.BSM, 100284 }, { CrafterJob.ARM, 100285 }, { CrafterJob.GSM, 100286 }, { CrafterJob.LTW, 100287 }, { CrafterJob.WVR, 100288 }, { CrafterJob.ALC, 100289 }, { CrafterJob.CUL, 100230 } },
            ["HeartAndSoul"]       = new() { { CrafterJob.CRP, 100419 }, { CrafterJob.BSM, 100420 }, { CrafterJob.ARM, 100421 }, { CrafterJob.GSM, 100422 }, { CrafterJob.LTW, 100423 }, { CrafterJob.WVR, 100424 }, { CrafterJob.ALC, 100425 }, { CrafterJob.CUL, 100426 } },
            ["PrudentSynthesis"]   = new() { { CrafterJob.CRP, 100427 }, { CrafterJob.BSM, 100428 }, { CrafterJob.ARM, 100429 }, { CrafterJob.GSM, 100430 }, { CrafterJob.LTW, 100431 }, { CrafterJob.WVR, 100432 }, { CrafterJob.ALC, 100433 }, { CrafterJob.CUL, 100434 } },
            ["TrainedFinesse"]     = new() { { CrafterJob.CRP, 100435 }, { CrafterJob.BSM, 100436 }, { CrafterJob.ARM, 100437 }, { CrafterJob.GSM, 100438 }, { CrafterJob.LTW, 100439 }, { CrafterJob.WVR, 100440 }, { CrafterJob.ALC, 100441 }, { CrafterJob.CUL, 100442 } },
            ["QuickInnovation"]    = new() { { CrafterJob.CRP, 100459 }, { CrafterJob.BSM, 100460 }, { CrafterJob.ARM, 100461 }, { CrafterJob.GSM, 100462 }, { CrafterJob.LTW, 100463 }, { CrafterJob.WVR, 100464 }, { CrafterJob.ALC, 100465 }, { CrafterJob.CUL, 100466 } },
            ["ImmaculateMend"]     = new() { { CrafterJob.CRP, 100467 }, { CrafterJob.BSM, 100468 }, { CrafterJob.ARM, 100469 }, { CrafterJob.GSM, 100470 }, { CrafterJob.LTW, 100471 }, { CrafterJob.WVR, 100472 }, { CrafterJob.ALC, 100473 }, { CrafterJob.CUL, 100474 } },
            ["TrainedPerfection"]  = new() { { CrafterJob.CRP, 100475 }, { CrafterJob.BSM, 100476 }, { CrafterJob.ARM, 100477 }, { CrafterJob.GSM, 100478 }, { CrafterJob.LTW, 100479 }, { CrafterJob.WVR, 100480 }, { CrafterJob.ALC, 100481 }, { CrafterJob.CUL, 100482 } },
            ["RefinedTouch"]       = new() { { CrafterJob.CRP, 100443 }, { CrafterJob.BSM, 100444 }, { CrafterJob.ARM, 100445 }, { CrafterJob.GSM, 100446 }, { CrafterJob.LTW, 100447 }, { CrafterJob.WVR, 100448 }, { CrafterJob.ALC, 100449 }, { CrafterJob.CUL, 100450 } }
        };

        return db;
    }
}
