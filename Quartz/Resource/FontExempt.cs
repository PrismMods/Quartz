using Quartz.Core;
using System.Reflection;
using UnityEngine;

using TMPro;

namespace Quartz.Resource;

// Marks a TMP text that picks its own font (e.g. the font dropdown's option
// rows, each rendered in the face it names) so FontManager.ApplyToAll leaves
// it alone when the global font changes.
public sealed class FontExempt : MonoBehaviour { }
