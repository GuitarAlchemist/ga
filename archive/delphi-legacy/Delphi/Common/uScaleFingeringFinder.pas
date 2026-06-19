unit uScalePatternFinder;

interface

uses
   uMusicClasses
   ,Classes
   ,Contnrs
   ;

type
   TFretBoardPosition = class
      private
         fStr : TString;
         fFret : TFret;
         fFinger : integer;
         fHalfTone : THalfTone;
         fTone : TTone;
         fOctave : smallint;
         fQuality : TToneQuality;
         fQualityAccidental : TNoteAccidental;
         fCharacterTone : TCharacterTone;
      public
         constructor Create;
         property str : TString read fStr;
         property fret : TFret read fFret;
         property finger : integer read fFinger;
         property quality : TToneQuality read fQuality;
         property qualityAccidental : TNoteAccidental read fQualityAccidental;
         property tone : TTone read fTone;
         property characterTone : TCharacterTone read fCharacterTone;
   end;

   TScalePatternFinder = class(TComponent)
      // Object core
      private
         fPatternPositions : TObjectList;
         fOtherPositions : TObjectList;
         fScale : TScale;

      // Positions computing
      private
         procedure AddPosition(str, fret : integer;
                               patternPosition : boolean = true;
                               prepend : boolean = false; reverse : boolean = false);
      public
         procedure Compute(note : THalfTone;
                           scaleName : string; scaleMode : integer;
                           pattern : integer; extended : boolean;
                           patternPositions, otherPositions : TObjectList);

      // Constructor, destructor and properties
      public
         constructor Create(fretCount : integer);
         destructor Destroy; override;
         property patternPositions : TObjectList read fPatternPositions;
         property otherPositions : TObjectList read fOtherPositions;
         property scale : TScale read fScale;
   end;

implementation

const
   STRING_COUNT = 6;
   MAX_STRING_EXTENT = 5;


{ TScalePatternFinder }

constructor TScalePatternFinder.Create(fretCount : integer);
begin
   fScale := TScale.Create;
end;

destructor TScalePatternFinder.Destroy;
begin
   fScale.Free;
   inherited;
end;

procedure TScalePatternFinder.AddPosition(str, fret : integer;
                                            patternPosition, prepend, reverse : boolean);
var
   position : TFretBoardPosition;
   index : integer;
   staffTone : RStaffTone;
   note : TNote;
   accidental : TNoteAccidental;
begin
   // Create the new position and prepend or append it to the list
   position := TFretBoardPosition.Create;
   if prepend
      then begin
              fPatternPositions.Insert(0, position);
              index := 0;
           end
      else index := fPatternPositions.Add(position);

   // Assign str, fret, quality and useSharp properties
   position.fStr := str;
   position.fFret := fret;

   // Compute the tone, the accidental and the character tone properties
end;

procedure TScalePatternFinder.Compute(note : THalfTone;
                                        scaleName : string; scaleMode : integer;
                                        pattern : integer; extended : boolean;
                                        patternPositions, otherPositions : TObjectList);
var
   pass : integer;
   index : integer;
   increment : integer;
   str : TString;
   fret : TFret;
   stringJumps : integer;
   stringIncrement : integer;
   stringPosCount : integer;
   minStringFret : integer;
   minFret : integer;
   maxFret : integer;
   invalidFret : integer;
   position : TFretBoardPosition;

   halfTonesFromRootNote : smallint;
   tonesFromRootNote : smallint;
   degreeNote : smallint;
   absDegreeNote : smallint;
   absRootNote : smallint;
begin
   // Inits
   fPatternPositions := patternPositions;
   fOtherPositions := otherPositions;
   globalScaleRepository.GetScale(fScale, scaleName);
   invalidFret := MaxInt;

   halfTonesFromRootNote := 0;
   for index := 0 to scaleMode - 2 do
      halfTonesFromRootNote := (halfTonesFromRootNote + fScale.degreeInterval[index]) mod 12;
   tonesFromRootNote := DIATONIC_TONE[halfTonesFromRootNote];

   // Compute pattern positions
   if patternPositions <> nil
      then begin
              patternPositions.Clear;

              absDegreeNote := halfTonesFromRootNote;
              for pass := 0 to 1 do
                 begin
                    // Find the first fret on the 6th string
                    str := 0;
                    stringPosCount := 0;
                    fret := (Ord(note) - Ord(htE) + 12) mod 12;
                    absRootNote := Ord(STANDARD_GUITAR_TUNING[0].halfTone) + fret;

                    // Pattern and mode adjustment
                    index := scaleMode - 1;
                    while pattern > 1 do
                       begin
                          increment := fScale.degreeInterval[index];
                          nextIncrement := fScale.degreeInterval[index];
                          Inc(Index);
                          if index > fScale.count
                             then index := 0;
                          if increment > 1
                             then Dec(pattern);
                          fret := fret + increment;
                          absDegreeNote := absDegreeNote + increment;
                          degreeNote :=  absDegreeNote mod 12;
                       end;

                    // Maybe lower from an octave
                    if fret > 12
                       then Dec(fret, 12);
                    minFret := fret;
                    maxFret := fret;

                    // Compute the finger positions
                    minStringFret := fret;
                    stringJumps := 0;
                    while str < STRING_COUNT do
                       begin
                          // Check the extremes
                          if fret < minFret
                             then minFret := fret;
                          if fret > maxFret
                             then maxFret := fret;

                          // Add the position
                          position := TFretBoardPosition.Create;
                          fPatternPositions.Add(position);
                          position.fStr := str;
                          position.fFret := fret;
                          position.fHalfTone := THalfTone((absRootNote + absDegreeNote) mod 12);
                          position.fOctave := (absDegreeNote + degreeNote) div 12;
                          position.fQuality := TToneQuality((DIATONIC_TONE[degreeNote] - tonesFromRootNote + 7) mod 7);
                          position.fQualityAccidental := TNoteAccidental(Ord(naNatural) + DEGREE_ACCIDENTAL[degreeNote] - KEY_ACCIDENTAL[halfTonesFromRootNote, DIATONIC_TONE[degreeNote]]);

                          // Move to the next position
                          Inc(stringPosCount);
                          increment := fScale.degreeInterval[index];
                          Inc(index);
                          if index > fScale.count
                             then index := 0;
                          fret := fret + increment;
                          absDegreeNote := absDegreeNote + increment;
                          degreeNote := absDegreeNote mod 12;
                          if increment > 1
                             then // Move more than half a tone away from current fret
                                  Inc(stringJumps);
                          if (fret - minStringFret >= 5) // String positions must fit in 5 frets
                             or (not(extended) and (increment > 2)) // Allow only 1 tone jumps for condensed handshape
                             or ((stringJumps > 1) and not(extended)) // Allow only one jump per string for condensed handshape
                             or (not(extended) and (fret - minFret > 4)) // Allow up to 5 frets wide for condensed handshape
                             or ((extended) and (fret - minFret > 6)) // Allow up 7 frets wide for extended handshape
                             or ((extended) and (stringJumps > 0) and (stringPosCount > 2)) // Special case
                             or (fret >= invalidFret) // Invalid fret from precedent pass
                             then // Jump to next string
                                  begin
                                     // Go to next string
                                     stringIncrement := Ord(STANDARD_GUITAR_TUNING[str + 1].halfTone) + STANDARD_GUITAR_TUNING[str + 1].octave * 12
                                                        - (Ord(STANDARD_GUITAR_TUNING[str].halftone) + STANDARD_GUITAR_TUNING[str].octave * 12);
                                     fret := fret - stringIncrement;
                                     Inc(str);

                                     // Reset
                                     stringPosCount := 0;
                                     stringJumps := 0;
                                     minStringFret := fret;
                                  end;
                       end;

                    // Check if a second pass is needed
                    if (not(extended) and (maxFret - MinFret >= 5))
                       or (extended and (maxFret - MinFret >= 7))
                       then begin
                               invalidFret := maxFret;
                               Assert(fPatternPositions.OwnsObjects);
                               fPatternPositions.Clear;
                            end
                       else Break;
           end;
      end;

   // Maybe compute other positions
   if otherPositions <> nil
      then begin
              otherPositions.Clear;

              for str := 0 to 5 do
                 begin
                    // Move the number of frets backward that separate the open string from the scale root
                    fret := -(Ord(STANDARD_GUITAR_TUNING[str].halfTone) - Ord(note) + 12) mod 12;
                    index := scaleMode - 1;
                    degreeNote := halfTonesFromRootNote;

                    // Compute the positions on the string
                    while true do
                       begin
                          // Check if string is done
                          if fret > 19
                             then Break;

                          // Maybe add the position
                          if fret >= 0
                             then // The position is on the fingerboard, include it
                                  begin
                                     // Add the position
                                     position := TFretBoardPosition.Create;
                                     fOtherPositions.Add(position);
                                     position.fStr := str;
                                     position.fFret := fret;
                                     position.fQuality := TToneQuality((DIATONIC_TONE[degreeNote] - tonesFromRootNote + 7) mod 7);
                                     position.fQualityAccidental := TNoteAccidental(Ord(naNatural) + DEGREE_ACCIDENTAL[degreeNote] - KEY_ACCIDENTAL[halfTonesFromRootNote, DIATONIC_TONE[degreeNote]]);
                                  end;

                          // Move to the next scale degree
                          increment := fScale.degreeInterval[index];
                          Inc(index);
                          if index > fScale.count
                             then index := 0;
                          fret := fret + increment;
                          degreeNote := (degreeNote + increment) mod 12;
                       end;
                 end;
           end;
end;


{ TFretBoardPosition }

constructor TFretBoardPosition.Create;
begin
   fStr := 0;
   fFret := 0;
   fFinger := 0;
   fQuality := tqUnison;
   fQualityAccidental := naNone;
   fCharacterTone := ctNone;
end;

end.
