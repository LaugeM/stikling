// Serves the backups in the stikling-testdata folder to the dev server, so a browser check can
// restore one without a file picker. The folder sits next to the stikling checkout and is never
// in the repository, since it holds a real collection. STIKLING_TESTDATA points somewhere else.
//
// GET /           lists the files as JSON
// GET /<name>     one file from the folder
//
// Only pages on localhost may read it, and it only listens on this machine.

import { createServer } from "node:http";
import { execFileSync } from "node:child_process";
import { existsSync, readdirSync, createReadStream } from "node:fs";
import { dirname, join, basename } from "node:path";
import { fileURLToPath } from "node:url";

function findFolder() {
    if (process.env.STIKLING_TESTDATA) return process.env.STIKLING_TESTDATA;
    // The common git dir is the main checkout's .git, also when this runs in a worktree.
    const gitDir = execFileSync("git", ["rev-parse", "--path-format=absolute", "--git-common-dir"], { encoding: "utf8", cwd: dirname(fileURLToPath(import.meta.url)) }).trim();
    return join(dirname(dirname(gitDir)), "stikling-testdata");
}

const folder = findFolder();
if (!existsSync(folder)) {
    console.error(`No test data folder at ${folder}. Make a stikling-testdata folder next to the stikling checkout, or set STIKLING_TESTDATA.`);
    process.exit(1);
}

const port = Number(process.env.PORT ?? 5171);

const local = /^(localhost|127\.0\.0\.1)(:\d+)?$/;

function handle(req, res) {
    // The Host check stops a page on another site that points its own name at 127.0.0.1.
    if (!local.test(req.headers.host ?? "")) {
        res.writeHead(403).end();
        return;
    }
    const origin = req.headers.origin;
    if (origin?.startsWith("http://") && local.test(origin.slice("http://".length))) {
        res.setHeader("Access-Control-Allow-Origin", origin);
    }
    if (req.method !== "GET") {
        res.writeHead(405).end();
        return;
    }

    const files = readdirSync(folder, { withFileTypes: true }).filter(f => f.isFile()).map(f => f.name);
    const path = decodeURIComponent(new URL(req.url, "http://localhost").pathname.slice(1));

    if (path === "") {
        res.writeHead(200, { "Content-Type": "application/json" }).end(JSON.stringify(files));
        return;
    }
    // Only a file directly in the folder, by its exact name.
    if (path !== basename(path) || !files.includes(path)) {
        res.writeHead(404).end();
        return;
    }
    res.writeHead(200, { "Content-Type": path.endsWith(".zip") ? "application/zip" : "application/octet-stream" });
    createReadStream(join(folder, path)).on("error", () => res.destroy()).pipe(res);
}

createServer((req, res) => {
    try {
        handle(req, res);
    } catch {
        if (!res.headersSent) res.writeHead(400);
        res.end();
    }
}).listen(port, "127.0.0.1", () => console.log(`Serving ${folder} on http://localhost:${port}`));
