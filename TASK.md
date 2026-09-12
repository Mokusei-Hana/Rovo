# Rovo

## Goal
Modern lightweight GUI frontend for Windows Robocopy.

## Tech Stack
- C#
- .NET 10
- WPF
- MVVM
- CommunityToolkit.Mvvm

## Architecture
- Keep the existing WPF application project and CommunityToolkit.Mvvm ViewModel.
- Views own dialogs, close confirmation, and UI-thread output updates.
- The ViewModel owns validation feedback, operation state, cancellation, and bounded output.
- Services own path validation, argument construction, and the Robocopy process.
- Models contain copy options and results. Do not add a framework or production project solely for tests.

## Phase 1 Requirements
The scope below records the existing README and implementation; it does not add a later phase.

- Select absolute source and destination folders (local or UNC).
- Copy new/changed files; retain destination-only files.
- Choose no subfolders, nonempty subfolders, or all subfolders.
- Configure nonnegative retry count and delay; defaults are 2 and 2 seconds.
- Show live output, completion outcome, and exit code.
- Cancel an operation and wait for Robocopy to exit before allowing another run or closing.

## UI / UX
- Native WPF, keyboard-accessible controls, readable errors, and indeterminate activity.
- Keep folder I/O and process startup off the UI thread, including unavailable UNC paths.
- Retain at most 10,000 output lines in each pending/visible buffer; flush a bounded batch on the UI thread.
- Preserve the output viewport when inspecting previous lines; disclose omitted older output.
- Keep the window usable at minimum size and 100%, 150%, and 200% Windows scaling.

## Robocopy Integration
- Launch the system Robocopy directly with ArgumentList, without shell execution or elevation.
- Use only the supported subfolder/retry switches plus `/XJ` and `/NP`.
- Drain stdout and stderr asynchronously using the Windows OEM code page.
- Treat exit codes 0–1 as completed, 2–7 as requiring review, and 8+ as failed.
- Reject equal/nested normalized paths; filesystem aliases and mapped drives are not resolved.
- Cancellation does not roll back copied files and may leave the current file incomplete.

## Out of Scope
Mirror/move/purge modes, custom flags, exclusions, saved jobs, persistence, schedules,
queues, log export, installers, WebView, large UI frameworks, databases, and plugins.

## Acceptance Criteria
- Restore and Release build the solution with the .NET 10 SDK.
- Pass UI-independent tests and Windows-only real Robocopy integration tests.
- Complete the manual Windows checks in README.md; cross-building does not verify WPF runtime behavior.
- Record actual validation evidence and any unavailable checks in VERIFICATION.md.

## Agent Workflow
1. Inspect
2. Plan
3. Implement
4. Build
5. Test
6. Review
