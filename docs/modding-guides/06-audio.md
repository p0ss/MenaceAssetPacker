# Audio Replacement

Sound brings Menace to life - weapon fire, footsteps, explosions, and the ambient soundtrack. This guide covers replacing audio assets to customize the game's soundscape.

## How Audio Replacement Works

Like other asset replacements, audio modding works through runtime interception. When the game requests a sound file, the Modpack Loader checks if your modpack provides a replacement. If found, your audio plays instead of the original.

The original game files remain untouched - your replacements exist only in memory while the mod is active.

## Supported Formats

The Modpack Loader supports:

- **WAV** - Uncompressed, highest quality, larger files
- **OGG** - Compressed, good quality, smaller files (recommended)

**Recommendations:**
- Use **OGG** for most sounds - good balance of quality and size
- Use **WAV** for very short sounds where compression artifacts might be noticeable
- Match the original's sample rate when possible (typically 44100 Hz or 48000 Hz)

## Step 1: Find Audio Assets

Use the Modkit to locate the sounds you want to replace:

1. Go to **Modding Tools > Assets**
2. Filter by type or search for audio files
3. Look for common prefixes:
   - `SFX_` - Sound effects
   - `AMB_` - Ambient/environment sounds
   - `UI_` - Interface sounds
   - `MUS_` - Music tracks
   - `VO_` - Voice lines (if applicable)

4. Note the full asset path (e.g., `Assets/Audio/SFX/Weapons/Rifle_Fire.ogg`)

You can also browse the extracted assets folder to preview sounds with your system audio player.

## Step 2: Create Your Replacement

When creating replacement audio:

**Technical requirements:**
- Stereo or mono (match the original)
- Sample rate: 44100 Hz or 48000 Hz
- Bit depth: 16-bit (standard) or 24-bit

**Tools you can use:**
- [Audacity](https://www.audacityteam.org/) - Free, full-featured
- [Ocenaudio](https://www.ocenaudio.com/) - Free, simple interface
- Any DAW (FL Studio, Ableton, Reaper, etc.)

## Step 3: Add to Modpack

Place your audio files in the modpack's `assets/` folder:

```
MyMod-modpack/
  modpack.json
  assets/
    audio/
      rifle_shot.ogg
      explosion.ogg
```

Reference them in your `modpack.json`:

```json
{
  "manifestVersion": 2,
  "name": "CustomAudio",
  "version": "1.0.0",
  "assets": {
    "Assets/Audio/SFX/Weapons/Rifle_Fire.ogg": "assets/audio/rifle_shot.ogg",
    "Assets/Audio/SFX/Explosions/Grenade.ogg": "assets/audio/explosion.ogg"
  }
}
```

## Example: Replacing a Weapon Sound

Let's replace the assault rifle's firing sound with a custom one.

### 1. Find the original

In the Asset Browser, search for "rifle" or browse to `Assets/Audio/SFX/Weapons/`. You'll find files like:

- `AssaultRifle_Fire.ogg` - Single shot
- `AssaultRifle_FireLoop.ogg` - Sustained fire
- `AssaultRifle_Reload.ogg` - Magazine reload
- `AssaultRifle_Empty.ogg` - Dry fire click

### 2. Prepare your replacement

Open Audacity and create or import your custom gunshot sound:

1. Trim to appropriate length (match original duration roughly)
2. Normalize volume (see Audio Mixing section below)
3. Export as OGG Vorbis, Quality 6-8

### 3. Create the modpack

```
CustomRifle-modpack/
  modpack.json
  assets/
    audio/
      my_rifle_fire.ogg
      my_rifle_reload.ogg
```

```json
{
  "manifestVersion": 2,
  "name": "CustomRifleAudio",
  "version": "1.0.0",
  "description": "Replaces assault rifle sounds",
  "assets": {
    "Assets/Audio/SFX/Weapons/AssaultRifle_Fire.ogg": "assets/audio/my_rifle_fire.ogg",
    "Assets/Audio/SFX/Weapons/AssaultRifle_Reload.ogg": "assets/audio/my_rifle_reload.ogg"
  }
}
```

### 4. Test it

Enable the mod, start the game, and fire the assault rifle. You should hear your custom sound.

## Ambient and Environment Sounds

Ambient sounds create atmosphere - wind, rain, distant machinery, wildlife. These typically loop seamlessly.

Common ambient audio paths:
```
Assets/Audio/AMB/Forest_Day.ogg
Assets/Audio/AMB/Industrial_Hum.ogg
Assets/Audio/AMB/Rain_Light.ogg
Assets/Audio/AMB/Wind_Strong.ogg
```

**Tips for ambient replacements:**
- Ensure seamless looping (no clicks at loop points)
- Keep consistent volume throughout
- Match the mood of the environment
- Test loop points using Audacity's loop preview

Example ambient replacement:

```json
{
  "manifestVersion": 2,
  "name": "CustomAmbience",
  "version": "1.0.0",
  "assets": {
    "Assets/Audio/AMB/Forest_Day.ogg": "assets/audio/custom_forest.ogg",
    "Assets/Audio/AMB/Rain_Light.ogg": "assets/audio/custom_rain.ogg"
  }
}
```

## UI Sounds

Interface sounds provide feedback for button clicks, menu navigation, and notifications.

Common UI audio paths:
```
Assets/Audio/UI/Button_Click.ogg
Assets/Audio/UI/Button_Hover.ogg
Assets/Audio/UI/Menu_Open.ogg
Assets/Audio/UI/Menu_Close.ogg
Assets/Audio/UI/Notification.ogg
Assets/Audio/UI/Error.ogg
```

**Tips for UI sound replacements:**
- Keep them short and snappy
- Avoid sounds that would be annoying with repetition
- Match volume levels to original UI sounds
- Test rapid clicking to ensure they don't overlap poorly

Example UI sound pack:

```json
{
  "manifestVersion": 2,
  "name": "CustomUISounds",
  "version": "1.0.0",
  "assets": {
    "Assets/Audio/UI/Button_Click.ogg": "assets/ui/click.ogg",
    "Assets/Audio/UI/Button_Hover.ogg": "assets/ui/hover.ogg",
    "Assets/Audio/UI/Notification.ogg": "assets/ui/notify.ogg"
  }
}
```

## Music Replacement

Music tracks are typically longer files that loop during gameplay or play in menus.

Common music paths:
```
Assets/Audio/MUS/Menu_Theme.ogg
Assets/Audio/MUS/Combat_Intense.ogg
Assets/Audio/MUS/Combat_Ambient.ogg
Assets/Audio/MUS/Victory.ogg
Assets/Audio/MUS/Defeat.ogg
```

**Tips for music replacements:**
- Match the mood and pacing of the original
- Ensure clean loop points for looping tracks
- Consider the context (combat music should be energetic, menu music relaxed)
- Test transitions between music states

Example music replacement:

```json
{
  "manifestVersion": 2,
  "name": "CustomSoundtrack",
  "version": "1.0.0",
  "description": "Custom music replacements",
  "assets": {
    "Assets/Audio/MUS/Menu_Theme.ogg": "assets/music/my_menu_theme.ogg",
    "Assets/Audio/MUS/Combat_Intense.ogg": "assets/music/my_battle_music.ogg"
  }
}
```

**Licensing reminder:** If distributing your mod, ensure you have rights to any music you include. Original compositions or Creative Commons licensed music are safe choices.

## Audio Mixing Considerations

Volume consistency is critical. Sounds that are too loud or too quiet relative to others break immersion and can be jarring.

### Volume Normalization

Before adding audio to your modpack, normalize volumes to match the game's levels:

1. **Extract the original** sound you're replacing
2. **Measure its peak level** in Audacity (Analyze > Find Peak)
3. **Adjust your replacement** to match that peak level
4. **Listen in context** - compare with other game sounds

### Recommended Levels

| Sound Type | Typical Peak Level |
|------------|-------------------|
| Weapon fire | -6 dB to -3 dB |
| Explosions | -6 dB to -1 dB |
| Footsteps | -18 dB to -12 dB |
| UI clicks | -12 dB to -9 dB |
| Ambient loops | -24 dB to -18 dB |
| Music | -12 dB to -6 dB |

These are rough guidelines - always compare against the original.

### Avoiding Clipping

Never let your audio exceed 0 dB - this causes harsh distortion. Leave at least 1-2 dB of headroom.

In Audacity:
1. Select all audio
2. Effect > Normalize
3. Set peak amplitude to -1.0 dB
4. Apply

### Compression for Consistency

For sounds with wide dynamic range (quiet and loud parts), apply gentle compression:

1. Effect > Compressor
2. Threshold: -12 dB
3. Ratio: 2:1 to 4:1
4. Attack: 10-50 ms
5. Release: 100-300 ms

This evens out volume while preserving dynamics.

## Testing Audio Mods

### Quick Test Workflow

1. **Enable your mod** in the Modkit
2. **Launch the game**
3. **Trigger the sound** (fire weapon, click button, enter area)
4. **Listen carefully** for:
   - Volume level compared to other sounds
   - Audio quality (no unwanted distortion)
   - Timing (does it feel right?)
   - Looping (for ambient/music)

### Common Issues

**Sound doesn't play**
- Check the asset path (case-sensitive!)
- Verify the audio file isn't corrupted (play it directly)
- Check Modkit output for loading errors
- Ensure the file format is supported (WAV/OGG)

**Sound is too loud/quiet**
- Normalize to match the original's volume
- Measure peak levels and adjust

**Sound has clicks or pops**
- Check start/end points for zero-crossings
- Add tiny fade-in/fade-out (5-10 ms)

**Loop has audible seam**
- Ensure the waveform matches at loop point
- Use crossfade looping in your audio editor

**Sound plays at wrong speed**
- Sample rate mismatch - re-export at 44100 Hz or 48000 Hz

### Batch Testing

For mods with many audio replacements, test systematically:

1. Make a checklist of all replaced sounds
2. Test each one in-game
3. Note any issues
4. Iterate and re-test

## Complete Audio Mod Example

Here's a complete modpack replacing multiple audio categories:

```
CustomAudioPack-modpack/
  modpack.json
  assets/
    weapons/
      rifle_fire.ogg
      shotgun_fire.ogg
      pistol_fire.ogg
    ambient/
      forest_ambience.ogg
      rain_loop.ogg
    ui/
      button_click.ogg
      menu_open.ogg
    music/
      menu_theme.ogg
```

```json
{
  "manifestVersion": 2,
  "name": "CustomAudioPack",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Complete audio overhaul - weapons, ambience, UI, and music",
  "assets": {
    "Assets/Audio/SFX/Weapons/AssaultRifle_Fire.ogg": "assets/weapons/rifle_fire.ogg",
    "Assets/Audio/SFX/Weapons/Shotgun_Fire.ogg": "assets/weapons/shotgun_fire.ogg",
    "Assets/Audio/SFX/Weapons/Pistol_Fire.ogg": "assets/weapons/pistol_fire.ogg",
    "Assets/Audio/AMB/Forest_Day.ogg": "assets/ambient/forest_ambience.ogg",
    "Assets/Audio/AMB/Rain_Light.ogg": "assets/ambient/rain_loop.ogg",
    "Assets/Audio/UI/Button_Click.ogg": "assets/ui/button_click.ogg",
    "Assets/Audio/UI/Menu_Open.ogg": "assets/ui/menu_open.ogg",
    "Assets/Audio/MUS/Menu_Theme.ogg": "assets/music/menu_theme.ogg"
  }
}
```

## Tips for Great Audio Mods

**Match the game's style**
Menace has a particular audio aesthetic. Listen to original sounds and try to match that quality and character.

**Consider spatial audio**
3D sounds in-game may behave differently than in your editor. Test at various distances.

**Mind file sizes**
Large audio files increase mod download size. Use OGG compression effectively.

**Document your sources**
If you use sounds from libraries, note where they came from for licensing compliance.

---

**Next:** [SDK Basics](07-sdk-basics.md)
