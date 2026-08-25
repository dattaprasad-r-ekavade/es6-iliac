# Android Feasibility

**Written:** 2026-08-25
**Question:** does the framework support Android builds, and what would it cost this game?
**Status:** research and a build spike. Nothing in the repository was changed to produce it.

---

## 1. The short answer

**Yes, twice over.**

MonoGame ships an Android runtime, `MonoGame.Framework.Android`, at version **3.8.5.1** — the exact
version this project already pins for `MonoGame.Framework.WindowsDX`. So the framework question is
settled without a caveat.

The more useful answer is about *this* codebase, and it is better than expected: **every line of
game code already compiles for Android, and packages into a signed APK, with no source change at
all.** The only new file needed is an Android entry point.

What that does *not* mean is that the game is playable on a phone. It compiles, links, AOT-compiles
and packages. It has never been run on a device, it has no way to load its own content on Android,
and it has no touch controls for a game built entirely around mouse-look. Those are three separate
pieces of real work, and the third one is a design problem rather than a porting problem.

---

## 2. What was actually proven

A throwaway spike, outside the repository, in `/tmp`. Three builds, in increasing ambition.

### 2.1 The domain compiles for Android

`RatnaBay.Domain` — every game rule, the save contracts, the mine generator, the run ledger, the
telemetry recorder — compiled against `net9.0-android` with **zero errors**. It picked up only the
one XML-comment warning it already has on Windows.

This was the expected result and it is worth stating anyway: the closed decision that game rules
stay engine-free, in a plain library with no MonoGame reference, is what makes this a non-event. The
rules are portable because they were never allowed to depend on anything.

### 2.2 The whole client compiles against the Android runtime

Then the interesting one. All of `RatnaBay.Game` — 7,800 lines including the 4,100-line `Game1.cs` —
compiled against `MonoGame.Framework.Android` 3.8.5.1 and `FontStashSharp.MonoGame` 1.5.6, targeting
`net9.0-android`. **Zero errors.** Only `Program.cs` was excluded, because a desktop `Main` is not
how an Android app starts.

Every XNA type the game touches exists on Android with the same signature: `SpriteBatch`,
`BasicEffect`, `Texture2D`, `VertexPositionNormalTexture`, `SoundEffect`, `DepthStencilState`,
`RasterizerState`, `Viewport`, even `Window.IsBorderless` and `GraphicsDeviceManager.IsFullScreen`.

### 2.3 It packages into a real APK

A minimal Android head — one `MainActivity` deriving from `AndroidGameActivity`, an
`AndroidManifest.xml`, and a csproj — produced:

| Artifact | Size |
|---|---:|
| `com.ratnabay.spike-Signed.apk` | 6.5 MB |
| `com.ratnabay.spike-Signed.aab` | 6.5 MB |

64 entries, `arm64-v8a` only, everything AOT-compiled. `libaot-MonoGame.Framework.dll.so` and
`libaot-RatnaBay.Android.dll.so` are both in there, and so is **`libopenal.so`** — meaning the audio
backend the game's `AmbientAudio` needs is bundled and wired up by the Android runtime, rather than
being the XAudio2/SharpDX path that only exists on Windows.

For scale: the Windows self-contained publish is **131 MB**. The Android package is 6.5 MB before
content, because Android AOT-compiles and trims rather than shipping a whole runtime folder.

### 2.4 Why the codebase made this so cheap

The reason there was nothing to fix is worth recording, because it is a property worth keeping.
A search of the entire game project for Windows-specific API surface returns **exactly one match**,
and it is the package reference itself:

```
src/RatnaBay.Game/RatnaBay.Game.csproj:22:  <PackageReference Include="MonoGame.Framework.WindowsDX" ... />
```

No `System.Windows.Forms`. No `SharpDX`. No `System.Drawing`. No `Microsoft.Win32`. No `DllImport`,
no `user32`, no `kernel32`, no registry access. The complete set of namespaces the client imports is:

```
FontStashSharp
Microsoft.Xna.Framework{,.Audio,.Graphics,.Input}
RatnaBay.Domain
System{,.Collections.Generic,.IO,.Linq}
```

That is the whole dependency surface. The `UseWindowsForms`, `net9.0-windows` and `win-x64` settings
in the csproj are inherited from the MonoGame WindowsDX template, not because any code needs them.

There is a related finding that matters for a port. Of the community packages the project pins and
`doctor` checks, **only FontStashSharp is used by any code at all**:

| Package | Used in game code |
|---|---|
| FontStashSharp.MonoGame | Yes — the two font systems and the Stambha carving |
| MonoGame.Extended | No — content-pipeline reference only |
| Gum.MonoGame | No — closed decision: UI is immediate-mode on SpriteBatch |
| ImGui.NET | No |
| BepuPhysics | No — closed decision: physics is hand-rolled |
| DotRecast.Recast / .Detour | No — closed decision: navigation is direct pursuit |
| Ink | No |

Six of the seven are dead references. On Windows they cost only restore time. On Android they would
be the most likely source of a native-library problem, and they can simply be dropped. The spike
referenced only MonoGame and FontStashSharp, and needed nothing else.

### 2.5 Reproducing it

Versions, so this can be re-run or disbelieved:

| Component | Version |
|---|---|
| .NET SDK | 9.0.317 |
| `Microsoft.Android.Sdk.Linux` workload | 35.0.105 |
| `MonoGame.Framework.Android` | 3.8.5.1 |
| `FontStashSharp.MonoGame` | 1.5.6 |
| Android SDK platform / build-tools | 35 / 35.0.0 |
| JDK | 17.0.19 |

```bash
dotnet workload install android
# Android SDK: platform-tools, platforms;android-35, build-tools;35.0.0
dotnet build RatnaBay.Android.csproj -c Release \
  -p:AndroidSdkDirectory=$ANDROID_HOME -p:JavaSdkDirectory=$JAVA_HOME
```

The head is a `net9.0-android` `Exe` with `SupportedOSPlatformVersion` 21, `targetSdkVersion` 35,
`RuntimeIdentifiers` `android-arm64`, referencing the two packages above and compiling the existing
`RatnaBay.Domain` and `RatnaBay.Game` sources with `Program.cs` excluded. `MainActivity` constructs
`Game1(Array.Empty<string>())`, pulls the `View` out of `Game.Services`, sets it as the content view,
and calls `Run()`.

---

## 3. What was not proven, and is the actual work

The spike deliberately stopped at packaging. Everything below is untested and three of the four
items are certain to be needed.

### 3.1 Content loading — certain, mechanical, bounded

This is the one guaranteed breakage. The game finds all of its data relative to the executable:

```
src/RatnaBay.Game/Game1.cs:339   AppContext.BaseDirectory
src/RatnaBay.Game/Game1.cs:348   File.ReadAllBytes(.../NotoSans/NotoSans-wght.ttf)
src/RatnaBay.Game/Game1.cs:352   File.ReadAllBytes(.../Cinzel/Cinzel-wght.ttf)
src/RatnaBay.Game/Game1.cs:1759  Content/World/northwatch.json
src/RatnaBay.Game/Game1.cs:1780  Content/World/Generated
src/RatnaBay.Game/Game1.cs:1800  Content/Dialogue/northwatch.json
src/RatnaBay.Game/Game1.cs:1863  Content/Shops/northwatch.json
src/RatnaBay.Game/Game1.cs:1998  Content/Quests/northwatch.json
```

On Android there is no such directory. Assets live compressed inside the APK and are read through
`TitleContainer.OpenStream` or the Android asset manager, not `File.ReadAllBytes` on a path.

Two ways out. Either introduce a small content-reading seam — one method that returns a `Stream` for
a relative content path, with a desktop implementation and an Android implementation — or, on first
launch, copy the asset tree out of the APK into app-private storage and leave every existing path
untouched. The seam is cleaner and would also make the existing hot-reload story explicit about
which platform supports it. It is perhaps a day of unglamorous work either way, and it touches
around eight call sites.

Note that the *save* path already works. `GameSession.SaveDirectory` uses
`Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)`, which on Android resolves to
the app's own private directory. Saves and telemetry recordings would land somewhere sensible with no
change at all.

### 3.2 Input — the real problem, and it is a design problem

The game binds **38 distinct keys** plus mouse-look, left-click attack, right-click guard, and
click-to-select in every panel. There is not one line of `TouchPanel` code in the project.

This is not a porting task that can be estimated in files changed. A first-person game with mouse
look, a melee swing, a guard, a jump, a crouch, five spells on the number row, and six panels bound
to letter keys does not have a touch layout waiting to be discovered. Twin sticks plus a small set of
context buttons is the obvious starting point, and it would change how the game plays — the archer
added in the last commit exists specifically to punish standing still, and a virtual stick is worse
at not standing still than a mouse is.

The 1280×720 logical canvas that already scales uniformly into any display is a genuine gift here:
the UI would fit a phone without being redesigned, which is more than most ports get. But readability
at phone size for a HUD authored for a monitor is its own question.

### 3.3 The content pipeline needs one switch

`src/RatnaBay.Game/Content/Content.mgcb` is set to `/platform:Windows` and `/profile:Reach`. Android
needs `/platform:Android`. The `Reach` profile is already the mobile-friendly choice, so that half is
right by accident.

The pipeline's only inputs are eight `.fbx` prop models under `Content/Feasibility`. Fonts and all
world/dialogue/quest/shop data are raw files read at runtime, not pipeline content. So the MGCB step
is small — and it is worth asking whether those eight models are still wanted at all, given the
closed decision that characters and weapons are sprites drawn in code, and the playtest finding that
the imported prop meshes were the thing occluding the Northwatch yard.

### 3.4 Nothing here says it renders or performs

The APK was never installed. The game draws its world as a large number of individually-issued cubes
through `BasicEffect`; that is fine on a desktop GPU and is exactly the shape of workload that falls
over on a mid-range phone. Draw-call batching may well be the first thing a device run demands. This
is unknown, not merely unmeasured.

---

## 4. Store and policy constraints, if it ever went further

These come from MonoGame's own upgrade guidance and are not optional:

- **.NET 9 is mandatory** for Android and iOS, not a recommendation. Desktop can stay on .NET 8;
  Android cannot. ([migration guide](https://docs.monogame.net/articles/migration/migrate_38.html))
- **MonoGame 3.8.4.1 or newer is required** to comply with Google's 16 KB memory-page policy. The
  project is already on 3.8.5.1.
- **`targetSdkVersion` must be at least 35.** MonoGame's own templates set `minSdkVersion` 21, which
  is safe; it is the target that Google polices.
- Google Play wants an **AAB**, not an APK. The spike produced both.
- The 16 KB page-size transition has caused real trouble for MonoGame Android developers around
  which architectures end up in a bundle
  ([discussion #8987](https://github.com/MonoGame/MonoGame/discussions/8987)). Worth reading before
  committing to a Play release rather than sideloaded test builds.

For *playtesting*, none of the Play Store constraints apply. itch.io tags any channel whose name
contains `android` as an Android application
([butler manual](https://itch.io/docs/butler/pushing.html)), so `butler push ... :android` publishes
an APK testers sideload directly. That skips store review entirely, which is the right way to test
and the wrong way to ship.

---

## 5. Recommendation

**Do not port now. Record that the door is open, and close the question.**

The reasoning is the production plan's own, not a general preference. Iteration 14 is built but not
judged: the loop's central question — whether anybody hesitates at the door — has one recorded
session against it, rated 6/10, with a median hesitation of one second. The board's WIP limit is one
and its Next item is "play it, then hand it to somebody". An Android port is a second platform for a
loop that has not yet been shown to work on the first one, and the standing risk list already names
"another engine or genre pivot" and "polish before playability" as the two most likely ways this
project ends.

There is also a specific reason Android is not a cheap way to get more playtesters, which is the
thing that would otherwise justify it: touch controls would change what is being tested. A tester on
a phone with a virtual stick is not answering the same question as a tester with a mouse. The one
open question does not get answered faster by adding a control scheme to it.

What is worth doing now, and costs nothing:

1. **Keep the property that made the spike free.** No `System.Windows.Forms`, no P/Invoke, no
   `SharpDX`, nothing but XNA and the BCL in the client. It held by accident so far; it is cheap to
   hold on purpose.
2. **Drop the six dead package references** when something else is being changed in that csproj.
   They are unused on Windows and they are the likeliest native-library trouble on any other
   platform. This also shortens `doctor`.
3. **Introduce the content-reading seam when content loading is next touched anyway.** One method
   returning a `Stream` for a relative path. It is the only mechanical blocker, and doing it
   opportunistically means the port never has a big-bang day.

Then revisit after the slice lock in iteration 21, when there is a game worth putting on a second
platform and a control scheme worth designing for it.

---

## 6. Summary table

| Question | Answer | Evidence |
|---|---|---|
| Does MonoGame support Android? | Yes | `MonoGame.Framework.Android` 3.8.5.1, same version as the pinned WindowsDX |
| Does the domain compile for Android? | Yes, 0 errors | `net9.0-android` build of all domain sources |
| Does the client compile for Android? | Yes, 0 errors | 7,800 lines incl. `Game1.cs` against `MonoGame.Framework.Android` |
| Does it package? | Yes | 6.5 MB signed APK + AAB, arm64-v8a, AOT, `libopenal.so` bundled |
| Source changes required? | None | Only a new Android head: `MainActivity` + manifest + csproj |
| Does it run on a device? | **Unknown — never installed** | Not tested |
| Can it load its content? | **No** | 8 call sites use `AppContext.BaseDirectory` + `File.ReadAllBytes` |
| Can it be played? | **No** | 38 key bindings + mouse-look, zero touch code |
| Blocking work | Content seam (small), touch controls (a design problem) | §3.1, §3.2 |
| Recommendation | Not now; keep the door open at zero cost | §5 |
