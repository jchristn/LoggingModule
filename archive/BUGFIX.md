# Bug fix plan: let callers turn on console logging without the terminal probe

Target release: **2.4.1** (patch). This file is a work order for the agent doing the change. Delete it when the release ships.

## The problem

`LoggingSettings.EnableConsole` has a setter that probes for a terminal before it accepts `true`:

```csharp
set
{
    if (value) _EnableConsole = ConsoleExists();   // Environment.UserInteractive && Console.WindowHeight > 0
    else _EnableConsole = false;
}
```

In a container, under Kubernetes, under systemd, or with stdout piped to a file, there is no terminal. `Environment.UserInteractive` is false and `Console.WindowHeight` throws, so the setter silently stores `false`. Console output is lost, even though stdout is exactly where those environments collect logs (`kubectl logs`, `docker logs`, journald).

Every path that passes `enableConsole: true` runs this probe:

- the setter itself (`log.Settings.EnableConsole = true`);
- the `LoggingModule(string, int, bool)`, `LoggingModule(List<SyslogServer>, bool)` and `LoggingModule(string, FileLoggingMode, bool)` constructors;
- the `AddSyslog(...)` and `AddFileLogging(...)` extension methods, which call those constructors.

Only the parameterless `LoggingModule()` constructor skips it, because the field already defaults to `true`. Downstream projects work around this today by never assigning `true` (Garrison does this in both of its `Bootstrapper.cs` files).

## The fix: an opt-in that skips the probe

Add one property to `LoggingSettings`. Its default keeps today's behavior exactly.

```csharp
/// <summary>
/// Whether setting <see cref="EnableConsole"/> to true first checks that an interactive console exists.
/// Default: true, which keeps the behavior of earlier versions: if no console is detected,
/// <see cref="EnableConsole"/> stays false.
/// Set this to false before setting <see cref="EnableConsole"/> to true when output goes to a
/// non-interactive stdout that is still collected, for example in containers, Kubernetes pods,
/// systemd services, or when stdout is redirected to a file or pipe.
/// The setting only affects later assignments to <see cref="EnableConsole"/>; it does not change the current value.
/// </summary>
public bool DetectConsole
{
    get
    {
        return _DetectConsole;
    }
    set
    {
        _DetectConsole = value;
    }
}

private bool _DetectConsole = true;
```

Change the `EnableConsole` setter to:

```csharp
set
{
    if (value) _EnableConsole = _DetectConsole ? ConsoleExists() : true;
    else _EnableConsole = false;
}
```

Update the `EnableConsole` XML doc to say the probe runs only when `DetectConsole` is true.

Use the name `DetectConsole`, or another name of your choice if it reads better in the existing API, but keep these rules: the property is positive (true means "probe"), its default is `true`, and its default matches today's behavior.

### How a caller uses it

```csharp
LoggingModule log = new LoggingModule("127.0.0.1", 514, enableConsole: false);
log.Settings.DetectConsole = false;
log.Settings.EnableConsole = true;
```

With `Microsoft.Extensions.Logging`, use the existing `configure` callback:

```csharp
builder.AddSyslog("syslogserver", 514, enableConsole: false, configure: m =>
{
    m.Settings.DetectConsole = false;
    m.Settings.EnableConsole = true;
});
```

## Why this is safe for every existing user

Each item below is a requirement. If the change can't meet one, stop and report back instead of shipping.

1. **No behavior change by default.** `DetectConsole` defaults to `true`, so every existing assignment, constructor and extension method runs the same probe as 2.4.0 and stores the same result.
2. **Additive only.** Nothing is removed, renamed or reordered, and no signature changes. No constructor or extension-method overloads are added, so overload resolution in callers is untouched. Code built against 2.4.0 keeps compiling and binding against 2.4.1.
3. **Leave `ConsoleExists()` as it is.** Don't make the probe smarter (for example by checking `Console.IsOutputRedirected`). That would change results for existing users, which is exactly what this release must not do.
4. **No new dependencies, and no target framework changes.** Keep all six targets: netstandard2.0, netstandard2.1, net462, net48, net8.0, net10.0. The property must compile on every target, so use plain property syntax.
5. **AOT and trimming stay clean.** The trim, AOT and single-file analyzers must still report zero warnings (they are build errors on net8.0 and net10.0).
6. **Console write failures are already handled.** If an opted-in caller has no usable stdout, `WriteToConsole` already catches the exception, records the destination failure and raises `OnLoggingError`. It does not throw into the caller. Do not change that code.
7. **Thread safety matches `EnableConsole`.** It is a plain field read in the setter, with no new locking.
8. **Persisted settings aren't affected.** `LoggingSettings` isn't serialized by the library. `SyslogServer`'s `syslog.json` has its own settings type, so don't change `SyslogServer` at all, and don't bump its version.

## Tests (Touchstone, in `src/Test.Shared/LoggingModuleSuites.cs`)

Add cases that run under all three runners (CLI, xUnit, NUnit) on net8.0 and net10.0:

- `DetectConsole` defaults to `true` on a new `LoggingSettings` and on `new LoggingModule().Settings`.
- With `DetectConsole = false`, `EnableConsole = true` reads back `true`, even when the test host has no console. That is the normal case for `dotnet test` and CI, so this case proves the fix.
- With `DetectConsole = true` (the default), `EnableConsole = true` stores the same value as before. Compare it to the result of the old probe expression, `Environment.UserInteractive && Console.WindowHeight > 0` (false if it throws), computed inside the test. This guards the no-behavior-change rule.
- `EnableConsole = false` stores `false` for both values of `DetectConsole`.
- Changing `DetectConsole` after `EnableConsole` was set does not change `EnableConsole`.
- With `DetectConsole = false` and `EnableConsole = true`, a logged message reaches stdout. Capture it with `Console.SetOut` to a `StringWriter` and restore the original writer in `finally`. Set `EnableColors = false` in that case so color calls don't depend on the host.

Add one line to `src/Test.Aot/Program.cs` that sets `DetectConsole = false` and then `EnableConsole = true`, then logs to the console. This checks the path under Native AOT.

Every existing case must still pass unchanged. Don't edit any existing test to make it pass.

## Release steps

1. Bump `<Version>` in `src/LoggingModule/LoggingModule.csproj` from 2.4.0 to 2.4.1. The repository owner has approved this version change for this release. Leave `SyslogServer` at 2.4.0.
2. Add `## v2.4.1` at the top of `CHANGELOG.md`, in the style of earlier entries. Say that the release adds `LoggingSettings.DetectConsole` (default `true`, no behavior change) so containers and services can turn on console output without the terminal probe.
3. README: next to the console logging docs, add a short "Console output in containers and services" note with the usage example above.
4. Update `PackageReleaseNotes` in the csproj if it lists release notes.
5. Build in Release with zero warnings and zero errors. Run `Test.Automated`, `Test.Xunit` and `Test.Nunit` on net8.0 and net10.0, plus the `Test.Aot` native publish and run.
6. Follow the repository `CLAUDE.md` style rules (explicit types, `_PascalCase` fields, XML docs on public members). Don't use em-dashes anywhere.
7. Commit and publish the package only when the repository owner asks for it.

On semantic versioning: adding a public property is strictly a minor-version change. It ships here as a patch because it is an opt-in fix for a defect, with no change for anyone who doesn't set it. If the owner prefers strict SemVer, the same change ships as 2.5.0 with nothing else different.

## Garrison follow-up (not part of this repository)

After 2.4.1 is published, Garrison (`~/Code/Garrison`, task G-8, decision D-026) bumps the `SyslogLogging` package reference in `Garrison.Server` and `Garrison.Worker` to 2.4.1. Each `Bootstrapper.BuildLogging` then sets `DetectConsole = false` and assigns `EnableConsole = settings.ConsoleLogging` directly, and the workaround comment is removed.
