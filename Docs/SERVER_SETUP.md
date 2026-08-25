# Ratna Bay — telemetry server setup

**For:** whoever administers `datathecodie.com`.
**What it is:** one PHP file that accepts a playtest recording from the game and writes it to
disk. Nothing else runs, nothing is served back, nothing talks to a database.
**Time to set up:** about ten minutes.

This document is self-contained. The full source is below — you do not need the game's
repository to deploy it.

---

## 1. What this does, in one paragraph

Ratna Bay is an alpha. While somebody plays, the game writes a JSON file describing what
happened — rooms cleared, what killed what, how long the player hesitated at a decision. On the
next launch, **if the player has agreed**, the game POSTs those files to this endpoint. The
endpoint checks a token, checks the size, checks it looks like a recording, and writes it to a
folder. That is the whole system.

**It is write-only on purpose.** There is no listing, no read-back, no way to ask it what it
holds. The build token shipped inside the game binary can be extracted by anybody who cares to,
so it is treated as a doorbell rather than a lock: the worst it buys is the ability to post
junk, which the rate limit and the size cap bound. Nothing in a payload is worth stealing.

---

## 2. What you need

| | |
|---|---|
| PHP | 8.0 or newer (uses `never` return type) |
| Web server | Apache or nginx with TLS on `datathecodie.com` |
| Disk | Trivial. A recording is roughly 50–150 KB; thirty testers will not reach 100 MB |
| Outbound | None. The endpoint never calls anything |

---

## 3. Install

### 3.1 Put the file in place

```sh
mkdir -p /var/www/datathecodie.com/ratnabay/recordings
# copy the source from section 7 into:
#   /var/www/datathecodie.com/ratnabay/upload.php
```

It must end up reachable at exactly:

```
https://datathecodie.com/ratnabay/upload.php
```

If it needs to live somewhere else, tell the developer — the URL is compiled into the game and
has to match.

### 3.2 Lock the recordings folder

**This is the step that matters.** Without it the recordings sit under the web root and anybody
who guesses a filename can download somebody's session.

```sh
chmod 700 /var/www/datathecodie.com/ratnabay/recordings
chown www-data:www-data /var/www/datathecodie.com/ratnabay/recordings   # or whatever PHP runs as
```

On **Apache**, also drop a `.htaccess` inside it:

```sh
printf 'Require all denied\n' > /var/www/datathecodie.com/ratnabay/recordings/.htaccess
```

On **nginx**, `.htaccess` does nothing — block it in the site config instead:

```nginx
location ^~ /ratnabay/recordings {
    deny all;
    return 404;
}
```

### 3.3 Turn the access log off for this path

The endpoint deliberately never writes the visitor's IP. If the access log records it anyway,
that care was wasted.

**Apache:**

```apache
<Location /ratnabay/upload.php>
    SetEnv dontlog
</Location>
CustomLog ${APACHE_LOG_DIR}/access.log combined env=!dontlog
```

**nginx:**

```nginx
location = /ratnabay/upload.php {
    access_log off;
    client_max_body_size 4m;
    include fastcgi_params;                       # plus your usual PHP-FPM block
    fastcgi_pass unix:/run/php/php8.2-fpm.sock;
}
```

If you would rather keep the log, that is a legitimate choice — say so, because the game tells
players their location is not sent and the developer needs to keep that true.

### 3.4 Allow a 4 MB request body

The endpoint caps uploads at 4 MB itself, but PHP must let one through:

```ini
post_max_size = 8M
upload_max_filesize = 8M
```

---

## 4. Check it works

The first command should print **403** (no token). The second should print **204** (accepted).

```sh
curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  https://datathecodie.com/ratnabay/upload.php

curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  -H 'X-Ratnabay-Build: alpha-2026-08' \
  -H "X-Ratnabay-Install: $(openssl rand -hex 16)" \
  -H "X-Ratnabay-Upload: $(openssl rand -hex 16)" \
  -H 'Content-Type: application/json' \
  --data '{"Version":1,"StartedUtc":"2026-08-25T00:00:00Z","Build":"test","Events":[]}' \
  https://datathecodie.com/ratnabay/upload.php
```

Then confirm a file landed, and that the web cannot reach it:

```sh
ls -l /var/www/datathecodie.com/ratnabay/recordings

curl -s -o /dev/null -w '%{http_code}\n' \
  https://datathecodie.com/ratnabay/recordings/          # want 403 or 404
```

### What the response codes mean

| Code | Meaning |
|---|---|
| `204` | Accepted and written. The game marks it sent and stops retrying |
| `403` | Wrong or missing build token — usually an old game build after a token change |
| `400` | Install or upload header was not 32 hex characters |
| `413` | Body empty or over 4 MB |
| `422` | Not a recording — valid JSON but no `Events` array |
| `429` | That install has sent 60 in the last hour |
| `405` | Not a POST |
| `500` | Could not create or write the folder — check permissions |

Anything other than `204` makes the game keep the file and try again next launch, so a
misconfigured server loses no data as long as it is fixed before the tester uninstalls.

---

## 5. Getting the data to the developer

```sh
scp -r datathecodie.com:/var/www/datathecodie.com/ratnabay/recordings ./inbox
```

Files are named `<utc-timestamp>-<install>-<upload>.json`. The `install` part is a random number
the game made up on first launch; the same value across two files means the same installation,
which is the only grouping that exists.

---

## 6. Running it responsibly

- **Rotate the token between rounds.** Change `BUILD_TOKEN` here and tell the developer to change
  it in the game. Old builds then stop being able to post, which is how a finished playtest round
  is closed.
- **Delete the recordings once the round is analysed.** What is worth keeping is the summary, not
  thirty people's sessions. A retention period nobody decided is a retention period of forever.
- **Never add IP logging to this file**, and do not put the IP in a filename or an error message.
  The game tells players their location is not sent.
- **The `.rate-*` files** in the recordings folder are the rate limiter's counters. They are safe
  to delete at any time and safe to leave; a daily `find ... -name '.rate-*' -mtime +1 -delete`
  tidies them if it bothers you.

### What is in a payload

Room numbers, timings, damage figures, item names, and the anonymous install id. **No name, no
file path, no hardware identifier, no location, and no free text** — the game has no field a
player can type into. If a future version wants to add a field, that is a conversation, not a
patch.

---

## 7. The source

Save as `/var/www/datathecodie.com/ratnabay/upload.php`.

```php
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
```
