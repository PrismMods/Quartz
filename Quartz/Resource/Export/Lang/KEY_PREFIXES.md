# Localization key prefixes

`en-US.json` / `ko-KR.json` are plain JSON (parsed with `Newtonsoft.Json.JObject.Parse`
in `Quartz/Localization/Translator.cs`), so they can't carry comments. This file maps
each opaque key prefix to the feature it belongs to, for anyone grepping the lang files.

Prefix -> feature (source of truth: the C# owning the setting / building the UI section):

| Prefix    | Feature                              | Where it's defined / built |
|-----------|---------------------------------------|-----------------------------|
| `AD_`     | Auto Deafen (Discord)                 | `Features/AutoDeafen`, built in `PageGameplay.cs` |
| `DL_`     | Death Limit                           | `Features/Restriction` (`RestrictionSettings.DeathLimit*`), built in `PageGameplay.cs` |
| `FXRM_`   | Effect Remover                        | `Features/EffectRemover`, built in `PageVisuals.cs` |
| `JR_`     | Judgement Restriction                 | `Features/Restriction` (`RestrictionSettings.JRestrict*`), built in `PageGameplay.cs` |
| `JPOP_`   | Hide Judgements (judgement popup mask)| `Features/Judgement/JudgementPopupHiderSettings`, built in `PageVisuals.cs` |
| `KCB_`    | Keyboard Chatter Blocker              | `Features/ChatterBlocker`, built in `PageGameplay.cs` |
| `KL_`     | Key Limiter                           | `Features/KeyLimiter`, built in `PageGameplay.cs` |
| `PCOL_`   | Planet Colors                         | `Features/PlanetColors`, built in `PageVisuals.cs` |
| `TW_`     | Visual Tweaks                         | `Features/Tweaks`, built in `PageVisuals.cs` |
| `UIH_`    | UI Hiding                             | `Features/UiHider`, built in `PageVisuals.cs` |

Notes:
- `DL_` and `JR_` both live on the single `Restriction` feature/settings class under the hood,
  but are shown as two separate UI sections ("Death Limit" and "Judgement Restriction"), each
  with its own key prefix.
- These prefixes are legacy/inconsistent with fuller-word prefixes used elsewhere in the file
  (e.g. `COMBO_`, `KEYVIEWER_`, `PROGRESSBAR_`, `PANEL_`). They are intentionally **not** renamed
  here — renaming a live localization key is a wide-reaching, riskier change than documenting it.
