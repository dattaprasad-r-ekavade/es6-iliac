# The recording sink

One endpoint on a server we run, which accepts a finished playtest recording and writes it to
disk. Nothing else. There is no listing, no reading it back over HTTP, and no way to ask it what
it holds — so the worst a leaked build token buys anybody is the ability to send junk, which the
rate limit and the size cap bound.

**Why self-hosted rather than a vendor.** Every HTTP endpoint sees the tester's IP; the only
question is whether it is written down. With somebody else's service that is a promise in their
policy. Here it is a line of our own code, or rather the absence of one.
[`Docs/TELEMETRY_RETURN_RESEARCH.md`](../Docs/TELEMETRY_RETURN_RESEARCH.md) works through the
alternatives and lands on Cloudflare only because it assumed no server was available.

---

## Install — PHP (shared hosting, cPanel, most managed boxes)

```sh
mkdir -p /var/www/datathecodie.com/ratnabay/recordings
cp upload.php /var/www/datathecodie.com/ratnabay/
chmod 700 /var/www/datathecodie.com/ratnabay/recordings
printf 'Require all denied\n' > /var/www/datathecodie.com/ratnabay/recordings/.htaccess
```

That last line matters. Without it the recordings sit under the web root and anybody who guesses
a filename can read one.

**Turn the access log off for this path**, or the IP the endpoint carefully does not write ends
up in the log anyway:

```apache
<Location /ratnabay/upload.php>
    SetEnv dontlog
</Location>
CustomLog /var/log/apache2/access.log combined env=!dontlog
```

Check it works — the first should give `403`, the second `204`:

```sh
curl -s -o /dev/null -w '%{http_code}\n' -X POST https://datathecodie.com/ratnabay/upload.php

curl -s -o /dev/null -w '%{http_code}\n' -X POST \
  -H 'X-Ratnabay-Build: alpha-2026-08' \
  -H "X-Ratnabay-Install: $(openssl rand -hex 16)" \
  -H "X-Ratnabay-Upload: $(openssl rand -hex 16)" \
  -H 'Content-Type: application/json' \
  --data '{"Version":1,"StartedUtc":"2026-08-25T00:00:00Z","Build":"test","Events":[]}' \
  https://datathecodie.com/ratnabay/upload.php
```

## Install — Node (a VPS you control)

`upload.mjs` is the same endpoint with no dependencies. Run it behind nginx or Caddy with TLS:

```sh
node server/upload.mjs                       # listens on 8787
```

```nginx
location /ratnabay/upload {
    proxy_pass http://127.0.0.1:8787;
    access_log off;                          # the point of self-hosting
    client_max_body_size 4m;
}
```

Then set `Telemetry.Endpoint` to `https://datathecodie.com/ratnabay/upload`.

---

## Reading what comes back

The file that lands is byte-for-byte the JSON the game wrote, which is the JSON
`PlayRecording.TryLoad` already reads. So there is no schema to map and no dashboard to learn:

```sh
scp -r datathecodie.com:/var/www/datathecodie.com/ratnabay/recordings ./inbox
dotnet run --project tools/RatnaBay.Tools -- review ./inbox/20260825-193102-<install>-<upload>.json
```

Everything `PlayReview` has learned the hard way — that a forced camp is not a decision, that a
re-advertised door is not a second one, that menu time is not deliberation — applies to a
tester's recording exactly as it does to ours.

## Keeping it honest

- **Change `BUILD_TOKEN` every build**, in both `upload.php` and `Telemetry.BuildToken`. An old
  build then stops being able to post, which is how a finished round is closed.
- **Delete the raw recordings once the question is answered.** What is worth keeping is the
  `PlayReview` output, not thirty testers' sessions. Retention you never decided is retention
  that lasts forever.
- **Nothing in a payload identifies a person** — no name, no path, no hardware id, no free text.
  The install id is a random GUID made on first launch and deleting the file makes a new one.
  `Docs/TELEMETRY_RETURN_RESEARCH.md` §4 lists what must never be added, and it is worth
  re-reading before adding a field.
