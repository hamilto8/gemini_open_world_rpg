# UI and Accessibility Authoring

The production UI shell lives in `scenes/ui/UIShell.tscn`. It owns modal navigation, keyboard/controller
focus, pause state, safe-area scaling, feedback, input glyphs, and preferences. Gameplay code should
publish UI events; it should never search for or mutate UI nodes.

## Add or replace a screen

1. Duplicate a screen in `scenes/ui/screens/` and keep `UIScreen` (or a small subclass) on its root.
2. Put the initial keyboard/controller focus target in `DefaultFocusPath`.
3. Add Back buttons to the `ui_back` group. Add navigation buttons to `ui_open_screen` and set their
   integer `target_screen` metadata to the matching `UIScreenId`.
4. Register the packed scene in `resources/ui/screen_registry.tres`. The registry validates missing,
   duplicate, and empty entries at boot.
5. Keep layouts container-driven. The shell supplies safe-area margins and the shared theme; avoid fixed
   viewport coordinates inside screens.

UI artists can replace `resources/ui/meridian_theme.tres`, individual `.tscn` layouts, and control glyph
art without changing navigation code. Screen text uses translation keys where it is stable. Add another
`Translation` resource to `UIShell.Translations` for each locale.

## Open UI from gameplay

Publish `UIScreenRequestedEvent` on `IEventBus`. Use `ReplaceTop` for state transitions and the default
push behavior for a child surface. Inventory, equipment, journal, map, and dialogue are deliberately
gray-box view hosts: content systems should provide view models/adapters rather than owning their nodes.

Publish `UIFeedbackEvent` for bounded notices (information, success, failure, quest, or save) and
`UIHoldProgressEvent` for hold interactions. The presenter queues messages and avoids timer races.
`HudNoticeEvent` remains supported for existing gameplay code.

## Settings and accessibility

`IUserInterfaceSettingsService` stores preferences in `user://preferences.json`, independently of save
slots. Writes use temporary-file replacement; malformed or unavailable files fall back to accessible
defaults. The settings screen currently exposes subtitles, text/HUD scale, safe area, contrast, reduced
motion, camera inversion, and master volume. The model also defines music/effects/dialogue volume, color
vision mode, screen shake, toggle aim/sprint, and mouse/controller sensitivity for specialized consumers.

Camera, subtitle, audio, and HUD implementations consume `UserInterfaceSettingsChangedEvent`; they must
not read the settings file themselves. Audio bus application is centralized in the settings service.

## Input and focus checklist

- Every interactive screen has a deterministic default focus target and a visible focus style.
- `ui_cancel` pops one level; the pause action closes all child surfaces before resuming play.
- All controls must work without a mouse. Use at least a 44-pixel authored target height.
- Rebinding detects conflicts within the same gameplay context and asks before replacement. Vehicle-only
  actions may intentionally share keys with on-foot actions. Restore Defaults updates both runtime input
  and persisted bindings.
- `IInputGlyphService` switches prompts with the last active device and identifies Xbox, PlayStation,
  Nintendo, or generic controller families. Text labels are the accessible fallback until final glyph art.

Before handoff, run `dotnet test Meridian.sln --no-restore`, open every screen at 1280×720 and ultrawide,
and navigate each screen using keyboard and controller only. Final art must preserve contrast, focus rings,
safe-area containment, and localization expansion room.
