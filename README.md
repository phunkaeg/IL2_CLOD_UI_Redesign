# PlaneLoadoutWpfTest v5 — reactive pass

This pass focuses on the visual polish items from your latest feedback.

## New in this version
- **Themed tabs** that match the rest of the interface instead of the generic WPF tab look.
- **User-supplied background** image used as the main page atmosphere.
- **User-supplied blueprint overlay** added at reduced opacity.
- **User-supplied top and front silhouettes** integrated into the convergence panel.
- **Subtle reactive lighting**: a soft mouse-following highlight over the main content area.
- **Animated panel transitions** when switching between Weapon Sets and Guns.
- **Fixed convergence preview** with actual horizontal and vertical controls plus working preview diagrams.
- Custom dark combo boxes, scroll bars, check boxes, sliders and buttons.
- Shared theme in `Theme.xaml`.

## Run
```powershell
cd PlaneLoadoutWpfTest_v5_reactive
dotnet run
```

## Notes
This is still a prototype pass, but it should be a much better base for future screens.
If you want, the next logical step is to split each screen into separate `UserControl`s and start treating this like a reusable UI kit.