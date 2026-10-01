using Assets.Scripts;
using Assets.Scripts.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DeepCoreMods.OxygenReserve
{
    internal static class StationeersKeybind
    {
        public static KeyItem LearnReferenceKeyItem { get; private set; }

        public const string GroupName =
            "DeepCore Mods - Oxygen Reserve";

        public const string KeyName =
            "Learn Full Tank Reference";

        public static void Register()
        {
            try
            {
                Plugin.Log.LogInfo(
                    "Registering Oxygen Reserve keybinding...");

                if (KeyManager.KeyItemLookup == null ||
                    KeyManager.AllKeys == null)
                {
                    Plugin.Log.LogWarning(
                        "KeyManager collections are not available yet.");

                    return;
                }

                ControlsGroup modGroup =
                    ControlsGroup.AllControlGroups.Find(
                        g => g.Name == GroupName);

                if (modGroup == null)
                {
                    modGroup =
                        new ControlsGroup(GroupName);

                    Plugin.Log.LogInfo(
                        $"Created ControlsGroup '{GroupName}'.");
                }

                KeyItem learnKey;

                if (!KeyManager.KeyItemLookup.TryGetValue(
                        KeyName,
                        out learnKey))
                {
                    // F7 is the default only.
                    // Players can rebind this through Stationeers' Controls menu.

                    learnKey =
                        new KeyItem(
                            KeyName,
                            KeyCode.F7,
                            false);

                    KeyManager.KeyItemLookup[KeyName] =
                        learnKey;

                    if (!KeyManager.AllKeys.Contains(learnKey))
                    {
                        KeyManager.AllKeys.Add(learnKey);
                    }

                    Plugin.Log.LogInfo(
                        "Created Oxygen Reserve reference-learning KeyItem.");
                }

                LearnReferenceKeyItem =
                    learnKey;

                if (!modGroup.KeyItems.Contains(learnKey))
                {
                    modGroup.KeyItems.Add(learnKey);
                }

                FieldInfo lookupField =
                    typeof(KeyManager).GetField(
                        "_controlsGroupLookup",
                        BindingFlags.Static |
                        BindingFlags.NonPublic);

                if (lookupField != null)
                {
                    Dictionary<string, ControlsGroup> lookup =
                        lookupField.GetValue(null)
                        as Dictionary<string, ControlsGroup>;

                    if (lookup != null)
                    {
                        lookup[KeyName] =
                            modGroup;
                    }
                }

                ControlsAssignment.RefreshState();

                Plugin.Log.LogInfo(
                    "Oxygen Reserve keybinding registration complete.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError(
                    "Oxygen Reserve keybinding registration failed: " +
                    ex);
            }
        }
    }
}