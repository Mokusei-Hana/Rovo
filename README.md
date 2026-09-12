# Rovo

A small Windows Robocopy frontend built with C#, .NET 10, WPF, and CommunityToolkit.Mvvm.

## Phase 1

Choose source and destination folders, select subfolder handling, set retries, and start copying. Rovo shows live output and the final exit code. Cancel stops Robocopy; files already copied remain and an interrupted file may be incomplete.

- Copies new and changed files using standard Robocopy behavior. Destination-only files are kept.
- Defaults: all subfolders, two retries, two seconds between retries.
- Junctions are excluded with `/XJ`. `/NP` suppresses per-file percentages; the UI shows indeterminate activity.
- Normal Robocopy output is decoded using the Windows OEM code page. Rovo does not force `/UNICODE`; characters outside that code page may not display faithfully even though Unicode paths are passed intact.
- Exit codes 0–1 indicate completion, 2–7 indicate differences to review, and 8 or above indicate failure. Output for the latest run is limited to 10,000 lines.
- Older output omitted by the limit is counted below the log. Scroll back or select text to inspect it; automatic scrolling resumes when you return to the bottom with no selection. Lines that age out of the limit cannot be preserved.
- Source and destination must be absolute local or UNC paths. The source must exist; Robocopy can create the destination.
- Rovo rejects equal or nested paths as an application safety restriction, not a Robocopy limitation. This check compares normalized paths; it does not resolve filesystem aliases or mapped drives.

Settings last for the current session only. Exclusions, mirror/move/purge modes, custom flags, saved jobs, schedules, queues, log export, and installers are outside Phase 1.

## Build and run

Use Windows with the .NET 10 SDK and the system Robocopy executable:

```powershell
dotnet restore Rovo.slnx
dotnet build Rovo.slnx -c Release --no-restore
dotnet test Rovo.Tests/Rovo.Tests.csproj -c Release --no-build
dotnet run --project Rovo/Rovo.csproj -c Release --no-build
```

The build is framework-dependent; running the built application requires the .NET 10 Desktop Runtime. No administrator elevation is requested. Source and destination access uses the current user's permissions.

The WPF project can be cross-built on Linux using its Windows targeting pack. UI-independent tests also run there; real Robocopy integration tests explicitly skip outside Windows. A cross-build does not verify WPF runtime behavior.

GitHub Actions runs restore, Release build, and tests on both Ubuntu and Windows. Windows runs the real Robocopy tests, including locked-file failure, cancellation, and a subsequent copy. Test reports are attached to each run. See [VERIFICATION.md](VERIFICATION.md) for the review findings and validation evidence.

## Structure

One application project contains Models, Services, ViewModels, and Views. `App` directly constructs the service, view model, and window. The view owns folder dialogs, close confirmation, and a timer that flushes buffered output. The service owns the Robocopy process and drains both output streams asynchronously.

Folder validation and service startup run off the UI thread. During an unavailable network-folder check, Cancel requests cancellation and keeps controls busy until that operating-system call returns; no copy is started afterward. Output producers share a bounded queue and each UI refresh consumes one finite snapshot.

The test project links the application's UI-independent source files so it can test the actual logic on Linux without an extra production library. It includes Windows-only tests that run real copies in isolated temporary folders.

## Windows acceptance checks

After build and automated tests pass:

1. Start Rovo; use keyboard navigation and folder dialogs. Verify readable layout at 100%, 150%, and 200% display scaling, including the minimum window size.
2. Copy a temporary directory with new/changed files, empty subfolders, paths containing spaces and non-ASCII characters, and destination-only files. Verify the destination contents and completion code.
3. Test invalid retries, equal/nested paths, missing sources, unavailable UNC paths, and denied destination access. Errors should remain visible and controls should recover.
4. Copy a large file tree. Verify the window stays responsive, output remains bounded, and scrolling back permits inspection.
5. Cancel a running copy and start another. Close during a copy, choosing both No (keep copying) and Yes (cancel and close). The application must wait for Robocopy to stop before closing.
6. Check output with the Windows system locale in use. Ordinary console output cannot represent every Unicode filename; verify actual destination names separately.

Robocopy reference: [Microsoft command documentation](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy).
