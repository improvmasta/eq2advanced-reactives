# Reactives for EQ2Advanced

A beta EQ2Advanced desktop plugin that shows a healer's remaining reactive heal triggers and time for each group member. It currently recognizes Inquisitor and Templar single and group reactives. The tab shows settings and group rows; the Dash widget can also open as an overlay.

## Credit

The concept came from **Avacii's [ReactiveTracker ACT plugin](https://github.com/Avacee/ACT/tree/master/ReactiveTracker)** (GitHub: **Avacee**). Avacii described the aim as tracking reactive charges per group member and called out group changes, overwrites and death as cases to improve. This is an independent implementation for EQ2Advanced's plugin API; it does not include source from the GPL-3.0 ACT project. The credit is also visible in the tab, overlay and plugin listing.

## How it works

- Watches only new EQ2 log lines while the desktop client is running. It does not read history or send data over the network.
- Finds your Inquisitor or Templar casts and the named group protection lines the game writes after a group cast. It shows a count only when the cast or protection was seen; a heal proc without its cast reports an unknown count.
- Counts down from the cast time and subtracts one trigger when the corresponding `YOUR ... heals ...` line appears. New casts replace the old count for that recipient.
- Clears a group effect when the log says its protection ended. A subsequent protection line from your new cast starts a fresh count. It also clears known recipients on death or group departure and adds people on group join.
- Keeps boxed healers separate by log character. The healer selector appears if more than one is seen.
- Coercive Healing, the Templar EoF two-piece bonus and warning thresholds are per healer and saved locally. Bonuses affect the next cast, not an active count.

The spell duration defaults to 30 seconds, matching the original tracker. EQ2 text and ability behavior can vary by server or era; if a log message differs, the plugin leaves the count unknown instead of inventing one. The first live cast is needed after the plugin starts. Group protection lines name recipients but do not name the caster; the plugin associates them with this character's recent group cast. Two healers casting the same group reactive at nearly the same time can make that inference ambiguous.

## Install and build

Install a ZIP through **Settings → Plugins → Install from ZIP**. The manifest requests only `live-log` and declares no network access. In the EQ2Advanced desktop source checkout, package it with:

```bash
bash scripts/package-plugin.sh plugins/reactive-tracker
```

This repository is also buildable on its own with .NET 8, Node.js, Python 3 and Bash:

```bash
bash package.sh
```

The installable ZIP is written to `dist/`. GitHub Actions runs the same checks and
publishes the ZIP as a workflow artifact. A local build also works with
`dotnet build src/EQ2Advanced.ReactiveTracker/EQ2Advanced.ReactiveTracker.csproj`.

`src/EQ2Advanced.Plugins` contains the minimal compile-time API 2 declarations. The desktop client supplies the actual `EQ2Advanced.Plugins.dll` at run time; packaging must omit the compile-time DLL. This keeps the plugin's public source buildable without the desktop source tree.

## License

The new implementation in this repository is MIT licensed; see [LICENSE](LICENSE). Avacii's original ACT project is GPL-3.0 and remains separately credited above.
