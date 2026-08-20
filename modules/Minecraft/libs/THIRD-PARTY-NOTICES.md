# Third-party notices — Minecraft module browser engine

The Minecraft module embeds these unmodified runtime dependencies in its `.qmod`
so the out-of-process browser engine can be driven without loose DLLs:

- **VoltRpc 3.2.1**, Copyright (c) Voltstro, MIT.
  Source: https://github.com/Voltstro-Studios/VoltRpc
- **VoltstroStudios.UnityWebBrowser.Shared 2.2.8**, Copyright (c) Voltstro-Studios,
  MIT. Source: https://github.com/Voltstro-Studios/UnityWebBrowser

The browser engine itself is **not** shipped inside the `.qmod`. It is downloaded
on request from Voltstro's package registry and stored beside the Quartz settings
folder. That payload contains:

- **UnityWebBrowser CEF Engine 2.2.8**, Copyright (c) Voltstro-Studios, MIT.
  Source: https://github.com/Voltstro-Studios/UnityWebBrowser
- **Chromium Embedded Framework**, Copyright (c) Marshall A. Greenblatt,
  BSD 3-Clause. Source: https://bitbucket.org/chromiumembedded/cef
- **Chromium**, Copyright (c) The Chromium Authors, BSD 3-Clause and the licences
  of its bundled components, redistributed unchanged inside the engine bundle.
