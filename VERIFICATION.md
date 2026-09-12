# Phase 1 review and verification

## Starting point

Reviewed `b1873e6` (the initial workspace snapshot). Folder selection, supported
Robocopy options, process ownership, cancellation, bounded output, exit-code
classification, unit tests, and three Windows integration tests were already
implemented. They were retained. `TASK.md` contained placeholder sections; these
now document the scope already present in README and the implementation.

## Changes

- Move filesystem validation and service startup off the WPF thread so unavailable
  UNC paths and synchronous process startup cannot freeze the window.
- Keep the operation busy until validation/process shutdown finishes. Check
  cancellation before process validation and again before launch.
- Replace the open-ended output drain with one bounded batch and constant-time
  queue accounting. Show how many older lines were omitted.
- Capture scroll/selection state before replacing bound output and restore it
  after layout. Disable the read-only log's undo history.
- Add a visible validation panel, cancellation-button feedback, a status panel,
  keyboard-focus accents, and layout rounding for scaled displays.
- Explicitly import System.IO in UI-independent sources also compiled by WPF.
- Add regression tests for cancellation ownership, validation recovery, output
  retention across refreshes, and Windows locked-file/pre-canceled operations.
- Add Windows and Ubuntu CI, without new application dependencies or projects.

## Validation evidence

The current Work session is Linux and has no `dotnet` executable. Local
`dotnet restore Rovo.slnx` returned exit 127 (`dotnet: command not found`). The SDK
installation endpoint was unreachable. Local build, tests, WPF startup, and
Robocopy execution have not been claimed as successful.

GitHub Actions results will be recorded after the branch is pushed and checked.

## Still requires interactive Windows verification

1. Start the WPF application and inspect initial layout, error/status panels,
   keyboard focus, folder dialogs, minimum window size, and scrolling at 100%,
   150%, and 200% scaling, including moving between monitors with different DPI.
2. Use an unavailable UNC source/destination: the window must remain responsive,
   Cancel must stay pending until validation returns, and no copy may start after
   cancellation. Test actual denied-access folders with the intended user account.
3. During heavy output, scroll back, select/copy text, scroll horizontally, and
   return to the bottom. Check responsiveness and memory on a large file tree.
   Old lines intentionally disappear once the retention limit is reached.
4. Close during copying and during folder validation. No keeps the operation
   running; Yes requests cancellation and waits before closing. Cancel and restart
   repeatedly; check that no Robocopy process is left running after close.
5. Check Chinese/Japanese paths and output under the intended Windows OEM code
   page. Verify actual destination filenames separately from console rendering.

Automated Windows Robocopy tests do not exercise the WPF window, visual DPI
behavior, real UNC availability, or interactive close confirmation. No rollback,
filesystem-alias resolution, or new Phase 2 features are included.
