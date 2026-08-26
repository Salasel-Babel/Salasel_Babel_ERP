// ─────────────────────────────────────────────────────────────────────────────
// خادم ثابت + وسيط عكسي — للتشغيل المحلي وحده.
//
// وهو **بديل nginx في المسار المحلي فقط**، ومكتوب ليطابق سلوكه في نقطتين هما
// كل ما تعتمد عليه الواجهة (deploy/nginx.conf):
//   1. ‏/api و/health يذهبان إلى الخلفية على الأصل نفسه — فلا CORS ولا مِنشأ ثانٍ.
//   2. أي مسار آخر لا يقابله ملف يُخدَم بـindex.html — تطبيق صفحة واحدة بموجّه.
//
// ولماذا يوجد أصلاً: التحقّق من الحزمة يجب أن يكون ممكناً على جهاز **بلا عفريت
// حاويات**. ولا يعمل هذا الملف في أي مسار نشر، ولا يُنسخ إلى أي صورة.
// ─────────────────────────────────────────────────────────────────────────────
import { createServer } from "node:http";
import { readFile, stat } from "node:fs/promises";
import { extname, join, normalize } from "node:path";

const root = process.env.BABEL_WEB_ROOT ?? "web/dist";
const api = process.env.BABEL_API ?? "http://127.0.0.1:5080";
const port = Number(process.env.BABEL_WEB_PORT ?? 5173);

const types = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".svg": "image/svg+xml",
  ".map": "application/json; charset=utf-8",
  ".woff2": "font/woff2",
  ".png": "image/png",
  ".ico": "image/x-icon",
};

async function send(response, path) {
  const body = await readFile(path);
  response.writeHead(200, {
    "content-type": types[extname(path)] ?? "application/octet-stream",
    "cache-control": extname(path) === ".html" ? "no-cache" : "public, max-age=3600",
  });
  response.end(body);
}

createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", "http://localhost");

  if (url.pathname.startsWith("/api/") || url.pathname === "/health") {
    const chunks = [];
    for await (const chunk of request) chunks.push(chunk);

    const headers = { ...request.headers };
    delete headers.host;
    delete headers["content-length"];

    const upstream = await fetch(api + url.pathname + url.search, {
      method: request.method,
      headers,
      body: chunks.length > 0 ? Buffer.concat(chunks) : undefined,
    });

    response.writeHead(upstream.status, {
      "content-type": upstream.headers.get("content-type") ?? "application/json",
    });
    response.end(Buffer.from(await upstream.arrayBuffer()));
    return;
  }

  // منع الخروج من الجذر: مسارٌ يحمل .. يُطبَّع ثم يُقيَّد.
  const requested = normalize(decodeURIComponent(url.pathname)).replace(/^(\.\.[/\\])+/, "");
  const candidate = join(root, requested);

  try {
    const found = await stat(candidate);
    if (found.isFile()) {
      await send(response, candidate);
      return;
    }
  } catch {
    /* لا ملف — الارتداد أدناه */
  }

  try {
    await send(response, join(root, "index.html"));
  } catch {
    response.writeHead(404, { "content-type": "text/plain; charset=utf-8" });
    response.end("لم تُبنَ الواجهة بعد: شغّل npm run build داخل web/.");
  }
}).listen(port, "127.0.0.1", () => {
  console.log(`الواجهة على http://127.0.0.1:${port}/  ·  الخلفية خلف /api و/health ← ${api}`);
});
