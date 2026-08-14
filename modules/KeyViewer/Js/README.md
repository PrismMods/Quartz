# KeyViewer JavaScript plugins

Quartz runs a practical subset of the DM Note declarative plugin API inside
the ADOFAI KeyViewer. The engine is disabled by default. Open KeyViewer
settings, enable **JavaScript Plugins**, then import a `.js` or `.mjs` file.
Imported source is copied into `KeyViewer.json`; **Reload Plugin Files** reads
the original paths again during development.

Each enabled file gets an isolated Jint engine, automatic strict/function
scope, a 1 MiB source limit, and execution limits. No browser DOM, Node.js,
network API, or CLR bridge is exposed. A plugin is still code, so only import
files you trust.

## Supported API

| API | Support |
| --- | --- |
| `dmn.plugin.defineElement()` | One automatically mounted panel per declaration |
| `template(state, settings, { html, css, styleMap, t })` | Tagged HTML, interpolated child nodes/arrays, inline CSS |
| `onMount(context)` | `setState`, `getSettings`, `setAnchor`, `getAnchor`, `onHook`, `onSettingsChange`, `expose`, cleanup return |
| `dmn.keys.onKeyState()` / `onRawInput()` | Keyboard and mouse DOWN/UP events |
| `dmn.stats.get()` / `subscribe()` / `reset()` | `kps`, `kpsAvg`, `kpsMax`, and `total` |
| `dmn.plugin.storage` | Namespaced `get`, `set`, `remove`, `clear`, `keys`, `hasData`, `clearByPrefix` |
| Timers and logging | `setTimeout`, `setInterval`, clear functions, `console` |
| Rendering | Common block/flex/grid layout, text, fills, borders, and basic inline SVG |

Element setting schemas currently supply their declared default values; Quartz
does not yet generate a per-panel settings editor. Multiple user-created
instances, context-menu actions, remote fonts/assets, external stylesheets,
full browser CSS, and the rest of DM Note's editor APIs are not implemented.

## Minimal plugin

```js
// @id quartz-counter

dmn.plugin.defineElement({
  name: "Quartz Counter",
  estimatedSize: { width: 180, height: 72 },
  settings: {
    color: { type: "color", default: "#86efac", label: "Color" },
  },
  previewState: { count: 0 },
  template: (state, settings, { html }) => html`
    <div style="width: 100%; height: 100%; padding: 12px; border-radius: 8px;
                background: rgba(17,17,20,.9); color: ${settings.color};">
      Keys: ${state.count ?? 0}
    </div>
  `,
  onMount: ({ setState, onHook }) => {
    let count = 0;
    return onHook("key", ({ state }) => {
      if (state === "DOWN") setState({ count: ++count });
    });
  },
});
```

`// @id` follows DM Note's rule: lowercase letters, digits, hyphens, or
underscores within the first 20 lines. Without it, Quartz derives the ID from
the filename. Plugin IDs namespace persistent storage, so changing one makes
its old data inaccessible.
