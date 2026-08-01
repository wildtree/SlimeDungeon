# assets

Hand-drawn artwork. Files here are copied next to the executable on build, so **edit them here, never under
`bin/`** — that tree is regenerated and anything put there is lost on the next rebuild.

The game also looks back up into this folder directly, so replacing a picture and relaunching is usually
enough; a rebuild is only needed to refresh the copy that ships with a publish.

Every file is optional. When one is missing the game falls back to the artwork it draws procedurally, so a
clean checkout with an empty assets folder runs exactly as before.

| File | Size | Used for |
|------|------|----------|
| `guild.png` | 400×400 | The guild interior: counter, receptionist, the room behind them. Replaces the procedurally drawn room. The carved sign and date slate the code draws are suppressed when this is present, so include them in the picture; today's date is overlaid on a small plate in the top-right corner. |

PNG, JPG and WebP all load (SDL_image). PNG with transparency is the safe choice.
