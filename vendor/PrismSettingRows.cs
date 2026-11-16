using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PrismLib.UI.Toolkit;
using UnityEngine;

namespace PrismLib.Bootstrap
{
    /// <summary>
    /// Prism · Settings rows for a mod that draws the shared settings window: PrismLib's setting schema
    /// converted to PrismLib.UI's own row type (PrismLib.UI does not reference PrismLib.dll), and the
    /// keybind capture those rows start. Copy this file into the mod unchanged, beside PrismBootstrap.cs;
    /// call Tick every frame the mod polls Prism input, Cancel when the mod switches off, and Rows only
    /// once PrismLib is Available. Nothing here but Rows names a PrismLib.dll type.
    /// </summary>
    internal static class PrismSettingRows
    {
#pragma warning disable 0649   // a mod sets only the hooks it needs
        /// <summary>Asked when a capture starts, with the cancel to call if something else takes the
        /// keyboard; false refuses the capture. Null: always granted.</summary>
        internal static Func<Action, bool> Claim;
        /// <summary>Told when a capture ends, taken or cancelled (the other half of Claim).</summary>
        internal static Action Release;
        /// <summary>Which keys a capture may take. Null: any key but the mouse and joystick buttons.</summary>
        internal static Func<KeyCode, bool> Usable;
#pragma warning restore 0649

        private static Action<string> _capture;
        private static readonly Action CancelOne = Cancel;   // cached: handed to Claim on every capture
        private static readonly KeyCode[] Keys = (KeyCode[])Enum.GetValues(typeof(KeyCode));

        /// <summary>A capture owns the keyboard: no window should open with its key.</summary>
        internal static bool Capturing { get { return _capture != null; } }

        /// <summary>Keybind capture: the next usable key wins, Escape cancels. Polled because Tab, the arrows
        /// and Escape are worth binding and are eaten as navigation inside a panel.</summary>
        internal static void Tick()
        {
            if (_capture == null) return;
            if (!Ui.AnyWindowOpen) { Cancel(); return; }   // its window closed under it: the next key is not a bind
            if (!Input.anyKeyDown) return;
            for (int i = 0; i < Keys.Length; i++)
            {
                KeyCode k = Keys[i];
                if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) continue;
                if (Usable != null ? !Usable(k) : k >= KeyCode.JoystickButton0) continue;
                if (!Input.GetKeyDown(k)) continue;
                Action<string> done = _capture;
                Cancel();
                if (k == KeyCode.Escape) Ui.ConsumeEscape();   // the cancel must not also close the window this frame
                done(k == KeyCode.Escape ? null : k.ToString());
                return;
            }
        }

        internal static void Cancel()
        {
            if (_capture == null) return;
            _capture = null;
            if (Release != null) Release();
        }

        /// <summary>The row's capture hook: the panel asks for a capture, Tick polls raw input, the mod's own
        /// setter takes the key, and the row is told what to show. Delegates, not the setting entry: the
        /// closure keeps no PrismLib.dll type.</summary>
        internal static Action<Action<string>> Capturer(Func<object> get, Action<object> set)
        {
            return done =>
            {
                if (Claim != null && !Claim(CancelOne)) return;
                _capture = name =>
                {
                    if (name != null && set != null) set(name);
                    done(get != null ? Convert.ToString(get()) : name);
                };
            };
        }

        /// <summary>Every mod's described settings as the panel's rows. Only once PrismLib is Available.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static IEnumerable<SettingRow> Rows()
        {
            var rows = new List<SettingRow>();
            foreach (KeyValuePair<string, PrismLib.SettingEntry> kv in PrismLib.Settings.Search(""))
            {
                PrismLib.SettingEntry e = kv.Value;
                rows.Add(new SettingRow
                {
                    Owner = kv.Key, Page = e.Page, Group = e.Group, Label = e.Label, Tooltip = e.Tooltip,
                    Kind = Kind(e.Kind), Get = e.Get, Set = e.Set,
                    Capture = e.Kind == PrismLib.SettingKind.Key ? Capturer(e.Get, e.Set) : null,
                    Min = (float)e.Min, Max = (float)e.Max, Options = e.Options, ActionLabel = e.ActionLabel,
                });
            }
            return rows;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static SettingControl Kind(PrismLib.SettingKind k)
        {
            switch (k)
            {
                case PrismLib.SettingKind.Int: return SettingControl.Int;
                case PrismLib.SettingKind.Float: return SettingControl.Float;
                case PrismLib.SettingKind.Enum: return SettingControl.Choice;
                case PrismLib.SettingKind.Text: return SettingControl.Text;
                case PrismLib.SettingKind.Action: return SettingControl.Action;
                case PrismLib.SettingKind.Colour: return SettingControl.Colour;
                case PrismLib.SettingKind.Key: return SettingControl.Key;
                default: return SettingControl.Bool;
            }
        }
    }
}
