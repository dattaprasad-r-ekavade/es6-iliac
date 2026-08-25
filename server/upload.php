<?php
// Ratna Bay — playtest recording sink.
//
// Drop this at https://datathecodie.com/ratnabay/upload.php and create a sibling directory
// called "recordings" that PHP can write to but the web server will not serve:
//
//   mkdir -p /path/to/ratnabay/recordings
//   chmod 700 /path/to/ratnabay/recordings
//   printf 'Require all denied\n' > /path/to/ratnabay/recordings/.htaccess
//
// Write-only by design. It accepts a recording, writes it, and says nothing back. There is no
// listing, no reading, and no way to ask it what it has — so the worst a leaked build token
// buys somebody is the ability to send junk, which the rate limit and the size cap bound.
//
// The client IP is never written down. That is the one privacy promise a self-hosted endpoint
// can make as code rather than as policy, and it is most of the reason to self-host at all.
// Note that the web server's own access log is a separate matter: turn it off for this path,
// or accept it and say so.

declare(strict_types=1);

const BUILD_TOKEN   = 'alpha-2026-08';   // must match Telemetry.BuildToken in the game
const MAX_BYTES     = 4 * 1024 * 1024;
const STORE         = __DIR__ . '/recordings';
const RATE_PER_HOUR = 60;                // per install, generous for a human, useless for a flood

function refuse(int $code): never
{
    // Deliberately terse. A sink that explains itself is a sink that helps somebody probe it.
    http_response_code($code);
    exit;
}

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
    refuse(405);
}

if (!hash_equals(BUILD_TOKEN, $_SERVER['HTTP_X_RATNABAY_BUILD'] ?? '')) {
    refuse(403);
}

$body = file_get_contents('php://input');
if ($body === false || $body === '' || strlen($body) > MAX_BYTES) {
    refuse(413);
}

// Must be a recording, not merely valid JSON. Cheapest possible sanity check against a sink
// that quietly fills with something else entirely.
$parsed = json_decode($body, true);
if (!is_array($parsed) || !isset($parsed['Events']) || !is_array($parsed['Events'])) {
    refuse(422);
}

// Identifiers come from the client and are constrained here rather than trusted: they end up
// in a filename, so anything that is not plain hex is not going anywhere near the disk.
$install = preg_replace('/[^a-f0-9]/', '', strtolower($_SERVER['HTTP_X_RATNABAY_INSTALL'] ?? ''));
$upload  = preg_replace('/[^a-f0-9]/', '', strtolower($_SERVER['HTTP_X_RATNABAY_UPLOAD'] ?? ''));

if (strlen($install) !== 32 || strlen($upload) !== 32) {
    refuse(400);
}

if (!is_dir(STORE) && !mkdir(STORE, 0700, true) && !is_dir(STORE)) {
    refuse(500);
}

// Rate limit per install, by counting what that install has already sent this hour. Crude, and
// exactly as clever as a thirty-tester alpha needs.
$hour  = gmdate('YmdH');
$stamp = STORE . "/.rate-$install-$hour";
$count = is_file($stamp) ? (int)file_get_contents($stamp) : 0;

if ($count >= RATE_PER_HOUR) {
    refuse(429);
}

file_put_contents($stamp, (string)($count + 1), LOCK_EX);

// Named from the server clock and the client's upload id. Never from the recording's own
// filename, which is stamped in the tester's local time and would leak roughly where they are.
$name = sprintf('%s/%s-%s-%s.json', STORE, gmdate('Ymd-His'), $install, $upload);

// Written whole to a temporary file, then moved into place, so a dropped connection leaves no
// half a recording for the review tool to choke on.
$temporary = $name . '.part';
if (file_put_contents($temporary, $body, LOCK_EX) === false || !rename($temporary, $name)) {
    @unlink($temporary);
    refuse(500);
}

http_response_code(204);
