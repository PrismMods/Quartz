using Newtonsoft.Json.Linq;
using Quartz.IO;
using Quartz.IO.Interface;
using UnityEngine;
namespace Quartz.Features.Restriction;
public sealed class JudgementSection {
    public float Start = 0f;
    public float End = 100f;
    public bool Contains(float percent) =>
        percent >= Mathf.Min(Start, End) && percent <= Mathf.Max(Start, End);
    public JObject Serialize() => new() {
        [nameof(Start)] = Start,
        [nameof(End)] = End,
    };
    public static JudgementSection Deserialize(JToken token) {
        JudgementSection section = new();
        if(token == null) return section;
        section.Start = Mathf.Clamp(IOUtils.Read(token, nameof(Start), section.Start), 0f, 100f);
        section.End = Mathf.Clamp(IOUtils.Read(token, nameof(End), section.End), 0f, 100f);
        return section;
    }
}
public sealed class RestrictionSettings : ISettingsFile {
    public const int MaxSections = 16;
    public bool JRestrictEnabled = false;
    public int JRestrictMode = 1;
    public float JRestrictAccuracy = 96.6741943f;
    public int JRestrictAllowedMask = 0;
    public string JRestrictMessage = "Broke the judgement restriction!!";
    public bool JRestrictSectionsEnabled = false;
    public readonly List<JudgementSection> JRestrictSections = [];
    public bool DeathLimitEnabled = false;
    public bool MaxDeathsOn = true;
    public int MaxDeaths = 10;
    public bool MaxMissesOn = false;
    public int MaxMisses = 3;
    public bool MaxOverloadsOn = false;
    public int MaxOverloads = 3;
    public string DeathLimitMessage = "Exceeded death limit!!";
    public JToken Serialize() {
        return new JObject {
            [nameof(JRestrictEnabled)] = JRestrictEnabled,
            [nameof(JRestrictMode)] = JRestrictMode,
            [nameof(JRestrictAccuracy)] = JRestrictAccuracy,
            [nameof(JRestrictAllowedMask)] = JRestrictAllowedMask,
            [nameof(JRestrictMessage)] = JRestrictMessage,
            [nameof(JRestrictSectionsEnabled)] = JRestrictSectionsEnabled,
            [nameof(JRestrictSections)] =
                new JArray(JRestrictSections.Select(s => s.Serialize()).Cast<object>().ToArray()),
            [nameof(DeathLimitEnabled)] = DeathLimitEnabled,
            [nameof(MaxDeathsOn)] = MaxDeathsOn,
            [nameof(MaxDeaths)] = MaxDeaths,
            [nameof(MaxMissesOn)] = MaxMissesOn,
            [nameof(MaxMisses)] = MaxMisses,
            [nameof(MaxOverloadsOn)] = MaxOverloadsOn,
            [nameof(MaxOverloads)] = MaxOverloads,
            [nameof(DeathLimitMessage)] = DeathLimitMessage,
        };
    }
    public void Deserialize(JToken token) {
        JRestrictEnabled = IOUtils.Read(token, nameof(JRestrictEnabled), JRestrictEnabled);
        JRestrictMode = IOUtils.Read(token, nameof(JRestrictMode), JRestrictMode);
        JRestrictAccuracy = IOUtils.Read(token, nameof(JRestrictAccuracy), JRestrictAccuracy);
        JRestrictAllowedMask = IOUtils.Read(token, nameof(JRestrictAllowedMask), JRestrictAllowedMask);
        JRestrictMessage = IOUtils.Read(token, nameof(JRestrictMessage), JRestrictMessage);
        JRestrictSectionsEnabled = IOUtils.Read(token, nameof(JRestrictSectionsEnabled), JRestrictSectionsEnabled);
        JRestrictSections.Clear();
        if(token?[nameof(JRestrictSections)] is JArray sections) {
            foreach(JToken item in sections) {
                if(JRestrictSections.Count >= MaxSections) break;
                JRestrictSections.Add(JudgementSection.Deserialize(item));
            }
        }
        DeathLimitEnabled = IOUtils.Read(token, nameof(DeathLimitEnabled), DeathLimitEnabled);
        MaxDeathsOn = IOUtils.Read(token, nameof(MaxDeathsOn), MaxDeathsOn);
        MaxDeaths = IOUtils.Read(token, nameof(MaxDeaths), MaxDeaths);
        MaxMissesOn = IOUtils.Read(token, nameof(MaxMissesOn), MaxMissesOn);
        MaxMisses = IOUtils.Read(token, nameof(MaxMisses), MaxMisses);
        MaxOverloadsOn = IOUtils.Read(token, nameof(MaxOverloadsOn), MaxOverloadsOn);
        MaxOverloads = IOUtils.Read(token, nameof(MaxOverloads), MaxOverloads);
        DeathLimitMessage = IOUtils.Read(token, nameof(DeathLimitMessage), DeathLimitMessage);
    }
}
