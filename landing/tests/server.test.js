const test = require("node:test");
const assert = require("node:assert/strict");
const { createServer, safeStaticPath } = require("../src/server");

function listen(server) {
  return new Promise((resolve) => {
    server.listen(0, "127.0.0.1", () => resolve(server.address().port));
  });
}

async function request(pathname) {
  const server = createServer();
  const port = await listen(server);
  try {
    const response = await fetch(`http://127.0.0.1:${port}${pathname}`);
    return {
      status: response.status,
      contentType: response.headers.get("content-type"),
      body: await response.text()
    };
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

test("serves the landing page", async () => {
  const response = await request("/");
  assert.equal(response.status, 200);
  assert.match(response.contentType, /text\/html/);
  assert.match(response.body, /The all-in-one mod/);
});

test("returns install steps for a valid loader", async () => {
  const response = await request("/api/install?loader=unitymodmanager");
  const body = JSON.parse(response.body);
  assert.equal(response.status, 200);
  assert.equal(body.loader, "unitymodmanager");
  assert.equal(body.steps.length, 5);
  assert.equal(body.steps[0].marker, "0");
  assert.equal(body.steps[1].parts[1].text, "QuartzUmm.zip");
  assert.equal(body.steps[4].type, "warning");
});

test("rejects unsupported loader input", async () => {
  const response = await request("/api/install?loader=../../bad");
  const body = JSON.parse(response.body);
  assert.equal(response.status, 400);
  assert.equal(body.error, "Unsupported loader");
});

test("blocks path traversal for static assets", () => {
  assert.equal(safeStaticPath("/../../package.json"), null);
});
