using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Quartz.Core;
using UnityEngine;
namespace Quartz.Features.TileArc;
[HarmonyPatch(typeof(FloorMesh), "SmallestAngleBetweenTwoAngles")]
internal static class CornerArcRadiusPatch {
    private static readonly MethodInfo Override =
        AccessTools.Method(typeof(TileArc), nameof(TileArc.ApplyCornerArcOverride));
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions, ILGenerator generator
    ) {
        List<CodeInstruction> codes = instructions.ToList();
        LocalBuilder result = generator.DeclareLocal(typeof(float));
        for(int i = 0; i < codes.Count; i++) {
            if(codes[i].opcode != OpCodes.Ret) continue;
            CodeInstruction store = new CodeInstruction(OpCodes.Stloc, result).MoveLabelsFrom(codes[i]);
            codes.Insert(i, store);
            codes.Insert(i + 1, new CodeInstruction(OpCodes.Ldloc, result));
            codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_1));
            codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldarg_2));
            codes.Insert(i + 4, new CodeInstruction(OpCodes.Call, Override));
            return codes;
        }
        MainCore.Log.Wrn("[TileArc] FloorMesh.SmallestAngleBetweenTwoAngles has no ret; corner radius left vanilla.");
        return codes;
    }
}
[HarmonyPatch(typeof(FloorMesh), "GetPositions")]
internal static class AllAngleArcCornersPatch {
    private static readonly MethodInfo Gate = AccessTools.Method(typeof(TileArc), nameof(TileArc.ArcGate));
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
        List<CodeInstruction> codes = instructions.ToList();
        int anchor = -1;
        for(int i = 0; i < codes.Count - 1; i++) {
            if(!IsCallTo(codes[i], "ModAngle360")) continue;
            if(codes[i + 1].opcode == OpCodes.Stfld
            && codes[i + 1].operand is FieldInfo stored && stored.Name == "angleDifference") {
                anchor = i;
                break;
            }
        }
        int gateIdx = -1;
        for(int i = anchor + 2; anchor >= 0 && i < codes.Count - 2; i++) {
            if(codes[i].opcode != OpCodes.Ldfld
            || codes[i].operand is not FieldInfo loaded || loaded.Name != "angleDifference")
                continue;
            if(codes[i + 1].opcode != OpCodes.Ldc_R4 || codes[i + 1].operand is not float threshold) continue;
            if(Mathf.Approximately(threshold, Mathf.PI)) continue;
            gateIdx = i + 1;
            break;
        }
        if(anchor < 0 || gateIdx < 0) {
            MainCore.Log.Wrn("[TileArc] FloorMesh.GetPositions corner-arc gate not found; obtuse corners left vanilla.");
            return codes;
        }
        codes.Insert(gateIdx + 1, new CodeInstruction(OpCodes.Call, Gate));
        return codes;
    }
    private static bool IsCallTo(CodeInstruction instruction, string methodName) {
        if(instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) return false;
        return instruction.operand is MethodInfo m
            && m.DeclaringType == typeof(FloorMesh)
            && m.Name == methodName;
    }
}
