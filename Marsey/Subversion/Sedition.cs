﻿using System.Reflection;
using HarmonyLib;
using Marsey.Game.Managers;
using Marsey.Handbreak;
using Marsey.Stealthsey;
using Marsey.Stealthsey.Reflection;

namespace Marsey.Subversion;

/// <summary>
/// Manages the subversion hide queue list
/// </summary>
public static class Sedition
{
    private static List<Assembly> _queue = new List<Assembly>();

    [HideLevelRequirement(HideLevel.Normal)]
    public static void Patch()
    {
        MethodInfo? Target = Helpers.GetMethod("Content.Client.Entry.EntryPoint", "Init");
        MethodInfo? Patch = Helpers.GetMethod(typeof(Sedition), "Prefix");
        Manual.Patch(Target, Patch, HarmonyPatchType.Prefix);
    }

    public static void Queue(Assembly subversion)
    {
        _queue.Add(subversion);
    }

    private static bool Prefix()
    {
        foreach (Assembly asm in _queue)
        {
            Hidesey.HidePatch(asm);
        }

        return true;
    }
}
