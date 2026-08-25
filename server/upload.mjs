// Ratna Bay — playtest recording sink, Node edition.
//
// The same endpoint as upload.php, for a box where PHP is not the natural thing. No
// dependencies. Run it behind nginx or Caddy for TLS, with access logging off for the path.
//
// Write-only: it accepts a recording, writes it, and says nothing back. The client IP is never
// read and never written, which is the one privacy promise a self-hosted sink can make as code
// rather than as policy.

import { createServer } from 'node:http';
import { randomUUID } from 'node:crypto';
import { mkdir, rename, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

const PORT = Number(process.env.PORT ?? 8787);
const BUILD_TOKEN = process.env.RATNABAY_TOKEN ?? 'alpha-2026-08';
const STORE = process.env.RATNABAY_STORE ?? './recordings';
const MAX_BYTES = 4 * 1024 * 1024;
const RATE_PER_HOUR = 60;

const seen = new Map();

/** Per install, per hour. Crude, and exactly as clever as a thirty-tester alpha needs. */
function withinRate(install) {
  const hour = new Date().toISOString().slice(0, 13);
  const key = `${install}:${hour}`;
  const count = (seen.get(key) ?? 0) + 1;

  seen.set(key, count);
  if (seen.size > 4096) seen.clear();

  return count <= RATE_PER_HOUR;
}

/** Constrained rather than trusted: these end up in a filename. */
const hex32 = (value) =>
  typeof value === 'string' && /^[a-f0-9]{32}$/.test(value.toLowerCase())
    ? value.toLowerCase()
    : null;

/** Reads at most MAX_BYTES, then gives up. A sink should not be a memory hole. */
function readBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let size = 0;

    request.on('data', (chunk) => {
      size += chunk.length;
      if (size > MAX_BYTES) {
        reject(new Error('too large'));
        request.destroy();
        return;
      }
      chunks.push(chunk);
    });

    request.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    request.on('error', reject);
  });
}

createServer(async (request, response) => {
  // Deliberately terse. A sink that explains itself is a sink that helps somebody probe it.
  const refuse = (code) => { response.writeHead(code).end(); };

  if (request.method !== 'POST') return refuse(405);
  if (request.headers['x-ratnabay-build'] !== BUILD_TOKEN) return refuse(403);

  const install = hex32(request.headers['x-ratnabay-install']);
  const upload = hex32(request.headers['x-ratnabay-upload']);
  if (!install || !upload) return refuse(400);
  if (!withinRate(install)) return refuse(429);

  let body;
  try {
    body = await readBody(request);
  } catch {
    return refuse(413);
  }

  // Must be a recording, not merely valid JSON.
  try {
    const parsed = JSON.parse(body);
    if (!parsed || !Array.isArray(parsed.Events)) return refuse(422);
  } catch {
    return refuse(422);
  }

  // Named from the server clock, never from the recording's own filename — that is stamped in
  // the tester's local time and would leak roughly where in the world they are.
  const stamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
  const target = join(STORE, `${stamp}-${install}-${upload}.json`);
  const partial = `${target}.${randomUUID()}.part`;

  try {
    await mkdir(STORE, { recursive: true, mode: 0o700 });

    // Written whole, then moved into place, so a dropped connection never leaves half a
    // recording for the review tool to choke on.
    await writeFile(partial, body, { mode: 0o600 });
    await rename(partial, target);
  } catch {
    return refuse(500);
  }

  refuse(204);
}).listen(PORT, () => console.log(`recording sink on :${PORT} -> ${STORE}`));
