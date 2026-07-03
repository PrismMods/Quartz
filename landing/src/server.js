const http = require("node:http");
const fs = require("node:fs/promises");
const path = require("node:path");
const { URL } = require("node:url");

const publicDir = path.join(__dirname, "public");
const port = Number.parseInt(process.env.PORT || "3000", 10);

const contentTypes = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml; charset=utf-8",
  ".png": "image/png",
  ".ico": "image/x-icon"
};

const installSteps = {
  melonloader: [
    {
      type: "note",
      parts: [
        { text: "If on Mac, there is an " },
        { text: "auto installer", href: "https://github.com/sbrothers7/UMMInstall/releases/latest" },
        { text: " for your convenience." }
      ]
    },
    {
      type: "step",
      parts: [
        { text: "Download " },
        { text: "modlist.org app", href: "https://github.com/modlist-org/modlist_org_app/releases/latest" },
        { text: " and " },
        { text: "Quartz", href: "https://github.com/QuartzTeam/Quartz/releases/latest" },
        { text: "." }
      ]
    },
    { type: "step", text: "If not installed MelonLoader yet, install it using the modlist.org app." },
    {
      type: "note",
      text: "If on Mac, In the modlist.org app, press \"Copy Native Launch Options\" in the \"Installed\" tab and paste it into your Steam launch arguments."
    },
    { type: "step", text: "Press \"Install Mod From File\" then select the zip (Quartz.zip)." },
    { type: "step", text: "Done!" }
  ],
  unitymodmanager: [
    { type: "step", marker: "0", text: "First make sure you have UnityModManager set up for ADoFaI." },
    {
      type: "step",
      parts: [
        { text: "Download " },
        { text: "QuartzUmm.zip", code: true },
        { text: " from " },
        { text: "releases", href: "https://github.com/QuartzTeam/Quartz/releases/latest" },
        { text: "." }
      ]
    },
    {
      type: "step",
      parts: [
        { text: "In the UMM installer, use \"Install mod\" and pick " },
        { text: "QuartzUmm.zip", code: true },
        { text: " - or just simply drag the " },
        { text: "QuartzUmm.zip", code: true },
        { text: " into the drag zip box" }
      ]
    },
    { type: "step", text: "Done! Open the in-game menu with the mod's keybind (settings live there, not in the UMM panel)." }
  ]
};

const pageMeta = {
  product: "Quartz",
  description: "An all-in-one mod for A Dance of Fire and Ice.",
  loaders: ["melonloader", "unitymodmanager"],
  status: ["Open Source", "MelonLoader & UMM", "Actively Maintained"],
  links: {
    github: "https://github.com/sbrothers7/Quartz",
    discord: "#",
    download: "https://github.com/sbrothers7/Quartz/releases"
  }
};

function sendJson(res, statusCode, payload) {
  const body = JSON.stringify(payload);
  res.writeHead(statusCode, {
    "content-type": contentTypes[".json"],
    "content-length": Buffer.byteLength(body)
  });
  res.end(body);
}

function safeStaticPath(pathname) {
  const decoded = decodeURIComponent(pathname);
  if (decoded.includes("\0")) return null;
  const requested = decoded === "/" ? "/index.html" : decoded;
  const filePath = path.resolve(publicDir, `.${requested}`);
  return filePath === publicDir || filePath.startsWith(`${publicDir}${path.sep}`) ? filePath : null;
}

async function serveStatic(req, res, pathname) {
  const filePath = safeStaticPath(pathname);
  if (!filePath) {
    sendJson(res, 400, { error: "Invalid path" });
    return;
  }

  try {
    const file = await fs.readFile(filePath);
    res.writeHead(200, {
      "content-type": contentTypes[path.extname(filePath)] || "application/octet-stream",
      "cache-control": "no-cache"
    });
    res.end(file);
  } catch (error) {
    if (error && error.code === "ENOENT") {
      const fallback = await fs.readFile(path.join(publicDir, "index.html"));
      res.writeHead(200, { "content-type": contentTypes[".html"] });
      res.end(fallback);
      return;
    }
    sendJson(res, 500, { error: "Unable to read asset" });
  }
}

function handleApi(req, res, url) {
  if (url.pathname === "/api/meta") {
    sendJson(res, 200, pageMeta);
    return true;
  }

  if (url.pathname === "/api/install") {
    const loader = String(url.searchParams.get("loader") || "melonloader").toLowerCase();
    if (!Object.hasOwn(installSteps, loader)) {
      sendJson(res, 400, { error: "Unsupported loader", validLoaders: Object.keys(installSteps) });
      return true;
    }
    sendJson(res, 200, { loader, steps: installSteps[loader] });
    return true;
  }

  return false;
}

function createServer() {
  return http.createServer(async (req, res) => {
    if (!req.url || !req.method) {
      sendJson(res, 400, { error: "Bad request" });
      return;
    }

    if (req.method !== "GET" && req.method !== "HEAD") {
      sendJson(res, 405, { error: "Method not allowed" });
      return;
    }

    const url = new URL(req.url, "http://localhost");
    if (url.pathname.startsWith("/api/") && handleApi(req, res, url)) return;
    await serveStatic(req, res, url.pathname);
  });
}

if (require.main === module) {
  createServer().listen(port, () => {
    console.log(`Quartz landing app running at http://localhost:${port}`);
  });
}

module.exports = { createServer, installSteps, pageMeta, safeStaticPath };
