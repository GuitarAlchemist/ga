unit uMidi;

interface

uses
   Classes
   ,ExtCtrls
   ,uMusicClasses
   ,uChordVoicings
   ;

type
   TMidi = class
      private
         fMidiOutHandle : integer;
         fMidiDevice : cardinal;
         fInstrument : byte;
         fMuteTimer : TTimer;
         fNoteDuration : cardinal;
         procedure OpenMidiOut;
         procedure CloseMidiOut;
         procedure DoMuteTimer(sender : TObject);
         procedure ResetMuteTimer;
         procedure SetInstrument(value : byte);
         procedure SetMidiDevice(value : cardinal);
         procedure SetNoteDuration(value : cardinal);
     public
         constructor Create;
         destructor Destroy; override;
         class procedure InitializeMidiObject;
         class procedure FinalizeMidiObject;
         class function GetMidiObject : TMidi;
         procedure Initialize;
         procedure NoteOn(note : integer);
         procedure NoteOff(note : integer);
         procedure AllNotesOff;
         function PlayScale(scaleRoot : THalfTone; scaleName : string; scaleMode : integer = 1) : boolean;
         procedure PlayChordTemplate(chordTemplate : TChordTemplate;
                            root: THalfTone;
                            halfToneIncrement : integer = 0;
                            octave : TOctave = 1
                           );
         procedure PlayChordVoicing(chordVoicing : TChordVoicing);
         property instrument : byte read fInstrument write SetInstrument;
         property midiDevice : cardinal read fMidiDevice write SetMidiDevice;
         property noteDuration : cardinal read fNoteDuration write SetNoteDuration;
   end;

   RMIDIDeviceCaps = record
      manufacturer : string;
      driverVersion : string;
      technology : string;
      voices : string;
      notes : string;
      support : string;
   end;


const
   DEFAULT_NOTE_DURATION = 1500;

   INSTRUMENT_NAMES : array [0..29] of string=(
      'Guitar - Acoustic nylon',
      'Guitar - Acoustic steel',
      'Guitar - Jazz electric',
      'Guitar - Clean electric',
      'Guitar - Overdriven',
      'Guitar - Distortion',

      'Piano - Acoustic grand piano',
      'Piano - Bright acoustic piano',
      'Piano - Electric grand piano',
      'Piano - Honky tonk piano',
      'Piano - Electric piano 1',
      'Piano - Electric piano 2',

      'Organ - Drawbar organ',
      'Organ - Percussive organ ',
      'Organ - Rock organ',
      'Organ - Church organ',
      'Organ - Reed organ',

      'Bass - Acoustic bass',
      'Bass - Fingered electric bass',
      'Bass - Picked electric bass',
      'Bass - Fretless bass',
      'Bass - Slap bass 1',
      'Bass - Slap bass 2',
      'Bass - Synth bass 1',
      'Bass - Synth bass 2',

      'Synth - String ensemble 1',
      'Synth - String ensemble 2',
      'Synth - Synth strings 1',
      'Synth - Synth strings 2',
      'Synth - Synth choir'
   );

   INSTRUMENTS : array [0..29] of byte=(
      24, 25, 26, 27, 29, 30,
      0, 1, 2, 3, 4, 5,
      16, 17, 18, 19, 20,
      32, 33, 34, 35, 36, 37, 38, 39,
      48, 49, 50, 51, 54
   );

   
{
   // Modulation
   //midiOutShortMsg(hMidiOut, $007F01B0);

   // Effect
   //midiOutShortMsg(hMidiOut, $007F5BB0);

   // Chorus
   //midiOutShortMsg(hMidiOut, $FF7F5DB0);

   // Phaser
   //midiOutShortMsg(hMidiOut, $007F5FB0);
}


procedure GetMIDIDeviceNames(names : TStrings);
function GetMIDIDeviceCaps(deviceIndex : cardinal) : RMIDIDeviceCaps;
function InstumentNameToIndex(instrumentName : string) : integer;
function DeviceNameToIndex(deviceName : string) : cardinal;

function globalMidi : TMidi;

const
   INSTRUMENT_GUITAR_NYLON = 24;
   INSTRUMENT_GUITAR_ACCOUSTIC_STEEL = 25;
   INSTRUMENT_GUITAR_JAZZ_ELECTRIC = 26;


implementation

uses
   MMSystem
   ,SysUtils
   ;

const
   DEFAULT_INSTRUMENT = INSTRUMENT_GUITAR_JAZZ_ELECTRIC;

var
   midiObject : TMidi;


procedure GetMIDIDeviceNames(names : TStrings);
var
   index, devCount : integer;
   devCaps : TMidiOutCapsA;
begin
   devCount := midiOutGetNumDevs;
   names.BeginUpdate;
   try
      names.Clear;
      for index := 0 to devCount - 1 do
         begin
            midiOutGetDevCaps(index, @devCaps, SizeOf(devCaps));
            names.Add(devCaps.szPname);
         end;
   finally
      names.EndUpdate;
   end;
end;

function GetMIDIDeviceCaps(deviceIndex : cardinal) : RMIDIDeviceCaps;
var
   devCaps : TMidiOutCapsA;
begin
   midiOutGetDevCaps(deviceIndex, @devCaps, SizeOf(devCaps));

   case devCaps.wPid of
      1: result.manufacturer := 'Microsoft Corporation';
      2: result.manufacturer := 'Creative Labs Inc.';
      3: result.manufacturer := 'Media Vision Inc.';
      4: result.manufacturer := 'Fujitsu';
      20: result.manufacturer := 'Artisoft Inc.';
      21: result.manufacturer := 'Turtle beach';
      22: result.manufacturer := 'IBM Corp.';
      23: result.manufacturer := 'Vocaltec Ltd.';
      24: result.manufacturer := 'Roland';
      25: result.manufacturer := 'DigiSpeech';
      26: result.manufacturer := 'Nec';
      27: result.manufacturer := 'ATI';
      28: result.manufacturer := 'Wang Laboratories, Inc.';
      29: result.manufacturer := 'Tandy Corporation';
      30: result.manufacturer := 'Voyetra';
      31: result.manufacturer := 'Antex';
      32: result.manufacturer := 'Icl ps';
      33: result.manufacturer := 'Intel';
      34: result.manufacturer := 'Gravis';
      35: result.manufacturer := 'Video Associates Labs';
      36: result.manufacturer := 'InterActive, Inc.';
      37: result.manufacturer := 'Yamaha Corp. of America';
      38: result.manufacturer := 'Everex Systems, Inc.';
      39: result.manufacturer := 'Echo Speech Corporation';
      40: result.manufacturer := 'Sierra Semiconductor';
      41: result.manufacturer := 'Computer Aided Technologies';
      42: result.manufacturer := 'APPS Software International';
      43: result.manufacturer := 'DSP Group, Inc.';
      44: result.manufacturer := 'MicroEngineering Labs';
      45: result.manufacturer := 'Computer Friends, Inc';
      46: result.manufacturer := 'ESS Technology';
      47: result.manufacturer := 'Audio, Inc.';
      else result.manufacturer := 'Unknown';
   end;

   result.driverVersion := IntToStr((devCaps.vDriverVersion div 100) * 100) + '.' + IntToStr(devCaps.vDriverVersion mod 100);

   case devCaps.wTechnology of
      MOD_FMSYNTH: result.technology := 'FM synthesizer';
      MOD_MAPPER: result.technology := 'Microsoft MIDI mapper';
      MOD_MIDIPORT: result.technology := 'MIDI hardware port';
      MOD_SQSYNTH: result.technology := 'Square wave synthesizer';
      MOD_SYNTH: result.technology := 'Synthesizer';
      else result.technology := 'Unknown';
   end;

   result.voices := IntToStr(devCaps.wVoices);

   result.notes := IntToStr(devCaps.wNotes);

   if (devCaps.dwSupport and MIDICAPS_LRVOLUME) = MIDICAPS_LRVOLUME
      then result.support := 'Separate left-right volume control'
      else result.support := '';
end;

function InstumentNameToIndex(instrumentName : string) : integer;
var
   index : integer;
begin
   result := 0;
   for index := 0 to High(INSTRUMENT_NAMES)
      do if SameText(INSTRUMENT_NAMES[index], instrumentName)
            then begin
                    result := index;
                    Break;
                 end;
end;

function DeviceNameToIndex(deviceName : string) : cardinal;
var
   deviceNames : TStringList;
   index : integer;
begin
   deviceNames := TStringList.Create;
   try
      GetMIDIDeviceNames(deviceNames);
      index := deviceNames.IndexOf(deviceName);
      if index < 0
         then result := MIDI_MAPPER
         else result := index;
   finally
      deviceNames.Free;
   end;
end;

function globalMidi : TMidi;
begin
   result := TMidi.GetMidiObject;
end;

function TMidi.PlayScale(scaleRoot : THalfTone; scaleName : string; scaleMode : integer) : boolean;
var
   scale : TScale;
   index : integer;
   note : integer;
begin
   AllNotesOff;

   // Retrieve the scale notes
   scale := TScale.Create;
   try
      result := globalScaleRepository.GetScale(scale, scaleName, scaleMode);
      if result
         then begin
                 note := 48 + Ord(scaleRoot);
                 for index := 0 to scale.count + 1 do
                    begin
                       NoteOn(note);
                       Sleep(150);
                       note := note + scale.degreeInterval[index];
                    end;
              end;
   finally
      scale.Free;
   end;
end;

procedure TMidi.PlayChordTemplate(chordTemplate : TChordTemplate;
                                  root: THalfTone;
                                  halfToneIncrement : integer;
                                  octave : TOctave
                                 );
var
   rootNote : integer;
   index : integer;
begin
   // Inits
   AllNotesOff;
   rootNote := 36 + Ord(root) + halfToneIncrement + octave * 12;

   // Retrieve the scale notes
   for index := 0 to chordTemplate.chordDegreesCount - 1 do
      begin
         NoteOn(rootNote + Ord(chordTemplate.chordDegrees[index]));
      end;
end;

procedure TMidi.PlayChordVoicing(chordVoicing : TChordVoicing);
var
   index : integer;
   position : RStringPosition;
   halfTone : RHalfTone;
   note : integer;
begin
   AllNotesOff;
   for index := 0 to 5 do
      begin
         position := chordVoicing.positions[index];
         if position.fret <> -1
            then begin
                    halfTone := position.halfTone;
                    note := 36 + halfTone.octave * 12+ Ord(halfTone.halfTone);
                    NoteOn(note);
                 end;
      end;
end;

{ TMidi }

constructor TMidi.Create;
begin
   fMidiOutHandle := 0;
   fMidiDevice := MIDI_MAPPER; // Use midi mapper by default
   fInstrument := 0; // Piano by default
   fMuteTimer := TTimer.Create(nil);
   fNoteDuration := DEFAULT_NOTE_DURATION;
   fMuteTimer.Interval := fNoteDuration;
   fMuteTimer.OnTimer := DoMuteTimer;
end;

destructor TMidi.Destroy;
begin
   fMuteTimer.Free;
   CloseMidiOut;
end;

procedure TMidi.AllNotesOff;
begin
   midiOutShortMsg(fMidiOutHandle, $00007BB0);
end;

procedure TMidi.NoteOff(note: integer);
var
   data : record case byte of
      0 : (b1, b2, b3, b4 : byte);
      1 : (l : LongInt);
   end;
begin
   OpenMidiOut;
   with data do
      begin
         b1 := $80;
         b2 := 0;
         b3 := 0;
         b4 := 0;
         midiOutShortMsg(fMidiOutHandle, l);
      end;
end;

procedure TMidi.Initialize;
begin
   OpenMidiOut;
end;

procedure TMidi.NoteOn(note: integer);
var
   data : record case byte of
      0 : (b1, b2, b3, b4 : byte);
      1 : (l : LongInt);
   end;
begin
   OpenMidiOut;
   with data do
      begin
         b1 := $90;
         b2 := note;
         b3 := 127;
         b4 := 0;
         midiOutShortMsg(fMidiOutHandle, l);
      end;
   ResetMuteTimer;
end;

procedure TMidi.OpenMidiOut;
begin
   if fMidiOutHandle = 0
      then begin
              if midiOutOpen(@fMidiOutHandle, fMidiDevice, CALLBACK_NULL, 0, 0) <> MMSYSERR_NOERROR
                 then begin
                         fMidiOutHandle := 0;
                         raise Exception.Create('Unable to open MIDI port');
                      end;

              // Reset controllers
              midiOutShortMsg(fMidiOutHandle, $000079B0);

              // Change to default instrument
              SetInstrument(DEFAULT_INSTRUMENT);
           end;
end;

procedure TMidi.CloseMidiOut;
begin
   if fMidiOutHandle <> 0
      then begin
              midiOutClose(fMidiOutHandle);
              fMidiOutHandle := 0;
           end;
end;

procedure TMidi.SetInstrument(value : byte);
var
   data : record case byte of
      0 : (b1, b2, b3, b4 : byte);
      1 : (l : LongInt);
   end;
begin
   if value <> fInstrument
      then begin
              OpenMidiOut;
              with data do
                 begin
                    b1 := $C0;
                    b2 := value;
                    b3 := 0;
                    b4 := 0;
                    midiOutShortMsg(fMidiOutHandle, l);
                 end;
              fInstrument := value;
           end;
end;

procedure TMidi.SetMidiDevice(value : cardinal);
begin
   if value <> fMidiDevice
      then begin
              CloseMidiOut;
              fMidiDevice := value;
              OpenMidiOut;
           end;
end;

procedure TMidi.SetNoteDuration(value : cardinal);
begin
   if value <> fNoteDuration
      then begin
              fMuteTimer.Interval := value;
              fNoteDuration := value;
           end;
end;

procedure TMidi.DoMuteTimer(sender : TObject);
begin
   fMuteTimer.enabled := false;
   AllNotesOff;
end;

procedure TMidi.ResetMuteTimer;
begin
   fMuteTimer.enabled := false;
   fMuteTimer.enabled := true;
end;

class procedure TMidi.InitializeMidiObject;
begin
   Assert(midiObject = nil);

   midiObject := TMidi.Create;
end;

class procedure TMidi.FinalizeMidiObject;
begin
   Assert(midiObject <> nil);

   midiObject.Free;
   midiObject := nil;
end;

class function TMidi.GetMidiObject : TMidi;
begin
   Assert(midiObject <> nil);

   result := midiObject;
end;

end.
