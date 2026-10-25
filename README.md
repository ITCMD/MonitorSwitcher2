# MonitorSwitcher2
a fork of monitorswitcher that doesnt forget monitor layouts

Based on [Monitor Profile Switcher](https://sourceforge.net/projects/monitorswitcher/) by Martin Krämer
(upstream git: `https://git.code.sf.net/p/monitorswitcher/code-git`, commit `cfa129a`).
Save multi-monitor layouts and switch between them from a tray menu.

## The problem this fixes

Windows identifies a graphics card by an adapter LUID, which is reassigned every time the graphics
driver reloads: a restart, a driver update, or a driver reset. Profiles store the LUID from when they
were saved, so upstream tries to swap in the current one before applying a profile. It only copies
the lower 32 bits of the 64-bit LUID. Once the upper half changes, every profile fails with
error 87 and nothing happens when you click it. The only notice is a tray balloon, which Windows
often hides. The usual workaround is to set the layout up by hand and save the profile again.

Upstream's fallback finds monitors by name, but only among displays that are currently on.
So a profile that turns on a disabled display (like a TV) can't be found that way either.

## What changed

- New matcher (`MatchToCurrentDisplays` in `MonitorSwitcher/MonitorSwitcher/Program.cs`):
  - lists every connected display, including disabled ones
  - matches each saved monitor by its device path, or by its EDID manufacturer/product ID
    when exactly one connected monitor has that ID
  - rewrites the full 64-bit adapter LUID and the target ID
  - profiles without monitor info fall back to the old source/target ID match, using the full LUID
- The last-resort upstream fallback also compares and replaces the full LUID now.
- New `-validate:{xmlfile}` command-line option. It runs the matching and asks Windows whether
  the profile could be applied (`SDC_VALIDATE`), without changing any displays. Add `-debug` first
  to see how each monitor was matched.
- Targets .NET Framework 4.8. ClickOnce manifest generation is turned off.

Profiles are still read from and saved to `%APPDATA%\MonitorSwitcher\Profiles`, in the same format,
so existing profiles keep working.

## Building

With the .NET SDK installed (it's only used to run MSBuild; the result is a .NET Framework 4.8 app):

```
dotnet msbuild MonitorSwitcherGUI\MonitorSwitcherGUI\MonitorSwitcherGUI.csproj /p:Configuration=Release
```

Output: `MonitorSwitcherGUI\MonitorSwitcherGUI\bin\Release\` (`MonitorSwitcherGUI.exe` + `MonitorSwitcher.exe`).
`MonitorSwitcherGUIConfig` is an unfinished upstream project and doesn't build.

## Command line

```
MonitorSwitcher.exe -save:"C:\path\Profile.xml"
MonitorSwitcher.exe -load:"C:\path\Profile.xml"
MonitorSwitcher.exe -debug -validate:"C:\path\Profile.xml"
```

## License

Mozilla Public License 2.0, same as upstream (see `MonitorSwitcherGUI/MonitorSwitcherGUI/Licence.txt`).
Icons are licensed separately, see `LicenceIcons.txt`.
