# Sound effect sources

All included sound effects were selected from **Kenney Interface Sounds 1.0**, released under Creative Commons Zero (CC0).

- Official asset page: https://kenney.nl/assets/interface-sounds
- CC0 license: https://creativecommons.org/publicdomain/zero/1.0/
- Creator: Kenney, https://kenney.nl/

The original OGG files were converted to mono PCM WAV so the WinForms application can play them through the Windows `SoundPlayer` API without an extra runtime codec. Output gain was reduced to 18 to 24 percent, silence was trimmed conservatively, and a 20 ms fade-out was applied. The conversion is reproducible with `tools/AudioAssetBuilder`.

| Included file | Original Kenney file | In-game use |
| --- | --- | --- |
| `ui-click.wav` | `click_004.ogg` | Small interface and object interaction |
| `locked.wav` | `toggle_002.ogg` | Locked star clock |
| `wrong.wav` | `error_006.ogg` | Incorrect puzzle or clock input |
| `puzzle-item.wav` | `confirmation_002.ogg` | Puzzle solved and memory shard acquired |
| `clock-restored.wav` | `maximize_006.ogg` | Star clock restored |
| `ending-bell.wav` | `bong_001.ogg` | Thirteenth bell and ending |
| `note-moon.wav` | `select_002.ogg` | Snow globe moon key |
| `note-tree.wav` | `select_004.ogg` | Snow globe tree key |
| `note-bell.wav` | `select_006.ogg` | Snow globe bell key |
| `note-star.wav` | `select_008.ogg` | Snow globe star key |
