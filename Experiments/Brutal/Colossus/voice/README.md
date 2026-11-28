# The colossus's voice

`lines.fr.txt` and `lines.en.txt` are the colossus's lines, original, line for line. `fr/N.wav` and `en/N.wav` are
those lines recorded ahead, and the scene plays the ones in its panel's language. They play five semitones down in
French and two in English (`Soundscape.FrenchPitch`, `EnglishPitch`), through the same chest, growl and drive as Piper's
voice (`Soundscape.Speak`).

| What | Source | Licence |
|---|---|---|
| Speech model | Chatterbox Multilingual, Resemble AI, run offline from a local cache (`render.py`) | MIT |
| French voice | "Erick", from Kyutai's voice donations (`kyutai/tts-voices`, `voice-donations/Erick_enhanced.wav`), recorded and given to be used as a speech synthesis voice | CC0 1.0 |
| English voice | "Darius", from the same donations (`voice-donations/Darius_enhanced.wav`) | CC0 1.0 |
| Lines | written for this scene | as the repository |

The donated voices were chosen because their speakers gave them for this use. The model only takes their timbre: the
words and the delivery are generated.

## How they were made (2026-10-20)

- Settings: `exaggeration` 0.85 and `cfg_weight` 0.3, seed 0.
  - French line 1: `cfg_weight` 0.5, because lower values repeated "je te vois".
  - French lines 1 and 5: the ellipsis was replaced by a full stop or a comma in the text given to the model. With the
    ellipsis, it repeated the end of the line.
- Check: each WAV was transcribed back with Whisper large-v3-turbo. All ten match their line word for word; only the
  ellipses differ.
- Pitch (pYIN median): about 104 Hz for Erick's French and 77 to 85 Hz for Darius's English. Before the scene's pitch,
  both are under 120 Hz.
