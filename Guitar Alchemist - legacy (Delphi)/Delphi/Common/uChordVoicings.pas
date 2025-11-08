unit uChordVoicings;

interface

uses
   StdCtrls
   ,Classes
   ,Contnrs
   ,Controls
   ,uMusicClasses
   ;

const
   EXTENT = 5;

type
   TChordVoicingDifficulty = (cvdEasy, cvdMedium, cvdHard, cvdVeryHard, cvdMortal, cvdImpossible);
   TChordVoicingBrightness = (cvbVeryDark, cvbDark, cvbMediumDark, cvbBalanced, cvbClear, cvbMediumBright, cvbBright, cvbVeryBright);
   TChordVoicingContrast = (cvcTiny, cvcSmall, cvcMedium, cvcBig, cvcVeryBig);
   TChordVoicingFilter = (cvfAny, cvfRoot, cvfFirstInversion, cvfSecondInversion, cvfThirdInversion,
                          cvfRootAsHighestNote, cvf2ndNoteAsHighestNote, cvf3ndNoteAsHighestNote, cvf4ndNoteAsHighestNote,
                          cvf5ndNoteAsHighestNote, cvf6ndNoteAsHighestNote, cvf7ndNoteAsHighestNote,
                          cvfDrop2);
const
   VOICING_DIFFICULTY : array[TChordVoicingDifficulty] of string =
      ('Easy', 'Medium', 'Hard', 'Very hard', 'Mortal', 'Impossible');
   VOICING_BRIGHTNESS_NAMES : array[TChordVoicingBrightness] of string =
      ('Very dark', 'Dark', 'Med dark', 'Balanced', 'Low bright', 'Med bright', 'Bright', 'Very bright');
   VOICING_CONTRAST_NAMES : array[TChordVoicingContrast] of string =
      ('Tiny', 'Small', 'Medium', 'Big', 'Very big');


type

   // Base voicing classes
   TChordVoicingCollection = class;

   TVoicingStringList = class(TStringList)
      protected
         function CompareStrings(const S1, S2: string): Integer; override;
   end;

   TChordVoicing = class
      private
         fCollection : TChordVoicingCollection;
         fPositions : TVoicingPositions;
         fFormula : TChordVoicingFormula;
         fInversion : integer;
         fDrop2 : boolean;
         fInversionNote : THalfTone;
         fMinFret : integer;
         fMaxFret : integer;
         fRootPosition : RStringPosition;
         fHighestNote : THalfTone;
         fHighestNoteQuality : THalfToneQuality;
         fValidChord : boolean;
         fFingerCount : integer;
         fGroupedStrings : boolean;
         fOpenStrings : integer;
         fMutedStrings : integer;
         fStringCount : integer;
         fPreciseBrightness : integer;
         fBrightness : TChordVoicingBrightness;
         fPreciseContrast : integer;
         fContrast : TChordVoicingContrast;
         fFirstFingerBarreStartingStr : integer;
         fFirstFingerBarreExtent : integer;
         fBigBarre : boolean;
         fVoicingId : string;
         fScore : integer;
         fDifficulty : TChordVoicingDifficulty;
         fNatural : integer;
{$IFDEF DebugVoicingsComputing}
         fEliminated : boolean;
{$ENDIF}
         procedure ComputeFirstFingerBarre;
      public
         constructor Create(positions : TVoicingPositions; collection : TChordVoicingCollection);
         property collection : TChordVoicingCollection read fCollection;
         property positions : TVoicingPositions read fPositions;
         property formula : TChordVoicingFormula read fFormula;
         property inversion : integer read fInversion;
         property inversionNote : THalfTone read fInversionNote;
         property drop2 : boolean read fDrop2;
         property minFret : integer read fMinFret;
         property maxFret : integer read fMaxFret;
         property rootPosition : RStringPosition read fRootPosition;
         property validChord : boolean read fValidChord;
         property groupedStrings : boolean read fGroupedStrings;
         property openStrings : integer read fOpenStrings;
         property mutedStrings : integer read fMutedStrings;
         property stringCount : integer read fStringCount;
         property brightness : TChordVoicingbrightness read fBrightness;
         property preciseBrightness : integer read fPreciseBrightness;
         property contrast : TChordVoicingContrast read fContrast;
         property preciseContrast : integer read fPreciseContrast;
         property fingerCount : integer read fFingerCount write fFingerCount;
         property firstFingerBarreExtent : integer read fFirstFingerBarreExtent;
         property firstFingerBarreStartingStr : integer read fFirstFingerBarreStartingStr;         
         property voicingId : string read fVoicingId;
         property score : integer read fScore;
         property difficulty : TChordVoicingDifficulty read fDifficulty;
         property natural : integer read fNatural;
{$IFDEF DebugVoicingsComputing}
         property eliminated : boolean read fEliminated;
{$ENDIF}
   end;

   TChordVoicingCollection = class
      private
         fRoot : THalfTone;
         fHalfTones : THalfTones;
         fCandidateQualities : array of THalfToneQuality;
         fChordVoicings : TVoicingStringList;
         fMutedVoicingsToCheck : TStringList;
         fSortOrder : TStringList;
         fQualities : THalfToneQualities;
         fMandatoryQualities : THalfToneQualities;
         fOptionalQualities : THalfToneQualities;
         fMaxDifficulty : TChordVoicingDifficulty;
         fMaxFret : integer;
         fNoMutedStrings : boolean;
         fNoOpenStrings : boolean;
         fNoBarres : boolean;
         fVoicingFilter : TChordVoicingFilter;
         procedure SetRoot(value : THalfTone);
         procedure SetQualities(value : THalfToneQualities);
         procedure SetMaxFret(value : integer);
         procedure SetMaxDifficulty(value : TChordVoicingDifficulty);
         procedure SetNoMutedStrings(value : boolean);
         procedure SetNoOpenStrings(value : boolean);
         procedure SetNoBarres(value : boolean);
         procedure SetVoicingFilter(value : TChordVoicingFilter);
         function GetCount : integer;
         function GetItem(index : integer) : TChordVoicing;
      protected
         procedure AddVoicings(startingFret, extent : integer);
         function AddVoicing(positions : TVoicingPositions) : integer;
         function CompareVoicings(voicing1, voicing2 : TChordVoicing) : integer;
      public
         constructor Create;
         destructor Destroy; override;
         procedure ComputeVoicings;
         procedure SortVoicings;
         procedure Clear;
         function GetInversion(quality : THalfToneQuality) : integer;
         function IsDrop2Voicing(chordVoicingFormula : TChordVoicingFormula) : boolean;
         property root : THalfTone read fRoot write SetRoot;
         property qualities : THalfToneQualities read fQualities write SetQualities;
         property mandatoryQualities : THalfToneQualities read fMandatoryQualities;
         property optionalQualities : THalfToneQualities read fOptionalQualities;
         property maxDifficulty : TChordVoicingDifficulty read fMaxDifficulty write SetMaxDifficulty;
         property maxFret : integer read fMaxFret write SetMaxFret;
         property noMutedStrings : boolean read fNoMutedStrings write SetNoMutedStrings;
         property noOpenStrings : boolean read fNoOpenStrings write SetNoOpenStrings;
         property noBarres : boolean read fNoBarres write SetNoBarres;
         property voicingFilter : TChordVoicingFilter read fVoicingFilter write SetVoicingFilter;
         property count : integer read GetCount;
         property sortOrder : TStringList read fSortOrder;
         property items[index : integer] : TChordVoicing read GetItem;
   end;


   // Voicing sorting classes
   TChordVoicingProperty = class
      private
         fCollection : TChordVoicingCollection;
         fCanHide : boolean;
         fMovable : boolean;
         fSorted : boolean;
         fReverseOrder : boolean;
      protected
         class function GetCaption : string; virtual; abstract;
         class function GetMovable : boolean; virtual;
         class function GetMinWidth : integer; virtual;
         class function GetMaxWidth : integer; virtual;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; virtual; abstract;
      public
         constructor Create(collection : TChordVoicingCollection); virtual;
         property caption : string read GetCaption;
         property canHide : boolean read fCanHide write fCanHide;
         property movable : boolean read GetMovable;
         property reverseOrder : boolean read fReverseOrder write fReverseOrder;
         property sorted : boolean read fSorted write fSorted;
         property minWidth : integer read GetMinWidth;
         property maxWidth : integer read GetMaxWidth;
   end;
   TChordVoicingPropertyClass = class of TChordVoicingProperty;

   TChordVoicing_Voicing = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function GetMovable : boolean; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
      public
         constructor Create(collection : TChordVoicingCollection); override;
   end;

   TChordVoicing_Difficulty = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
   end;

   TChordVoicing_Fret = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
   end;

   TChordVoicing_Inversion = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
   end;

   TChordVoicing_MutedStrings = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
   end;

   TChordVoicing_OpenString = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
   end;

   TChordVoicing_Strings = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
   end;

   TChordVoicing_Fingers = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
   end;

   TChordVoicing_Brightness = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
         class function GetMinWidth : integer; override;
         class function GetMaxWidth : integer; override;
   end;

   TChordVoicing_Contrast = class(TChordVoicingProperty)
      protected
         class function GetCaption : string; override;
         class function Compare(voicing1, voicing2 : TChordVoicing) : integer; override;
   end;


const
   CHORD_VOICING_PROPERTY_CLASSES : array[0..9] of TChordVoicingPropertyClass =
   (
       TChordVoicing_Voicing
       ,TChordVoicing_Fret
       ,TChordVoicing_Inversion
       ,TChordVoicing_Difficulty
       ,TChordVoicing_MutedStrings
       ,TChordVoicing_OpenString
       ,TChordVoicing_Strings
       ,TChordVoicing_Fingers
       ,TChordVoicing_Brightness
       ,TChordVoicing_Contrast
   );


implementation

uses
   uVoicingFingersFinder
   ,Math
   ,SysUtils
   ;

const
   MIN_MEAN = 6.5;
   MAX_MEAN = 43.5;

   MIN_STDDEV = 0;
   MAX_STDDEV = 22;

function CompareChordVoicings(list : TStringList; index1, index2 : integer) : integer;
var
   voicing1, voicing2 : TChordVoicing;
   collection : TChordVoicingCollection;
begin
   // Retrieve the voicings to be compared
   Assert(list.objects[index1] is TChordVoicing);
   Assert(list.objects[index2] is TChordVoicing);
   voicing1 := TChordVoicing(list.objects[index1]);
   voicing2 := TChordVoicing(list.objects[index2]);
   collection := voicing1.collection;

   // Perform the comparison
   result := collection.CompareVoicings(voicing1, voicing2);
end;


{ TChordVoicingCollection }

constructor TChordVoicingCollection.Create;
var
   index : integer;
   chordVoicingPropertyClass : TChordVoicingPropertyClass;
   chordVoicingProperty : TChordVoicingProperty;
begin
   fChordVoicings := TVoicingStringList.Create;
   fMutedVoicingsToCheck := TStringList.Create;   
   fSortOrder := TStringList.Create;
   fMaxDifficulty := cvdHard;
   fMaxFret := 15;
   fNoMutedStrings := false;
   fNoOpenStrings := false;
   fNoBarres := false;
   fVoicingFilter := cvfAny;

   // Init with C major
   fRoot := htC;
   fQualities := [htqUnison, htqMajorThird, htqPerfectFifth];
   fOptionalQualities := [];
   fMandatoryQualities := [htqUnison, htqMajorThird, htqPerfectFifth];

   // Init sort order
   for index := 0 to Length(CHORD_VOICING_PROPERTY_CLASSES) - 1 do
      begin
         chordVoicingPropertyClass := CHORD_VOICING_PROPERTY_CLASSES[index];
         chordVoicingProperty := chordVoicingPropertyClass.Create(self);
         fSortOrder.AddObject(chordVoicingProperty.caption, chordVoicingProperty);
      end;
end;

destructor TChordVoicingCollection.Destroy;
var
   index : integer;
begin
   Clear;
   fChordVoicings.Free;
   fMutedVoicingsToCheck.Free;
   for index := 0 to fSortOrder.count - 1 do
      begin
         Assert(fSortOrder.Objects[index] <> nil);
         fSortOrder.Objects[index].Free
      end;
   fSortOrder.Free;
   inherited;
end;

procedure TChordVoicingCollection.Clear;
var
   index : integer;
   voicing : TChordVoicing;
begin
   for index := 0 to fChordVoicings.count - 1 do
      begin
         Assert(fChordVoicings.Objects[index] is TChordVoicing);
         voicing := TChordVoicing(fChordVoicings.Objects[index]);
         voicing.Free;
      end;
   fChordVoicings.Clear;
   fMutedVoicingsToCheck.Clear;   
end;

function TChordVoicingCollection.GetInversion(quality : THalfToneQuality) : integer;
var
   index : integer;
begin
   result := -1;
   for index := 0 to High(fCandidateQualities) do
      begin
         if SameQualityOctaveQuality(fCandidateQualities[index], quality)
            then begin
                    result := index;
                    Break;
                 end;
      end;
   Assert(result <> -1);
end;

function TChordVoicingCollection.IsDrop2Voicing(chordVoicingFormula : TChordVoicingFormula) : boolean;
var
   str : integer;

   function IsExpectedHalfTone(index : integer) : boolean;
   var
      stringAbsHalftone : integer;
   begin
      repeat
         Inc(str);
         stringAbsHalftone := Ord(chordVoicingFormula[str]) mod 12;
      until (stringAbsHalftone <> -1);
      result := Ord(stringAbsHalftone) mod 12 = (Ord(fRoot) + Ord(fCandidateQualities[index])) mod 12;
   end;

begin
   // Inits
   result := false;
   str := -1;

   // The chord template must have 4 notes exactly
   if (Length(fCandidateQualities) <> 4)
      then Exit;

   if IsExpectedHalfTone(2) and IsExpectedHalfTone(0) and IsExpectedHalfTone(1) and IsExpectedHalfTone(3)
      then result := true;
end;

procedure TChordVoicingCollection.ComputeVoicings;
var
   index : integer;
   quality : THalfToneQuality;
   startingFret : integer;
   voicing : TChordVoicing;

   procedure MaybeDeleteMutedVoicing(voicingTemplate : string);
   var
      index : integer;
      voicingId : string;
      existingVoicing : TChordVoicing;
      candidateMutedVoicing : TChordVoicing;

      function AnyMutedPositionsInsideBarre : boolean;
      var
         str : integer;
      begin
         result := false;
         for str := candidateMutedVoicing.firstFingerBarreStartingStr - candidateMutedVoicing.firstFingerBarreExtent + 1to
                    candidateMutedVoicing.firstFingerBarreStartingStr do
            if (candidateMutedVoicing.fFormula[str] = -1) and (existingVoicing.fFormula[str] <> -1)
               then // Muted position inside the barre while
                    // it's not muted in the existing voicing
                    begin
                       result := true;
                       Break;
                    end;
      end;

   begin
      if fChordVoicings.Find(voicingTemplate, index)
         then begin
                 // Replace ?? back to FF
                 voicingId := StringReplace(voicingTemplate, '??', 'FF', [rfReplaceAll]);
                 Assert(fChordVoicings.Objects[index] is TChordVoicing);
                 existingVoicing := TChordVoicing(fChordVoicings.Objects[index]);

                 // Delete this muted voicing
                 fChordVoicings.Find(voicingId, index);
                 if index <> -1
                    then // The voicing is in the list, just as expected
                         begin
                            Assert(fChordVoicings.Objects[index] is TChordVoicing);
                            candidateMutedVoicing := TChordVoicing(fChordVoicings.Objects[index]);
                            if (candidateMutedVoicing.fFirstFingerBarreExtent > 3) // Delete only barre voicing
                               and (candidateMutedVoicing.fInversionNote = existingVoicing.fInversionNote) // The root note must be the same
                               and AnyMutedPositionsInsideBarre // There's a muted string inside the barre
                               then
{$IFDEF DebugVoicingsComputing}
                                    candidateMutedVoicing.fEliminated := true;
{$ELSE}
                                    fChordVoicings.Delete(index);
{$ENDIF}
                         end
                    else // Unexpected situation
                         Assert(false, 'The voicing should have been found in the list!');
              end;
   end;

   function FilterVoicing(voicing : TChordVoicing) : boolean;
   var
      highestNoteIndex : integer;
   begin
           if fVoicingFilter = cvfAny
              then result := true
      else if fVoicingFilter in [cvfRoot..cvfThirdInversion]
              then result := voicing.fInversion = Ord(fVoicingFilter) - Ord(cvfRoot)
      else if fVoicingFilter in [cvfRootAsHighestNote..cvf7ndNoteAsHighestNote]
              then begin
                      highestNoteIndex := Ord(fVoicingFilter) - Ord(cvfRootAsHighestNote);
                      if highestNoteIndex >= Length(fCandidateQualities)
                         then result := false
                         else result := (Ord(fCandidateQualities[highestNoteIndex]) mod 12) = Ord(voicing.fHighestNoteQuality) mod 12;
                   end
//  todo: cvfDrop2
      else
                   begin
                       Assert(false, 'Voicing filter not handled');
                       result := true;
                   end;

   end;

begin
   // Inits
   Clear;
   fChordVoicings.sorted := true;
   fChordVoicings.duplicates := dupIgnore;
   fHalfTones := GetHalfTonesFromQualities(fQualities, fRoot);

   // Compute mandatory and optional qualities
   fMandatoryQualities := fQualities;
   fOptionalQualities := [];
   if (htqPerfectFifth in fQualities)
      and ((htqMinorSeventh in fQualities) or (htqMajorSeventh in fQualities))
      then // 5 is optional if b7 or 7 chord
           begin
              Include(fOptionalQualities, htqPerfectFifth);
              Exclude(fMandatoryQualities, htqPerfectFifth);
           end;
   if htqPerfectEleventh in fQualities
      then // 9 is optional if 11 chord
           begin
              Include(fOptionalQualities, htqMajorNinth);
              Exclude(fMandatoryQualities, htqMajorNinth);
           end;
   if htqMajorThirteenth in fQualities
      then // 11 is optional if 13 chord
           begin
              Include(fOptionalQualities, htqPerfectEleventh);
              Exclude(fMandatoryQualities, htqPerfectEleventh);
           end;

   // Limit qualities to first octave
   LimitQualitiesToFirstOctave(fMandatoryQualities);
   LimitQualitiesToFirstOctave(fOptionalQualities);

   // Compute inversion qualities
   SetLength(fCandidateQualities, 0);
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         quality := THalfToneQuality(index);
         if quality in fQualities
            then begin
                    SetLength(fCandidateQualities, Length(fCandidateQualities) + 1);
                    fCandidateQualities[High(fCandidateQualities)] := quality;
                 end;
      end;

   // Compute voicings for each fret
   for startingFret := 1 to FRET_COUNT - 1 do
      AddVoicings(startingFret, EXTENT);

   // Maybe eliminate voicings which contains muted positions made obsolete by other positions
   for index := 0 to fMutedVoicingsToCheck.count - 1 do
      MaybeDeleteMutedVoicing(fMutedVoicingsToCheck[index]);

   // Eliminate all unwanted voicings (Difficulty, max fret, inversions, barre
   index := 0;
   while index < fChordVoicings.count do
      begin
         Assert(fChordVoicings.objects[index] is TChordVoicing);
         voicing := TChordVoicing(fChordVoicings.objects[index]);
         if (voicing.fingerCount > 4) // Too many fingers !
            or (voicing.difficulty > fMaxDifficulty) // Difficulty limit
            or (voicing.maxFret > fMaxFret)          // Fret limit
            or not FilterVoicing(voicing)                // Voicing filter
            or (fNoBarres and (voicing.fFirstFingerBarreExtent > 2))
            then // Don't include the voicing in the list and free it immediately
                 begin
                    voicing.Free;
                    fChordVoicings.Delete(index);
                 end
            else Inc(index);
      end;

   // Sort voicings
   SortVoicings;
end;

procedure TChordVoicingCollection.SortVoicings;
begin
   if fChordVoicings.count > 0
      then begin
              fChordVoicings.sorted := false;
              fChordVoicings.CustomSort(CompareChordVoicings);
           end;
end;

procedure TChordVoicingCollection.AddVoicings(startingFret, extent : integer);
var
   str, fret, index : integer;
   chordCandidatePositions : TChordPositionArray;
   stringCandidatePosition : RStringPosition;
   stringCandidatePositions : TStringPositionArray;
   candidatePositionCount : array of integer;
   voicingFormula : array of integer;
   finished : boolean;
   voicingQualities : THalfToneQualities;
   voicingPositions : TVoicingPositions;
   rootGenerated : boolean;
   outsideFingerBoard : boolean;
   voicingId : string;
   voicingIndex : integer;   

   procedure MutedStringCandidatePosition;
   const
      MUTED_STRING_POSITION : RStringPosition =
         (fret: -1; halfTone: (halfTone : htC; octave : 0); quality : htqUnison; finger : 0);
   begin
      if not fNoMutedStrings
         then // Add to string candidate positions
              begin
                 SetLength(stringCandidatePositions, Length(stringCandidatePositions) + 1);
                 stringCandidatePositions[High(stringCandidatePositions)] := MUTED_STRING_POSITION;
              end;
   end;

   function ScanCandidatePosition(str, fret : integer) : boolean;
   var
      stringCandidatePosition : RStringPosition;
      halfTone : RHalfTone;
      quality : THalfToneQuality;
   begin
      halfTone := PositionHalfTone(str, fret);
      quality := GetHalfToneQuality(halfTone.halfTone, fRoot);
      result := (halfTone.halfTone in fHalfTones) // Must be in halftones
                 and not((fret = 0) and fNoOpenStrings); // Cannot be fret 0 if no open strings allowed
      if result
         then // The position generates a valid note
              begin
                 // Init string candidate position
                 stringCandidatePosition.fret := fret;
                 stringCandidatePosition.halfTone := halfTone;
                 stringCandidatePosition.quality := quality;
                 stringCandidatePosition.finger := 0;

                 // Add to string candidate positions
                 SetLength(stringCandidatePositions, Length(stringCandidatePositions) + 1);
                 stringCandidatePositions[High(stringCandidatePositions)] := stringCandidatePosition;

                 // Maybe set rootGenerated
                 if quality = htqUnison
                    then rootGenerated := true;
              end;
   end;

begin
   // Init
   rootGenerated := false; // Once the root has been generated, we don't need to mute a string if it can be open

   ////////////////////////////////////
   // Compute chord candidate positions
   SetLength(chordCandidatePositions, 0);
   SetLength(candidatePositionCount, 6);
   SetLength(voicingFormula, 6);

   try
      for str := 0 to 5 do // For each string
        begin
           // Init string
           SetLength(stringCandidatePositions, 0);

           // Scan fret 0
           // Force including muted string position if the root has not been generated yet
           // Don't include muted string if open string can be played
           if (not ScanCandidatePosition(str, 0)) or (not rootGenerated)
              then MutedStringCandidatePosition;

           // Scan each fret from startingFret to extent frets further
           for fret := startingFret to startingFret + extent - 1 do
              ScanCandidatePosition(str, fret);

           SetLength(chordCandidatePositions, Length(chordCandidatePositions) + 1);
           chordCandidatePositions[High(chordCandidatePositions)] := stringCandidatePositions;
        end;
   finally
      SetLength(stringCandidatePositions, 0);
   end;

   ////////////////////////////////////////////////
   // Iterate the tree of chord candidate positions

   // Compute the number of candidate positions on each string
   for str := 0 to 5 do
      begin
         voicingFormula[str] := 0;
         candidatePositionCount[str] := High(chordCandidatePositions[str]) + 1;
      end;

   // Iterate
   finished := false;
   while not finished do
      begin
         // Compute voicing qualities and compile positions
         voicingQualities := [];
         outsideFingerBoard := false;
         voicingId := '';
         for str := 0 to 5 do
            begin
               index := voicingFormula[str];
               stringCandidatePositions := chordCandidatePositions[str];
               if Length(stringCandidatePositions) > index
                  then begin
                          stringCandidatePosition := stringCandidatePositions[index];
                          fret := stringCandidatePosition.fret;
                          if fret > FRET_COUNT
                             then begin
                                     outsideFingerBoard := true;
                                     Break;
                                  end;
                          voicingPositions[str] := stringCandidatePosition;
                          if fret = -1
                             then voicingId := voicingId + 'FF'
                             else voicingId := voicingId + IntToHex(fret, 2);
                          if fret <> -1
                            then // Include the quality generated by the position
                                 // if it's not a muted string
                                 Include(voicingQualities, stringCandidatePosition.quality);
                       end;
            end;

         // Maybe include this voicing  and it is doable
         if (not outsideFingerBoard)                             // Not outside the fingerboard
            and (voicingQualities >= fMandatoryQualities)        // includes mandatory qualities
            and not fChordVoicings.Find(voicingId, voicingIndex) // Does not already exists
            then AddVoicing(voicingPositions);

         // Next voicing
         for str := 0 to 5 do
            begin
               if voicingFormula[str] < candidatePositionCount[str] - 1
                  then // Next position on string
                       begin
                          Inc(voicingFormula[str]);
                          Break;
                       end
                  else // This is the last position on string
                       begin
                          if str = 5
                             then // Last string, no more voicing formulas
                                  begin
                                     finished := true;
                                     Break;
                                  end
                             else // Reset to first string position
                                  voicingFormula[str] := 0;
                       end;
            end;
      end;
end;

function TChordVoicingCollection.AddVoicing(positions : TVoicingPositions) : integer;
var
   voicing : TChordVoicing;
   checkMutedPosition : boolean;
   includeMutingVoicing : boolean;
   voicingTemplate : string;
   str : integer;
   fret : integer;
begin
   voicing := TChordVoicing.Create(positions, self);

   // Add the chord voicing to the list
   result := fChordVoicings.AddObject(voicing.voicingId, voicing);

   // Maybe add this one to the list of muted voicings to be checked
   voicingTemplate := '';
   checkMutedPosition := false;
   includeMutingVoicing := false;
   for str := 0 to 5 do
      begin
         fret := voicing.formula[str];
         if fret = -1
            then voicingTemplate := voicingTemplate + '??'
            else begin
                    voicingTemplate := voicingTemplate + IntToHex(fret, 2);
                     // Start checking for muted positions only if other non-muted positions have been encountered
                    if not checkMutedPosition
                       then checkMutedPosition := true;
                 end;
         if checkMutedPosition and (voicing.formula[str] = -1)
            then includeMutingVoicing := true;
      end;
  if includeMutingVoicing
     then fMutedVoicingsToCheck.Add(voicingTemplate);
end;

function TChordVoicingCollection.CompareVoicings(voicing1, voicing2 : TChordVoicing) : integer;
var
   index : integer;
   voicingProperty : TChordVoicingProperty;
begin
   result := 0;
   for index := 0 to fSortOrder.count - 1 do
      begin
         // Retrieve the property
         Assert(fSortOrder.objects[index] is TChordVoicingProperty);
         voicingProperty := TChordVoicingProperty(fSortOrder.objects[index]);

         // Maybe compare the 2 voicing properties
         if voicingProperty.sorted
            then begin
                    result := voicingProperty.Compare(voicing1, voicing2);
                    if result <> 0
                       then // Found the comparison
                            begin
                               // Maybe reverse the comparison
                               if voicingProperty.reverseOrder
                                  then result := -result;
                               Break;
                            end;
                 end;
      end;
end;

procedure TChordVoicingCollection.SetRoot(value : THalfTone);
begin
   if value <> fRoot
      then begin
              fRoot := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetQualities(value : THalfToneQualities);
begin
   if value <> fQualities
      then begin
              fQualities := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetMaxFret(value : integer);
begin
   if value <> fMaxFret
      then begin
              fMaxFret := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetMaxDifficulty(value : TChordVoicingDifficulty);
begin
   if value <> fMaxDifficulty
      then begin
              fMaxDifficulty := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetNoMutedStrings(value : boolean);
begin
   if value <> fNoMutedStrings
      then begin
              fNoMutedStrings := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetNoOpenStrings(value : boolean);
begin
   if value <> fNoOpenStrings
      then begin
              fNoOpenStrings := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetNoBarres(value : boolean);
begin
   if value <> fNoBarres
      then begin
              fNoBarres := value;
              ComputeVoicings;
           end;
end;

procedure TChordVoicingCollection.SetVoicingFilter(value : TChordVoicingFilter);
begin
   if value <> fVoicingFilter
      then begin
              fVoicingFilter := value;
              ComputeVoicings;
           end;
end;

function TChordVoicingCollection.GetCount : integer;
begin
   result := fChordVoicings.count;
end;

function TChordVoicingCollection.GetItem(index : integer) : TChordVoicing;
begin
   if (index >= 0) and (index < fChordVoicings.count)
      then begin
              Assert(fChordVoicings.objects[index] is TChordVoicing);
              result := TChordVoicing(fChordVoicings.objects[index]);
           end
      else result := nil;
end;

{ TChordVoicing }

constructor TChordVoicing.Create(positions : TVoicingPositions; collection : TChordVoicingCollection);
var
   rootPositionFound : boolean;
   position : RStringPosition;
   str : integer;
   fret : integer;
   voicingFingersFinder : TVoicingFingersFinder;
   firstFingering : TFingeringItem;
   absoluteHalfToneArray : array of double;
   mean, stdDev : extended;
   fingerIndex : integer;
   barreFingerFound : boolean;
   highestNote : integer;
   highestNoteQuality : THalfToneQuality;
begin
   // Inits
   Assert(collection <> nil);
   fCollection := collection;
   fVoicingId := '';
   for str := 0 to 5 do
      begin
         fPositions[str] := positions[str];
         if positions[str].fret = -1
            then fVoicingId := fVoicingId + 'FF'
            else fVoicingId := fVoicingId + IntToHex(positions[str].fret, 2);
      end;
   rootPositionFound := false;

   // Set the formula
   fMinFret := MaxInt;
   fMaxFret := 0;
   fFingerCount := 0;
   fOpenStrings := 0;
   fMutedStrings := 0;
   fStringCount := 0;
   highestNote := 0;
   highestNoteQuality := htqUnison;
{$IFDEF DebugVoicingsComputing}
   fEliminated := false;
{$ENDIF}
   SetLength(absoluteHalfToneArray, 0); // For brightness and contrast
   try
      for str := 0 to 5 do
         begin
            position := positions[str];
            fret := position.fret;
            fFormula[str] := fret;

            // Maybe increase open strings
            if fret = 0
               then Inc(fOpenStrings);

            // Maybe set min fret
            if (fret > 0) and (fret <= fMinFret)
               then fMinFret := fret;

            // Maybe set max fret
            if fret > fMaxFret
               then fMaxFret := fret;

            // Maybe increase muted strings
            if fret = -1
               then Inc(fMutedStrings)
               else begin
                       // Maybe increase the number of fingers
                       if fret <> 0
                          then Inc(fFingerCount);

                       // Increase string count
                       Inc(fStringCount);

                       // Maybe set root position
                       if not rootPositionFound
                          then begin
                                  fRootPosition := position;
                                  rootPositionFound := true;
                               end;

                       // Maybe update the highest note
                       if Ord(position.halfTone.halfTone) + position.halfTone.octave * 12 > highestNote
                          then begin
                                  highestNote := Ord(position.halfTone.halfTone) + position.halfTone.octave * 12;
                                  highestNoteQuality := position.quality;
                               end;

                       // Append item to halfToneArray
                       SetLength(absoluteHalfToneArray, Length(absoluteHalfToneArray) + 1);
                       absoluteHalfToneArray[High(absoluteHalfToneArray)] := position.halfTone.octave * 12 + Ord(position.halfTone.halfTone);
                    end;
         end;

      // Set highest note
      fHighestNote := THalfTone(highestNote mod 12);
      fHighestNoteQuality := highestNoteQuality;

      // Compute the brightness and the contrast
      MeanAndStdDev(absoluteHalfToneArray, mean, stdDev);

      fPreciseBrightness := Round(100 * (mean - MIN_MEAN) / (MAX_MEAN - MIN_MEAN));
      if fPreciseBrightness < 0
         then fPreciseBrightness := 0;
      if fPreciseBrightness > 100
         then fPreciseBrightness := 100;
      fBrightness := TChordVoicingBrightness(
                        Round(fPreciseBrightness * Ord(High(TChordVoicingBrightness)) / 100)
                     );
      if Ord(fBrightness) > Ord(High(TChordVoicingBrightness))
         then fBrightness := High(TChordVoicingBrightness);

      fPreciseContrast := Round(100 * (stdDev - MIN_STDDEV) / (MAX_STDDEV - MIN_STDDEV));
      if fPreciseContrast < 0
         then fPreciseContrast := 0;
      if fPreciseContrast > 100
         then fPreciseContrast := 100;
      fContrast := TChordVoicingContrast(
                      Round(fPreciseContrast * Ord(High(TChordVoicingContrast)) / 100)
                   );
      if Ord(fContrast) > Ord(High(TChordVoicingContrast))
         then fContrast := High(TChordVoicingContrast);
   finally
      SetLength(absoluteHalfToneArray, 0);
   end;

   // Compute the inversion
   fInversion := fCollection.GetInversion(fRootPosition.quality);
   fDrop2 := (fInversion = 3) // Drop2 is a special kind of third inversion
             and fCollection.IsDrop2Voicing(fFormula);
   Assert(fInversion >= 0);
   Assert(fInversion <= High(fCollection.fCandidateQualities));
   fInversionNote := THalfTone((Ord(fCollection.fRoot) + Ord(fCollection.fCandidateQualities[fInversion])) mod 12);

   // Compute barre
   ComputeFirstFingerBarre;

   // Compute the chord validity
   voicingFingersFinder := TVoicingFingersFinder.Create;
   try
      voicingFingersFinder.voicing := self;
      if voicingFingersFinder.fingerOrders.count = 0
         then // No valid fingering
              begin
                 fValidChord := false;
                 fScore := 10000;
                 fNatural := 10000;
                 fDifficulty := cvdImpossible;
              end
         else // Some valid fingering found
              begin
                 fValidChord := true;
                 Assert(voicingFingersFinder.fingerOrders.objects[0] is TFingeringItem);
                 firstFingering := TFingeringItem(voicingFingersFinder.fingerOrders.objects[0]);

                 // Assign a finger to each position
                 fingerIndex := 0;
                 if fFirstFingerBarreExtent > 0
                    then // Barre chord
                         begin
                            barreFingerFound := false;
                            for str := 0 to 5 do
                               if fPositions[str].fret > 0
                                  then // Finger used for this position
                                       begin
                                          if fPositions[str].fret = fMinFret
                                             then // Always first finger (barre)
                                                  begin
                                                     fPositions[str].finger := 1;
                                                     if not barreFingerFound
                                                        then begin
                                                                Inc(fingerIndex);
                                                                barreFingerFound := true;
                                                             end;
                                                  end
                                             else // Other finger
                                                  begin
                                                     fPositions[str].finger := firstFingering.order[fingerIndex];
                                                     Inc(fingerIndex);
                                                  end;
                                       end
                                  else // No finger user for this position
                                       fPositions[str].finger := -1
                         end
                    else // Regular chord
                         for str := 0 to 5 do
                            if fPositions[str].fret > 0
                               then // Finger used for this position
                                    begin
                                       fPositions[str].finger := firstFingering.order[fingerIndex];
                                       Inc(fingerIndex);
                                    end
                               else // No finger user for this position
                                    fPositions[str].finger := -1;

                 // Adjust the difficulty
                 fScore := firstFingering.score;
                 fNatural := firstFingering.natural;
                      if fScore > 300
                         then fDifficulty := cvdMortal
                 else if fScore > 200
                         then fDifficulty := cvdVeryHard
                 else if fScore > 100
                         then fDifficulty := cvdHard
                 else if fScore > 50
                         then fDifficulty := cvdMedium
                 else         fDifficulty := cvdEasy;
              end;
   finally
      voicingFingersFinder.Free;
   end;
end;

procedure TChordVoicing.ComputeFirstFingerBarre;
var
   str : integer;
   fret : integer;
   strStop : integer;
   lastMinFretStr : integer;
   actualBarrePositionCount : integer;
   oldFingerCount : integer;
begin
   fFirstFingerBarreStartingStr := -1;
   fFirstFingerBarreStartingStr := -1;
   actualBarrePositionCount := 0;
   strStop := -1;
   lastMinFretStr := -1;
   oldFingerCount := fFingerCount;
   fFingerCount := 0;
   for str := 5 downto 0 do
      begin
         fret := fPositions[str].fret;
              if (fFirstFingerBarreStartingStr = -1) and (fret = fMinFret)
                 then // Barre start
                      begin
                         fFirstFingerBarreStartingStr := str;
                         Inc(fFingerCount);
                         actualBarrePositionCount := 1;
                      end
         else if (fret = 0) and (fFirstFingerBarreStartingStr = -1)
                 then // Open position before the barre has started, the barre is impossible
                      begin
                         fFirstFingerBarreStartingStr := -1;
                         Break;
                      end
         else if (fFirstFingerBarreStartingStr <> -1) and (strStop = -1)
                 then // Inside barre
                      begin
                              if fret = fMinFret
                                 then // Remember lastMinFretStr
                                      begin
                                         Inc(actualBarrePositionCount);
                                         lastMinFretStr := str;
                                         //fPositions[str].fret := -2; // Replace barre position by -2
                                      end
                         else if fret = 0
                                 then // Fret 0, stop the barre
                                      strStop := str
                         else if fret <> -1
                                 then // Actual finger position
                                      Inc(fFingerCount);
                      end
         else         // Before barre
                      begin
                         if fret <> -1
                            then // Actual finger position
                                 Inc(fFingerCount);
                      end;
      end;

{
   // Set last barre position back to fMinFret
   if lastMinFretStr <> -1
      then fPositions[lastMinFretStr].fret := fMinFret;
}

   if (fFirstFingerBarreStartingStr <> -1) and (actualBarrePositionCount >= 2)
      then begin
              fFirstFingerBarreExtent := fFirstFingerBarreStartingStr - lastMinFretStr + 1;
              fBigBarre := fFirstFingerBarreStartingStr <= 2;
           end
      else begin
              fFirstFingerBarreExtent := 0;
              fFingerCount := oldFingerCount;
              fBigBarre := false;
           end;
end;

{ TChordVoicingProperty }

constructor TChordVoicingProperty.Create(collection : TChordVoicingCollection);
begin
   fCollection := collection;
   fSorted := true;
   fReverseOrder := false;
   fMovable := false;
   fCanHide := true;
end;

class function TChordVoicingProperty.GetMovable : boolean;
begin
   result := true;
end;

class function TChordVoicingProperty.GetMinWidth : integer;
begin
   result := -1;
end;

class function TChordVoicingProperty.GetMaxWidth : integer;
begin
   result := -1;
end;

{ TChordVoicing_Difficulty }

class function TChordVoicing_Difficulty.GetCaption : string;
begin
   result := 'Difficulty';
end;

class function TChordVoicing_Difficulty.GetMinWidth : integer;
begin
   result := 60;
end;

class function TChordVoicing_Difficulty.GetMaxWidth : integer;
begin
   result := 100;
end;

class function TChordVoicing_Difficulty.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.fDifficulty > voicing2.fDifficulty
           then result := 1
   else if voicing1.fDifficulty < voicing2.fDifficulty
           then result := -1
   else         result := 0;
end;

{ TChordVoicing_Fret }

class function TChordVoicing_Fret.GetCaption : string;
begin
   result := 'Fret';
end;

class function TChordVoicing_Fret.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.fMinFret > voicing2.fMinFret
           then result := 1
   else if voicing1.fMinFret < voicing2.fMinFret
           then result := -1
   else         result := 0;
end;

class function TChordVoicing_Fret.GetMinWidth: integer;
begin
   result := 35;
end;

class function TChordVoicing_Fret.GetMaxWidth: integer;
begin
   result := 35;
end;

{ TChordVoicing_Inversion }

class function TChordVoicing_Inversion.GetCaption : string;
begin
   result := 'Inversion';
end;

class function TChordVoicing_Inversion.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.inversion > voicing2.inversion
           then result := 1
   else if voicing1.inversion < voicing2.inversion
           then result := -1
   else         result := 0;
end;


{ TChordVoicing_MutedStrings }

class function TChordVoicing_MutedStrings.GetCaption : string;
begin
   result := 'Muted';
end;

class function TChordVoicing_MutedStrings.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.mutedStrings > voicing2.mutedStrings
           then result := 1
   else if voicing1.mutedStrings < voicing2.mutedStrings
           then result := -1
   else         result := 0;
end;

class function TChordVoicing_MutedStrings.GetMinWidth: integer;
begin
   result := 43;
end;

class function TChordVoicing_MutedStrings.GetMaxWidth: integer;
begin
   result := 43;
end;

{ TChordVoicing_OpenString }

class function TChordVoicing_OpenString.GetCaption : string;
begin
   result := 'Open';
end;

class function TChordVoicing_OpenString.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.fBigBarre > voicing2.fBigBarre
           then result := 1
   else if voicing1.fBigBarre < voicing2.fBigBarre
           then result := -1
   else         begin
                        if voicing1.openStrings < voicing2.openStrings
                           then result := 1
                   else if voicing1.openStrings > voicing2.openStrings
                           then result := -1
                   else         result := 0;
                end;
end;

class function TChordVoicing_OpenString.GetMinWidth: integer;
begin
   result := 40;
end;

class function TChordVoicing_OpenString.GetMaxWidth: integer;
begin
   result := 60;
end;

{ TChordVoicing_Strings }

class function TChordVoicing_Strings.GetCaption : string;
begin
   result := 'Strings';
end;

class function TChordVoicing_Strings.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.stringCount < voicing2.stringCount
           then result := 1
   else if voicing1.stringCount > voicing2.stringCount
           then result := -1
   else         result := 0;
end;

class function TChordVoicing_Strings.GetMinWidth: integer;
begin
   result := 45;
end;

class function TChordVoicing_Strings.GetMaxWidth: integer;
begin
   result := 45;
end;

{ TChordVoicing_Fingers }

class function TChordVoicing_Fingers.GetCaption : string;
begin
   result := 'Fingers';
end;

class function TChordVoicing_Fingers.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.fFingerCount > voicing2.fFingerCount
           then result := 1
   else if voicing1.fFingerCount < voicing2.fFingerCount
           then result := -1
   else         result := 0;
end;

class function TChordVoicing_Fingers.GetMinWidth: integer;
begin
   result := 50;
end;

class function TChordVoicing_Fingers.GetMaxWidth: integer;
begin
   result := 50;
end;

{ TChordVoicing_Brightness }

class function TChordVoicing_Brightness.GetCaption : string;
begin
   result := 'Brightness';
end;

class function TChordVoicing_Brightness.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.fBrightness < voicing2.fBrightness
           then result := 1
   else if voicing1.fBrightness > voicing2.fBrightness
           then result := -1
   else         result := 0;
end;

class function TChordVoicing_Brightness.GetMinWidth: integer;
begin
   result := 65;
end;

class function TChordVoicing_Brightness.GetMaxWidth: integer;
begin
   result := 70;
end;

{ TChordVoicing_Voicing }

class function TChordVoicing_Voicing.Compare(voicing1, voicing2: TChordVoicing) : integer;
begin
   result := 0;
end;

constructor TChordVoicing_Voicing.Create(collection : TChordVoicingCollection);
begin
   inherited Create(collection);
   fCanHide := false;
   fMovable := false;
end;

class function TChordVoicing_Voicing.GetCaption: string;
begin
   result := 'Voicing';
end;

class function TChordVoicing_Voicing.GetMovable : boolean;
begin
   result := false;
end;

{ TChordVoicing_Contrast }

class function TChordVoicing_Contrast.GetCaption: string;
begin
   result := 'Contrast';
end;

class function TChordVoicing_Contrast.Compare(voicing1, voicing2 : TChordVoicing) : integer;
begin
        if voicing1.fContrast < voicing2.fContrast
           then result := 1
   else if voicing1.fContrast > voicing2.fContrast
           then result := -1
   else         result := 0;
end;

{ TVoicingStringList }

function TVoicingStringList.CompareStrings(const S1, S2: string): Integer;
var
   index : integer;
   s : string;
begin
   s := s2;
   for index := 1 to Length(s1) do
      if (Length(s) >= index) and (s[index] = '?')
         then s[index] := s1[index];
   result := AnsiCompareStr(s1, s);
end;

end.
