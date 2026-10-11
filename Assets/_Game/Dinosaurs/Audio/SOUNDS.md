# Sound Checklist

Drop each sound into its folder using the file name shown. The names let the scripts and prefabs find the right clip later.
★ = most important for the demo.

**Format:** `.wav`, 16-bit, 44.1 or 48 kHz, **mono** for anything that comes from a place in the world (dinosaurs, arrow hits). Stereo `.wav` or `.ogg` is fine for music, ambience and UI. All of these go through Git LFS automatically.
**Before adding:** trim the silence at the start and end, and keep loops seamless (the end flows into the start).

## Bow/
- [ ] `bow_draw_loop.wav`: string creak while drawing (loop)
- [ ] ★ `bow_release.wav`: string twang
- [ ] `arrow_whoosh.wav`: arrow in flight (short)
- [ ] ★ `arrow_hit_flesh.wav`: thud into a dinosaur
- [ ] `arrow_hit_ground.wav`: thunk into the ground or a tree
- [ ] ★ `arrow_hit_head.wav`: heavier crunch for a headshot

## Dinosaurs/Maip/ and Dinosaurs/Gorgosaurus/
Same set in each folder. The Maip should sound light and fast, the Gorgosaurus heavy and deep.
- [ ] ★ `<name>_footstep_01.wav`, `_02`, `_03`: a few variations
- [ ] `<name>_spawn_call.wav`: distant roar when it appears
- [ ] `<name>_sniff.wav`: growl or sniff when it pauses
- [ ] ★ `<name>_charge_roar.wav`: final run-in
- [ ] ★ `<name>_bite.wav`: jaw snap
- [ ] `<name>_hurt.wav`: hit in the body
- [ ] `<name>_hurt_leg.wav`: hit in the leg (limping yelp)
- [ ] ★ `<name>_death.wav`: death cry
- [ ] `<name>_collapse.wav`: body hitting the ground

(`<name>` = `maip` or `gorgosaurus`)

## Player/
- [ ] ★ `player_hurt.wav`: grunt or impact
- [ ] `player_heartbeat_loop.wav`: low health (loop)
- [ ] `player_death.wav`

## UI/
- [ ] `ui_calibrate_prompt.wav`: "relax" and "flex" chime
- [ ] `ui_calibrate_done.wav`
- [ ] ★ `ui_countdown_beep.wav` and `ui_countdown_go.wav`
- [ ] `ui_timer_tick.wav`: last 10 seconds
- [ ] ★ `ui_win.wav`: Act 1 complete
- [ ] ★ `ui_death.wav`: death screen
- [ ] `ui_click.wav`: restart and confirm

## Ambience/
- [ ] ★ `amb_forest_wind_loop.wav` (or `.ogg`)

## Music/
- [ ] `music_survive_loop.ogg`: tense, for Survive
- [ ] `music_calm_loop.ogg`: optional, for Calibrate and Countdown

---
Credits: write down where each sound came from (site, author, license) so it can go in `CREDITS.md`.
