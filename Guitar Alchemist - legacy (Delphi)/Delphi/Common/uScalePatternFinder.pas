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
         property characterTone : TCharacterTone read fCharacterTone;
   end;

   TScalePatternFinder = class;
   TScalePatternChangeLink = class(TObject)
      private
         fSender : TScalePatternFinder;
         fOnChange : TNotifyEvent;
      public
         destructor Destroy; override;
         procedure Change; dynamic;
         property onChange : TNotifyEvent read fOnChange write fOnChange;
         property sender: TScalePatternFinder read fSender write fSender;
   end;

   TScalePatternFinder = class(TComponent)
      // Object core
      private
         fPatternPositions : TObjectList;
         fOtherPositions : TObjectList;
         fScale : TScale;
         fNote : THalfTone;
         fScaleName : string;
         fScaleMode : integer;
         fPattern : integer;
         fExtended : boolean;
         fOctave : boolean;
         fMinFret : integer;
         fMaxFret : integer;
         fPatternStartDegree : integer;

      // Changes notification
      private
         fClients : TList;
         fOnChange : TNotifyEvent;
         fChanged : boolean;
         fUpdateCount : Integer;         
      protected
         procedure BeginUpdate; dynamic;
         procedure EndUpdate; dynamic;
         procedure Change; dynamic;
      public
         procedure RegisterChanges(value : TScalePatternChangeLink);
         procedure UnRegisterChanges(value : TScalePatternChangeLink);
      published
         property OnChange : TNotifyEvent read fOnChange write fOnChange;

      // Positions computing
      public
         procedure Compute(note : THalfTone; scaleName : string; scaleMode : integer;
                           pattern : integer; extended : boolean;
                           var octave : boolean);
         function FindPatternForPosition(str : TString; fret : TFret) : boolean;

      // Constructor, destructor and properties
      public
         constructor Create(AOwner: TComponent); override;
         destructor Destroy; override;
         function IsPatternPosition(str : TString; fret : TFret) : boolean;
         function IsOtherPosition(str : TString; fret : TFret) : boolean;
         function PatternPositionIndex(str : TString; fret : TFret) : integer;
         property patternPositions : TObjectList read fPatternPositions;
         property otherPositions : TObjectList read fOtherPositions;
         property scale : TScale read fScale;
         property note : THalfTone read fNote;
         property scaleName : string read fScaleName;
         property scaleMode : integer read fScaleMode;
         property pattern : integer read fPattern;
         property extended : boolean read fExtended;
         property octave : boolean read fOctave;
         property minFret : integer read fMinFret;
         property maxFret : integer read fMaxFret;
         property patternStartDegree : integer read fPatternStartDegree;
   end;

implementation

const
   STRING_COUNT = 6;
   MAX_STRING_EXTENT = 5;


{ TScalePatternFinder }

constructor TScalePatternFinder.Create(AOwner: TComponent);
begin
   inherited Create(AOwner);
   fClients := TList.Create;
   fScale := TScale.Create;
   fPatternPositions := TObjectList.Create;
   fOtherPositions := TObjectList.Create;
   fClients := TList.Create;
   fScaleName := 'Major';
   fScaleMode := 1;
   fMinFret := 0;
   fMaxFret := 0;
   fPatternStartDegree := 0;
end;

destructor TScalePatternFinder.Destroy;
begin
   while fClients.Count > 0 do
      UnRegisterChanges(TScalePatternChangeLink(fClients.last));
   fClients.Free;
   fClients := nil;
   fScale.Free;
   fPatternPositions.Free;
   fOtherPositions.Free;
   inherited;
end;

procedure TScalePatternFinder.Compute(note : THalfTone; scaleName : string; scaleMode : integer;
                                      pattern : integer; extended : boolean;
                                      var octave : boolean);
var
   pass : integer;
   index : integer;
   increment : integer;
   nextIncrement : integer;
   str : TString;
   fret : integer;
   p : integer;
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

   minorScale : boolean;

   modesAllowed : boolean;
begin
   // Inits
   fNote := note;
   fScaleName := scaleName;
   fScaleMode := scaleMode;
   fPattern := pattern;
   fExtended := extended;
   fOctave := octave;
   minorScale := false;
   fPatternPositions := patternPositions;
   fOtherPositions := otherPositions;
   globalScaleRepository.GetScale(fScale, scaleName, modesAllowed);
   invalidFret := MaxInt;

   halfTonesFromRootNote := 0;
   for index := 0 to scaleMode - 2 do
      halfTonesFromRootNote := (halfTonesFromRootNote + fScale.degreeInterval[index]) mod 12;
   tonesFromRootNote := DIATONIC_TONE[halfTonesFromRootNote];

   // Compute pattern positions
   for pass := 0 to 1 do
      begin
         patternPositions.Clear;
         absDegreeNote := halfTonesFromRootNote;

         // Find the first fret on the 6th string
         str := 0;
         stringPosCount := 0;
         fret := (Ord(note) - Ord(htE) + 12) mod 12;

         // Mode adjustment
         index := scaleMode - 1;

         // Pattern adjustment
         fPatternStartDegree := 0;
         if pattern > 1
            then // Pattern adjustment
                 begin
                    p := pattern;
                    while p > 1 do
                       begin
                          increment := fScale.degreeInterval[index];
                          nextIncrement := fScale.degreeInterval[index + 1];
                          Inc(Index);
                          Inc(fPatternStartDegree);
                          if index > fScale.count
                             then index := 0;
                          if (increment >= 2) // 2 or more frets
                             or ((increment = 1) and (nextIncrement >= 3)) // 1 fret interval, but next interval is 3 frets or more
                             then Dec(p);
                          fret := fret + increment;
                          absDegreeNote := absDegreeNote + increment;
                       end;
                 end;
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
               position.fQuality := TToneQuality((DIATONIC_TONE[absDegreeNote mod 12] - tonesFromRootNote + 7) mod 7);
               position.fQualityAccidental := TNoteAccidental(Ord(naNatural) + DEGREE_ACCIDENTAL[absDegreeNote mod 12]
                                              - KEY_ACCIDENTAL[halfTonesFromRootNote, DIATONIC_TONE[absDegreeNote mod 12]]);
               // Detect minor key
               if (position.fQuality = tqThird) and (position.fQualityAccidental = naFlat)
                  then minorScale := true;

               // Detect primary character tones
               if minorScale
                  then begin
                          if ((position.fQuality in [tqSixth,tqSeventh])
                             and (position.fQualityAccidental <> naFlat))
                             or ((position.fQualityAccidental <> naNatural)
                             and (position.fQuality <> tqThird))
                          then // Primary character tone if not flatted 6th or 7th in minor scale
                               position.fCharacterTone := ctPrimary
                       end
               else if position.fQualityAccidental <> naNatural
                       then position.fCharacterTone := ctPrimary;

               // Detect secondary character tones
               if (position.fCharacterTone = ctNone)
                  and ((position.fQuality = tqThird) or (position.fQuality = tqSeventh))
                  then position.fCharacterTone := ctSecondary;

               // Move to the next position
               Inc(stringPosCount);
               increment := fScale.degreeInterval[index];
               Inc(index);
               if index > fScale.count
                  then index := 0;
               fret := fret + increment;
               absDegreeNote := absDegreeNote + increment;
               if increment > 1
                  then // Move more than half a tone away from current fret
                       Inc(stringJumps);
               if (fret - minStringFret >= 5) // String positions must fit in 5 frets
                  or (not(extended) and (increment > 3)) // Allow only 1.5 tones jumps for condensed handshape
                  or ((stringJumps > 1) and not(extended)) // Allow only one jump per string for condensed handshape
                  or (not(extended) and (fret - minFret > 4)) // Allow up to 5 frets wide for condensed handshape
                  or ((extended) and (fret - minFret > 6)) // Allow up 7 frets wide for extended handshape
                  or ((extended) and (stringJumps > 0) and (stringPosCount > 2)) // Special case
                  or (fret >= invalidFret) // Invalid fret from precedent pass
                  then // Jump to next string
                       begin
                          // Go to next string
                          if str = 5
                             then Break;
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

   // Maybe compute other positions
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

   // Compute the extremums
   fMinFret := FRET_COUNT;
   fMaxFret := 0;
   for index := 0 to patternPositions.count - 1 do
      begin
         Assert(patternPositions[index] is TFretBoardPosition);
         position := TFretBoardPosition(patternPositions[index]);
         if position.fret > fMaxFret
            then fMaxFret := position.fret;
         if position.fret < fMinFret
            then fMinFret := position.fret;
      end;

   // Octave correction
   if fMaxFret > 19
      then begin
              for index := 0 to patternPositions.count - 1 do
                 begin
                    Assert(patternPositions[index] is TFretBoardPosition);
                    position := TFretBoardPosition(patternPositions[index]);
                    Dec(position.fFret, 12);
                 end;
              Dec(fMinFret, 12);
              Dec(fMaxFret, 12);
           end;
   if (fMinFret >= 12) and not octave
      then begin
              for index := 0 to patternPositions.count - 1 do
                 begin
                    Assert(patternPositions[index] is TFretBoardPosition);
                    position := TFretBoardPosition(patternPositions[index]);
                    Dec(position.fFret, 12);
                 end;
              Dec(fMinFret, 12);
              Dec(fMaxFret, 12);
            end
   else if ((fMaxFret <= 19 - 12) and octave) or (fMinFret < 0)
      then begin
              for index := 0 to patternPositions.count - 1 do
                 begin
                    Assert(patternPositions[index] is TFretBoardPosition);
                    position := TFretBoardPosition(patternPositions[index]);
                    Inc(position.fFret, 12);
                 end;
              Inc(fMinFret, 12);
              Inc(fMaxFret, 12);
           end;
   octave := fMinFret >= 12;

   // Notify change to linked fingerboard
   Change
end;

function TScalePatternFinder.IsPatternPosition(str : TString; fret : TFret) : boolean;
var
   index : integer;
   position : TFretBoardPosition;
begin
   result := false;
   for index := 0 to fPatternPositions.count - 1 do
      begin
         Assert(fPatternPositions[index] is TFretBoardPosition);
         position := TFretBoardPosition(fPatternPositions[index]);
         result := (position.str = str) and (position.fret = fret);
         if result
            then Break;
      end;
end;

function TScalePatternFinder.IsOtherPosition(str : TString; fret : TFret) : boolean;
var
   index : integer;
   position : TFretBoardPosition;
begin
   result := false;
   for index := 0 to fOtherPositions.count - 1 do
      begin
         Assert(fOtherPositions[index] is TFretBoardPosition);
         position := TFretBoardPosition(fOtherPositions[index]);
         if (position.str = str) and (position.fret = fret)
            then begin
                    result := true;
                    Break;
                 end;   
      end;
end;

function TScalePatternFinder.PatternPositionIndex(str : TString; fret : TFret) : integer;
var
   index : integer;
   position : TFretBoardPosition;
begin
   result := -1;
   for index := 0 to fPatternPositions.count - 1 do
      begin
         Assert(fPatternPositions[index] is TFretBoardPosition);
         position := TFretBoardPosition(fPatternPositions[index]);
         if (position.str = str) and (position.fret = fret)
            then begin
                    result := index;
                    Break;
                 end;
      end;
end;

function TScalePatternFinder.FindPatternForPosition(str : TString; fret : TFret) : boolean;
var
   index : integer;
   patternIndex : integer;
   octaveIndex : integer;
   suggestedOctave : boolean;
   octave : boolean;
begin
   // Init
   patternIndex := fPattern;
   result := false;
   suggestedOctave := fOctave;
   if fMaxFret >= 12
      then suggestedOctave := true;

   // Explore both octaves if needed
   for octaveIndex := 0 to 1 do
      begin
         if fret <= fMinFret
            then for index := 1 to 5 do
                    begin
                       Dec(patternIndex);
                       if patternIndex < 1
                          then patternIndex := 5;
                       octave := suggestedOctave;
                       Compute(fNote, fScaleName, fScaleMode, patternIndex, fExtended, octave);
                       if IsPatternPosition(str, fret) and not (octave and not suggestedOctave)
                          then begin
                                  result := true;
                                  Break;
                               end;
                    end
            else for index := 1 to 5 do
                    begin
                       Inc(patternIndex);
                       if patternIndex > 5
                          then patternIndex := 1;
                       octave := suggestedOctave;
                       Compute(fNote, fScaleName, fScaleMode, patternIndex, fExtended, octave);
                       if IsPatternPosition(str, fret)
                          then begin
                                  result := true;
                                  Break;
                               end;
                    end;

         if fMaxFret > 19
            then result := false;

         if result
            then Break
            else suggestedOctave := not suggestedOctave;
      end;

   // Maybe set the result
   if result
      then begin
              fOctave := octave;
              fPattern := patternIndex;
           end;
end;

procedure TScalePatternFinder.Change;
var
   i : Integer;
begin
   fChanged := True;
   if fUpdateCount > 0
      then Exit;
   if fClients <> nil
      then for i := 0 to fClients.Count - 1 do
              TScalePatternChangeLink(fClients[I]).Change;
   if Assigned(fOnChange)
      then fOnChange(Self);
end;

procedure TScalePatternFinder.RegisterChanges(value : TScalePatternChangeLink);
begin
   value.sender := self;
   if fClients <> nil
      then fClients.Add(value);
end;

procedure TScalePatternFinder.UnRegisterChanges(value : TScalePatternChangeLink);
var
   i : integer;
begin
   if fClients <> nil
      then for i := 0 to fClients.count - 1 do
              if fClients[i] = value
                 then begin
                         value.sender := nil;
                         fClients.Delete(i);
                         Break;
                      end;
end;

procedure TScalePatternFinder.BeginUpdate;
begin
   Inc(fUpdateCount);
end;

procedure TScalePatternFinder.EndUpdate;
begin
   if fUpdateCount > 0
      then Dec(fUpdateCount);
   if fChanged
      then begin
              fChanged := false;
              Change;
           end;
end;

{ TFretBoardPosition }

constructor TFretBoardPosition.Create;
begin
   inherited Create;
   fStr := 0;
   fFret := 0;
   fFinger := 0;
   fQuality := tqUnison;
   fQualityAccidental := naNone;
   fCharacterTone := ctNone;
end;

{ TScalePatternChangeLink }

destructor TScalePatternChangeLink.Destroy;
begin
  if sender <> nil
     then sender.UnRegisterChanges(self);
  inherited Destroy;
end;

procedure TScalePatternChangeLink.Change;
begin
  if Assigned(onChange)
     then onChange(Sender);
end;


end.
