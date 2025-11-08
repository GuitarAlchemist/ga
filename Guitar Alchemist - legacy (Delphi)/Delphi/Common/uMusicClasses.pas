unit uMusicClasses;

interface

uses
   Classes
   ,SysUtils
   ,Graphics
   ;

// Misc types
type

// Strings and frets
   TString = shortint;
   TFret = shortint;


   TOctave = 0..8;
   TTone = (tC, tD, tE, tF, tG, tA, tB);
   THalfTone = (htC, htCSharpDFlat, htD, htDSharpEFlat, htE, htF, htFSharpGFlat, htG, htGSharpAFlat, htA, htASharpBFlat, htB);
   THalfTones = set of THalfTone;
   TChordNoteCount = 3..7;

   TNoteAccidental = (naNone, naDoubleFlat, naFlat, naNatural, naSharp, naDoubleSharp);

   TNoteDuration = (ndDoubleWholeNote, ndWholeNote, ndHalfNote, ndQuarterNote, ndEighthNote,
                    ndSixteenthNote, ndThirtySecondNote, ndSixtyFourthNote);

   THalfToneQuality = (htqUnison, htqMinorSecond, htqMajorSecond, htqMinorThird, htqMajorThird, htqPerfectFourth,
                       htqDiminishedFifth, htqPerfectFifth, htqAugmentedFifth, htqMajorSixth, htqMinorSeventh, htqMajorSeventh,
                       htqOctave, htqMinorNinth, htqMajorNinth, htqAugmentedNinth, htqMajorTenth, htqPerfectEleventh,
                       htqAugmentedEleventh, htqPerfectTwelfth, htqMinorThirteenth, htqMajorThirteenth);
   THalfToneQualities = set of THalfToneQuality;
   THalfToneQualityArray = array of THalfToneQuality;

   TToneQuality = (tqUnison, tqSecond, tqThird, tqFourth, tqFifth, tqSixth, tqSeventh, tqOctave,
                   tqNinth, tqTenth, tqEleventh, tqTwelfth, tqThirteenth);
   TToneQualities = set of TToneQuality;

   TToneQualityAlteration = (tqaDiminished, tqaMinor, tqaPerfect, tqaMajor, tqaAugmented);

   TKey = (ksNone, ksCMajorAMinor,
           ksGMajorEMinor, ksDMajorBMinor, ksAMajorFSharpMinor, ksEMajorCSharpMinor,
           ksBMajorGSharpMinor, ksFSharpMajorDSharpMinor, ksCSharpMajorASharpMinor,
           ksFMajorDMinor, ksBFlatMajorGMinor, ksEFlatMajorCMinor, ksAFlatMajorFMinor,
           ksDFlatMajorBFlatMinor, ksGFlatMajorEFlatMinor, ksCFlatMajorAFlatMinor);

   TKeyPreference = (kpNone, kpMajor, kpMinor);

   TChordKind = (ckMajor, ckMinor, ckDominant, ckSpecial, ckNone);
   TChordThirdKind = (ckMajorThird, ckMinorThird, ckSus4, ckSus2, ckPower);
   TChordFifthKind = (ckPerfectFifth, ckDiminishedFifth, ckAugmentedFifth);
   TChordSeventhKind = (ckNoSeventh, ckDimSeventh, ckHalfDimSeventh, ckMinorSeventh, ckMajorSeventh, ckMinorMajorSeventh);
   TChordSeventhExtensionKind = (cseNone, cseSeventh, cseNinth, cseEleventh, cseThirteenth);
   TChordNinthKind = (ckNoNinth, ckMajorNinth, ckMinorNinth, ckAumentedNinth);

   TCharacterTone = (ctNone, ctPrimary, ctSecondary);


   RHalfTone = record
      halfTone : THalfTone;
      octave : TOctave;
   end;

   THalfToneArray = array of RHalfTone;

   RTone = record
      tone : TTone;
      octave : TOctave;
   end;

   // Chord voicings
   TChordVoicingFormula = array[0..5] of integer;
   RStringPosition = record
      fret : integer;
      halfTone : RHalfTone;
      quality : THalfToneQuality;
      finger : integer;
   end;
   TStringPositionArray = array of RStringPosition;
   TChordPositionArray = array of TStringPositionArray;
   TVoicingPositions = array[0..5] of RStringPosition;
   TVoicingFingersOrder = array[0..3] of integer;
   TVoicingFingersOrderArray = array of TVoicingFingersOrder;

   TFinger = 1..4;
   TFingers = set of TFinger;

// Misc constants
const
   FRET_COUNT = 19;

   MAJOR_KEY_ROOT : array [TKey] of THalfTone = (htC, htC, htG, htD, htA, htE, htB, htFSharpGFlat, htCSharpDFlat,
                                                 htF, htASharpBFlat, htDSharpEFlat, htGSharpAFlat, htCSharpDFlat,
                                                 htFSharpGFlat, htB);
   KEY_NAME_MAJOR : array [TKey] of string =
   ('(None)', 'C', 'G', 'D', 'A', 'E', 'B', 'F#', 'C#', 'F', 'Bb', 'Eb', 'Ab', 'Db', 'Gb', 'Cb');
   KEY_NAME_MINOR : array [TKey] of string =
   ('(None)', 'Am', 'Em', 'Bm', 'F#m', 'C#m', 'G#m', 'D#m', 'A#m', 'Dm', 'Gm', 'Cm', 'Fm', 'Bbm', 'Ebm', 'Abm');

   MAJOR_KEY_NAME : array [TKey] of string = ('(None)', 'C', 'G', 'D', 'A', 'E', 'B', 'F#', 'C#', 'F', 'Bb', 'Eb', 'Ab', 'Db', 'Gb', 'Cb');
   MINOR_KEY_NAME : array [TKey] of string = ('(None)', 'A', 'E', 'B', 'F#', 'C#', 'G#', 'D#', 'A#', 'D', 'G', 'C', 'F', 'Bb', 'Eb', 'Ab');

   HALFTONE_NAME_FLAT : array [THalfTone] of string = ('C', 'Db', 'D', 'Eb', 'E', 'F', 'Gb', 'G', 'Ab', 'A', 'Bb', 'B');
   HALFTONE_NAME_SHARP : array [THalfTone] of string = ('C', 'C#', 'D', 'D#', 'E', 'F', 'F#', 'G', 'G#', 'A', 'A#', 'B');
   HALFTONE_NAME_BOTH : array [THalfTone] of string = ('C', 'C#/Db', 'D', 'D#/Eb', 'E', 'F', 'F#/Gb', 'G', 'G#/Ab', 'A', 'A#/Bb', 'B');
   HALFTONE_NAME_SMART : array [THalfTone] of string = ('C', 'C#', 'D', 'Eb', 'E', 'F', 'F#', 'G', 'G#', 'A', 'Bb', 'B');

   KEY_TONE_IS_ACCIDENTED : array [TKey, TTone] of boolean =
   (
      (false, false, false, false, false, false, false), // ksNone
      (false, false, false, false, false, false, false), // ksCMajorAMinor
      (false, false, false, true,  false, false, false), // ksGMajorEMinor
      (true,  false, false, true,  false, false, false), // ksDMajorBMinor
      (true,  false, false, true,  true,  false, false), // ksAMajorFSharpMinor
      (true,  true,  false, true,  true,  false, false), // ksEMajorCSharpMinor
      (true,  true,  false, true,  true,  true,  false), // ksBMajorGSharpMinor
      (true,  true,  true,  true,  true,  true,  false), // ksFSharpMajorDSharpMinor
      (true,  true,  true,  true,  true,  true,  true ), // ksCSharpMajorASharpMinor
      (false, false, false, false, false, false, true ), // ksFMajorDMinor
      (false, false, true,  false, false, false, true ), // ksBFlatMajorGMinor
      (false, false, true,  false, false, true,  true ), // ksEFlatMajorCMinor
      (false, true,  true,  false, false, true,  true ), // ksAFlatMajorFMinor
      (false, true,  true,  false, true,  true,  true ), // ksDFlatMajorBFlatMinor
      (true,  true,  true,  false, true,  true,  true ), // ksGFlatMajorEFlatMinor
      (true,  true,  true,  true,  true,  true,  true )  // ksCFlatMajorAFlatMinor
   );

   TONE_NAME : array [TTone] of string = ('C', 'D', 'E', 'F', 'G', 'A', 'B');

   ACCIDENTAL_NAME : array [TNoteAccidental] of string = ('', 'bb', 'b', '', '#', 'x');

   HALFTONE_QUALITY_NUMBER : array [THalfToneQuality] of string =
   (
      '1', 'b2', '2', 'b3', '3', '4', 'b5', '5', '#5', '6', 'b7', '7', '8', 'b9', '9', '#9',
      '10', '11', '#11', '12', 'b13', '13'
   );


   CHORD_KIND_NAME : array[TChordKind] of string =
   (
      'major',
      'minor',
      'dominant',
      'special',
      '(none)'
   );

   STANDARD_GUITAR_TUNING : array[0..5] of RHalfTone =
      ((halfTone: htE; octave: 0)
       ,(halfTone: htA; octave: 0)
       ,(halfTone: htD; octave: 1)
       ,(halfTone: htG; octave: 1)
       ,(halfTone: htB; octave: 1)
       ,(halfTone: htE; octave: 2)
       );

   HALFTONE_NAMES : array[THalfTone] of string =
      ('C', 'C#/Db', 'D', 'D#/Eb', 'E', 'F', 'F#/Gb', 'G', 'G#/Ab', 'A', 'A#/Bb', 'B');


// Stuff for computing the qualities of scale degrees
const
   KEY_ACCIDENTAL : array [THalfTone, TTone] of shortint =
   (
      (0, 0, 0, 0, 0, 0, 0)
     ,(0, -1, -1, 0, -1, -1, -1)
     ,(1, 0, 0, 1, 0, 0, 0)
     ,(0, 0, -1, 0, 0, -1, -1)
     ,(1, 1, 0, 1, 1, 0, 0)
     ,(0, 0, 0, 0, 0, 0, -1)
     ,(-1, -1, -1, 0, -1, -1, -1)
     ,(0, 0, 0, 1, 0, 0, 0)
     ,(0, -1, -1, 0, 0, -1, -1)
     ,(1, 0, 0, 1, 1, 0, 0)
     ,(0, 0, -1, 0, 0, 0, -1)
     ,(1, 1, 0, 1, 1, 1, 0)
   );

   DEGREE_ACCIDENTAL : array [THalfTone] of shortint =
   (0, -1, 0, -1, 0, 0, -1, 0, -1, 0, -1, 0);

   DIATONIC_TONE : array [THalfTone] of shortint =
   (0, 1, 1, 2, 2, 3, 4, 4, 5, 5, 6, 6);


   CHOMATIC_TONE : array [TTone] of shortint =
   (0, 2, 4, 5, 7, 9, 11);


   TONE_ALTERATIONS_TABLE : array [TTone, THalfTone] of integer =
   (
      (  0,  1,  2,127,127,127,127,-128,-128,-128, -2, -1),
      ( -2, -1,  0,  1,  2,127,127,127,127,-128,-128,-128),
      (-128,-128, -2, -1,  0,  1,  2,127,127,127,127,-128),
      (-128,-128,-128, -2, -1,  0,  1,  2,127,127,127,127),
      (127,127,-128,-128,-128, -2, -1,  0,  1,  2,127,127),
      (127,127,127,127,-128,-128,-128, -2, -1,  0,  1,  2),
      (  1,  2,127,127,127,127,-128,-128,-128, -2, -1,  0)
   );


   HALFTONE_TO_SHARPKEY_MAJOR : array [THalfTone] of TKey =
   (
      ksCMajorAMinor, ksCSharpMajorASharpMinor, ksDMajorBMinor, ksNone, ksEMajorCSharpMinor, ksNone,
      ksFSharpMajorDSharpMinor, ksGMajorEMinor, ksNone, ksAMajorFSharpMinor, ksNone, ksBMajorGSharpMinor
   );
   HALFTONE_TO_FLATKEY_MAJOR : array [THalfTone] of TKey =
   (
      ksNone, ksDFlatMajorBFlatMinor, ksNone, ksEFlatMajorCMinor, ksNone, ksFMajorDMinor, ksGFlatMajorEFlatMinor,
      ksNone, ksAFlatMajorFMinor, ksNone, ksBFlatMajorGMinor, ksCFlatMajorAFlatMinor
   );
   HALFTONE_TO_SHARPKEY_MINOR : array [THalfTone] of TKey =
   (
      ksNone, ksEMajorCSharpMinor, ksNone, ksFSharpMajorDSharpMinor, ksGMajorEMinor, ksNone, ksAMajorFSharpMinor,
      ksNone, ksBMajorGSharpMinor, ksCMajorAMinor, ksCSharpMajorASharpMinor, ksDMajorBMinor
   );
   HALFTONE_TO_FLATKEY_MINOR : array [THalfTone] of TKey =
   (
      ksEFlatMajorCMinor, ksNone, ksFMajorDMinor, ksGFlatMajorEFlatMinor, ksNone, ksAFlatMajorFMinor, ksNone,
      ksBFlatMajorGMinor, ksCFlatMajorAFlatMinor, ksNone, ksDFlatMajorBFlatMinor, ksNone
   );

   HALFTONE_IS_SHARP_KEY : array [THalfTone] of boolean =
   (
      true, true, true, false, true, false, true, true, false, true, false, true
   );

   KEYROOT_HALFTONE_MAJOR : array[TKey] of shortint =
   (0, 0, 7, 2, 9, 4, 11, 6, 1, 5, 10, 3, 8, 1, 6, 11);
   KEYROOT_TONE_MAJOR : array[TKey] of shortint =
   (0, 0, 4, 1, 5, 2, 6, 3, 0, 3, 6, 2, 5, 1, 4, 0);


   KEYROOT_HALFTONE_MINOR : array[TKey] of shortint =
   (0, 9, 4, 11, 6, 1, 8, 3, 10, 2, 7, 0, 5, 10, 3, 8);
   KEYROOT_TONE_MINOR : array[TKey] of shortint =
   (0, 5, 2, 6, 3, 0, 4, 1, 5, 1, 4, 0, 3, 6, 2, 5);
type

// Chord templates
   TChordTemplate = class
      private
         fQualities : THalfToneQualities;
         fQualityCount : integer;
         
         fKind : TChordKind;
         fThirdKind : TChordThirdKind;
         fFifthKind : TChordFifthKind;
         fNinthKind : TChordNinthKind;
         fSeventhKind : TChordSeventhKind;
         fAlterations : integer;
         fSeventhExtensionKind : TChordSeventhExtensionKind;
         fHalfDimSeventh : boolean;
         fDimSeventh : boolean;
         fDimOrHalfDimSeventh : boolean;
         fContainsSixth : boolean;
         fContainsSeventh : boolean;
         fContainsNinth : boolean;
         fContainsEleventh : boolean;
         fContainsOnlyStackedThirds : boolean;
         fHighestQuality : THalfToneQuality;
         fChordDegrees : array of THalfToneQuality;
         fChordDegreeCount : integer;
         fChordName : string;
         function GetChordDegree(index : integer) : THalfToneQuality;
         function ComputeChordName : string;
         class procedure FixupQualities(var qualities : THalfToneQualities);
      public
         constructor Create(qualities : THalfToneQualities; containsOnlyStackedThirds : boolean);
         destructor Destroy; override;
         class function GetQualityNames(qualities : THalfToneQualities) : string;
         property chordName : string read fChordName;
         property chordDegrees[index : integer] : THalfToneQuality read GetChordDegree;
         property chordDegreesCount : integer read fChordDegreeCount;
         property qualities : THalfToneQualities read fQualities;
         property qualityCount : integer read fQualityCount;
         property kind : TChordKind read fKind;
         property thirdKind : TChordThirdKind read fThirdKind;
         property fifthKind : TChordFifthKind read fFifthKind;
         property ninthKind : TChordNinthKind read fNinthKind;
         property alterations : integer read fAlterations;
         property containsSixth : boolean read fContainsSixth;
         property containsSeventh : boolean read fContainsSeventh;
         property containsNinth : boolean read fContainsNinth;
         property containsEleventh : boolean read fContainsEleventh;
         property highestQuality : THalfToneQuality read fHighestQuality;
         property containsOnlyStackedThirds : boolean read fContainsOnlyStackedThirds;
         property seventhKind : TChordSeventhKind read fSeventhKind;
         property seventhExtensionKind : TChordSeventhExtensionKind read fSeventhExtensionKind;
         property dimSeventh : boolean read fDimSeventh;
         property halfDimSeventh : boolean read fHalfDimSeventh;
         property dimOrHalfDimSeventh : boolean read fDimOrHalfDimSeventh;
    end;


// Scales
type
   TScale = class(TPersistent)
      private
         fModeNames : TStrings;
         fName : string;
         fMode : integer;
         fModesAllowed : boolean;
         fMinor : boolean;
         fKeyPreference : TKeyPreference;
         fCanHarmonize : boolean;         
         fIntervals : array of smallint;
         fDegreeQualities : array of THalfToneQuality;
         fDegreeRefQualities : array of THalfToneQuality;
         fDegreeAccidentals : array of TNoteAccidental;
         fDegreeCharacterTones : array of TCharacterTone;
// todo:         fDegreeEnharmonics : 
         fQualities : THalfToneQualities;
         fMatchingChords : TStrings;
         fStackedThirds : THalfToneQualities;
         fStackedFourths : THalfToneQualities;
         fAllowIncrementalMatchingChordList : boolean;
         fIncrementalMatchingChordListDone : boolean;
         function GetDegreeInterval(index : integer) : integer;
         function GetDegreeQuality(index : integer) : THalfToneQuality;
         function GetStackedThirds : THalfToneQualities;
         function GetStackedFourths : THalfToneQualities;
         function GetMatchingChords : TStrings; overload;
      protected
         procedure ComputeDegreeQualities;
      public
         constructor Create;
         destructor Destroy; override;
         procedure SetScale(scaleIntervals : array of smallint; mode : integer = 1);
         function GetFromRepository(scaleName : string; mode : integer = 1) : boolean;
         function GetIntervalBetweenDegrees(fromDegree, toDegree : integer) : integer;
         function GetScaleName(key : TKey) : string; overload;
         function GetScaleName : string; overload;
         function GetModeName : string;
         function GetDegreeHalfTone(key : TKey; degree : integer) : THalfTone;
         function ContainsQualities(qualities : THalfToneQualities) : boolean;
         function count : integer;
         function GetMatchingChordsEx(requiredQualities : THalfToneQualities; chordKindFilter : TChordKind) : TStrings;
         procedure ClearMatchingChords;
         procedure SortIncrementalMatchingChordList;
         procedure SetAllowIncrementalMatchingChordList(value : boolean);
         property degreeInterval[index : integer] : integer read GetDegreeInterval;
         property degreeQuality[index : integer] : THalfToneQuality read GetDegreeQuality;
         property matchingChords : TStrings read GetMatchingChords;
         property mode : integer read fMode;
         property modesAllowed : boolean read fModesAllowed;
         property isMinor : boolean read fMinor;
         property keyPreference : TKeyPreference read fKeyPreference;
         property canHarmonize : boolean read fCanHarmonize;
         property stackedThirds : THalfToneQualities read GetStackedThirds;
         property stackedFourths : THalfToneQualities read GetStackedFourths;
         property allowIncrementalMatchingChordList : boolean read fAllowIncrementalMatchingChordList write SetAllowIncrementalMatchingChordList;
         property modeNames : TStrings read fModeNames;
   end;

   TScaleRepositoryItem = class
      private
         fScaleIntervals : array of smallint;
         fScaleModes : TStringList;
         fAllowModes : boolean;
         fVisible : boolean;
         fKeyPreference : TKeyPreference;
      public
         constructor Create(aScaleIntervals : array of smallint; aScaleModes : array of string;
                            allowModes : boolean = true; visible : boolean = true; keyPreference : TKeyPreference = kpNone);
         destructor Destroy; override;
   end;

   TScaleRepository = class
      private
         fScales : TStringList;
      public
         constructor Create;
         destructor Destroy; override;
         function RegisterScale(scaleName : string; scaleIntervals : array of smallint;
                                allowModes : boolean = true; visible : boolean = true;
                                keyPreference : TKeyPreference = kpNone) : boolean; overload;
         function RegisterScale(scaleName : string; scaleIntervals : array of smallint; scaleModeNames : array of string;
                                allowModes : boolean = true; visible : boolean = true;
                                keyPreference : TKeyPreference = kpNone) : boolean;  overload;
         function UnregisterScale(scaleName : string) : boolean;
         procedure GetNames(names : TStrings; suffix : string = '');
         procedure GetModes(scaleName : string; names : TStrings; numberPrefix : boolean = false; suffix : string = '');
         function GetScale(scale : TScale; scaleName : string; mode : integer = 1) : boolean; overload;
         function GetScale(scale : TScale; scaleName : string; out modesAllowed : boolean; mode : integer = 1) : boolean; overload;
         function AreModeAllowed(scaleName : string) : boolean;
         function IsScaleVisible(scaleName : string) : boolean;
   end;


const
   TONE_ACCIDENTED_IN_KEY : array[TKey, TTone] of boolean =
      (
         (false, false, false, false, false, false, false), // None
         (false, false, false, false, false, false, false), // C

         (false, false, false, true, false, false, false), // G
         (true, false, false, true, false, false, false), // D
         (true, false, false, true, true, false, false), // A
         (true, true, false, true, true, false, false), // E
         (true, true, false, true, true, true, false), // B
         (true, true, true, true, true, true, false), // F#
         (true, true, true, true, true, true, true), // C#

         (false, false, false, false, false, false, true), // F
         (false, false, true, false, false, false, true), // Bb
         (false, false, true, false, false, true, true), // Eb
         (false, true, true, false, false, true, true), // Ab
         (false, true, true, false, true, true, true), // Db
         (true, true, true, false, true, true, true), // Gb
         (true, true, true, true, true, true, true) // Cb
      );


   HALFTONE_TO_KEY : array[THalfTone] of TKey =
      (
         ksCMajorAMinor,
         ksDFlatMajorBFlatMinor,
         ksDMajorBMinor,
         ksEFlatMajorCMinor,
         ksEMajorCSharpMinor,
         ksFMajorDMinor,
         ksGFlatMajorEFlatMinor,
         ksGMajorEMinor,
         ksAFlatMajorFMinor,
         ksAMajorFSharpMinor,
         ksBFlatMajorGMinor,
         ksBMajorGSharpMinor
      );

   function GetKeyRoot(key : TKey; minor : boolean = false) : THalfTone;

   function GetToneDifference(tone1, tone2 : RTone) : integer;

   function GetHalfToneQuality(halfTone, refHalfTone : THalfTone) : THalfToneQuality;

   function HalfToneQualityToToneQuality(quality : THalfToneQuality; useFlatFifth : boolean = false; useFlatSixth : boolean = false) : TToneQuality;

   function GetHalfTonesFromQualities(qualities : THalfToneQualities; halfTone : THalfTone) : THalfTones;

   function ToneToHalftoneQuality(quality : TToneQuality) : THalfToneQuality;

   function GetQualityColor(halfToneQuality : THalfToneQuality) : TColor; overload;
   function GetQualityColor(toneQuality : TToneQuality; accidental : TNoteAccidental) : TColor; overload;

   function MajorToMinorKey(key : TKey) : TKey;

   function MinorToMajorKey(key : TKey) : TKey;

   function PositionHalfTone(str : TString; fret : TFret) : RHalfTone;

   function IsHalfToneQualityOrderedSubsetOf(a, b : THalfToneQualities) : boolean;

   function LimitQualityToFirstOctave(quality : THalfToneQuality) : THalfToneQuality;

   procedure LimitQualitiesToFirstOctave(var qualities : THalfToneQualities);

   function SameQualityOctaveQuality(q1, q2 : ThalfToneQuality) : boolean;

   function ComputeModeDevirativeScaleRoot(scaleName : string; scaleMode : integer; scaleRoot : THalfTone) : THalfTone;

   function ComputeScaleModeFromRoot(scaleName : string; parentScaleRoot : THalfTone; halfTone : THalfTone) : integer;

   function GetScaleHalftones(scaleName : string; scaleRoot : THalfTone) : THalfTones;

   function ComputeScaleModeRoot(scaleName : string; scaleMode : integer; scaleRoot : THalfTone) : THalfTone;

   function IsAlteration(quality : THalfToneQuality) : boolean;

   function GetScaleKeyPreference(scaleName : string) : TKeyPreference;


var
   globalScaleRepository : TScaleRepository;

implementation

uses
   uMusicFontRoutines
   ;

const
   QUALITY_COLORS : array [0..11] of TColor =
   (
      $000000
      ,$D50075
      ,$D50005
      ,$D56A00
      ,$D5D500
      ,$85D500
      ,$00D525
      ,$00D5D5
      ,$009AD5
      ,$002BD5
      ,$6A00D5
      ,$BF00D5
   );

function GetQualityColor(halfToneQuality : THalfToneQuality) : TColor;
begin
   result := QUALITY_COLORS[Ord(halfToneQuality) mod 12];
end;

function GetQualityColor(toneQuality : TToneQuality; accidental : TNoteAccidental) : TColor;
var
   halfToneQuality : THalfToneQuality;
begin
   halfToneQuality := THalfToneQuality(Ord(ToneToHalftoneQuality(toneQuality)) + Ord(accidental) - Ord(naNatural));
   result := GetQualityColor(halfToneQuality);
end;

function MajorToMinorKey(key : TKey) : TKey;
const
   MAJOR_TO_MINOR_KEY : array [TKey] of TKey =
   (
      ksEFlatMajorCMinor, ksEFlatMajorCMinor, ksBFlatMajorGMinor, ksFMajorDMinor, ksCMajorAMinor, ksGMajorEMinor, ksDMajorBMinor,
      ksAMajorFSharpMinor, ksEMajorCSharpMinor, ksAFlatMajorFMinor, ksDFlatMajorBFlatMinor, ksGFlatMajorEFlatMinor,
      ksCFlatMajorAFlatMinor, ksEMajorCSharpMinor, ksAMajorFSharpMinor, ksDMajorBMinor
   );
begin
   result := MAJOR_TO_MINOR_KEY[key];
end;

function MinorToMajorKey(key : TKey) : TKey;
const
   MINOR_TO_MAJOR_KEY : array [TKey] of TKey =
   (
      ksAMajorFSharpMinor, ksAMajorFSharpMinor, ksEMajorCSharpMinor, ksBMajorGSharpMinor, ksFSharpMajorDSharpMinor, ksCSharpMajorASharpMinor,
      ksAFlatMajorFMinor, ksEFlatMajorCMinor, ksBFlatMajorGMinor, ksDMajorBMinor, ksGMajorEMinor, ksCMajorAMinor,
      ksFMajorDMinor, ksBFlatMajorGMinor, ksEFlatMajorCMinor, ksAFlatMajorFMinor
   );
begin
   result := MINOR_TO_MAJOR_KEY[key];
end;

function GetKeyRoot(key : TKey; minor : boolean) : THalfTone;
begin
   result := MAJOR_KEY_ROOT[key];
   if minor
      then result := THalfTone((Ord(result) + 8) mod 12);
end;

function GetToneDifference(tone1, tone2 : RTone) : integer;
begin
   result := Ord(tone1.tone) + tone1.octave * 7 - (Ord(tone2.tone) + tone2.octave * 7);
end;

function GetHalfToneQuality(halfTone, refHalfTone : THalfTone) : THalfToneQuality;
begin
   result := THalfToneQuality((Ord(halfTone) + 12 - Ord(refHalfTone)) mod 12);
end;

function HalfToneQualityToToneQuality(quality : THalfToneQuality; useFlatFifth, useFlatSixth : boolean) : TToneQuality;
const
   TONE_QUALITY_TABLE : array[THalfToneQuality] of TToneQuality =
   (tqUnison, tqSecond, tqSecond, tqThird, tqThird, tqFourth, tqFourth, tqFifth, tqFifth, tqSixth, tqSeventh, tqSeventh,
    tqOctave, tqNinth, tqNinth, tqTenth, tqTenth, tqEleventh, tqEleventh, tqTwelfth, tqTwelfth, tqThirteenth);
begin
        if (quality = htqDiminishedFifth) and useFlatFifth
           then result := tqFifth
   else if (quality = htqAugmentedFifth) and useFlatSixth
           then result := tqSixth
   else         result := TONE_QUALITY_TABLE[quality];
end;

function GetHalfTonesFromQualities(qualities : THalfToneQualities; halfTone : THalfTone) : THalfTones;
var
   index : integer;
   quality : THalfToneQuality;
   h : THalfTone;
begin
   result := [];
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         quality := THalfToneQuality(index);
         if quality in qualities
            then begin
                    h := THalfTone((Ord(halfTone) + Ord(quality)) mod 12);
                    Include(result, h);
                 end;
      end;
end;

function ToneToHalftoneQuality(quality : TToneQuality) : THalfToneQuality;
const
   TRANSFORM : array[TToneQuality] of THalfToneQuality =
   (
       htqUnison,
       htqMajorSecond,
       htqMajorThird,
       htqPerfectFourth,
       htqPerfectFifth,
       htqMajorSixth,
       htqMajorSeventh,
       htqOctave,
       htqMajorNinth,
       htqMajorTenth, htqPerfectEleventh,
       htqPerfectTwelfth,
       htqMajorThirteenth
   );
begin
   result := TRANSFORM[quality];
end;

function PositionHalfTone(str : TString; fret : TFret) : RHalfTone;
var
   h : integer;
begin
   h := Ord(STANDARD_GUITAR_TUNING[str].halfTone) + STANDARD_GUITAR_TUNING[str].octave * 12;
   h := h + fret;
   result.halfTone := THalfTone(h mod 12);
   result.octave := h div 12;
end;

function IsHalfToneQualityOrderedSubsetOf(a, b : THalfToneQualities) : boolean;
var
   index : integer;
   missingQualities : THalfToneQualities;
   quality : THalfToneQuality;
begin
   result := false;
   missingQualities := [];
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         quality := THalfToneQuality(index);
         if quality in b
            then // Quality should be in a
                 begin
                    if not(quality in a)
                       then missingQualities := missingQualities + [quality]
                       else begin
                               if missingQualities <> []
                                  then Exit;
                            end;
                 end
            else // Quality should not be in a
                 if quality in a
                    then Exit;
      end;
   result := true;
end;

function LimitQualityToFirstOctave(quality : THalfToneQuality) : THalfToneQuality;
begin
   if quality >= htqOctave
      then result := THalfToneQuality(Ord(quality) - 12)
      else result := quality;
end;

procedure LimitQualitiesToFirstOctave(var qualities : THalfToneQualities);
var
   index : integer;
   quality : THalfToneQuality;
begin
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         quality := THalfToneQuality(index);
         if (quality >= htqOctave)
            and (quality in qualities)
            then begin
                    Exclude(qualities, quality);
                    quality := THalfToneQuality(Ord(quality) - 12);
                    Include(qualities, quality);
                 end;
      end;
end;

function SameQualityOctaveQuality(q1, q2 : ThalfToneQuality) : boolean;
begin
   result := LimitQualityToFirstOctave(q1) = LimitQualityToFirstOctave(q2);
end;

function ComputeModeDevirativeScaleRoot(scaleName : string; scaleMode : integer; scaleRoot : THalfTone) : THalfTone;
var
   modeIntervals : shortint;
   scale : TScale;
   index : integer;
   modesAllowed : boolean;
begin
   // Sum up the intervals between each degree for the mode
   modeIntervals := 0;
   scale := TScale.Create;
   try
      globalScaleRepository.GetScale(scale, scaleName, modesAllowed);
      for index := 0 to scaleMode - 2 do
         Inc(modeIntervals, scale.degreeInterval[index]);
   finally
      scale.Free;
   end;

   // Substract the mode intervals from the scale root
   result := THalfTone( (Ord(scaleRoot) + 12 - modeIntervals) mod 12 );
end;

function ComputeScaleModeFromRoot(scaleName : string; parentScaleRoot : THalfTone; halfTone : THalfTone) : integer;
var
   scale : TScale;
   index : integer;
   ht : integer;
begin
   result := -1;
   ht := Ord(parentScaleRoot);

   scale := TScale.Create;
   try
      globalScaleRepository.GetScale(scale, scaleName);
      for index := 0 to scale.count do
         begin
            if (ht mod 12) = Ord(halfTone)
               then begin
                       result := (index + 1) mod (scale.count + 2);
                       Break;
                    end
               else Inc(ht, scale.degreeInterval[index]);
         end;
   finally
      scale.Free;
   end;
end;

function GetScaleHalftones(scaleName : string; scaleRoot : THalfTone) : THalfTones;
var
   scale : TScale;
   index : integer;
   ht : integer;
begin
   result := [];
   scale := TScale.Create;
   try
      if scale.GetFromRepository(scaleName)
         then begin
                 ht := Ord(scaleRoot);
                 for index := 0 to scale.count do
                    begin
                       Include(result, THalfTone(ht mod 12));
                       ht := ht + scale.degreeInterval[index];
                    end;
              end;
   finally
   end;
end;

function ComputeScaleModeRoot(scaleName : string; scaleMode : integer; scaleRoot : THalfTone) : THalfTone;
var
   modeIntervals : shortint;
   scale : TScale;
   index : integer;
   modesAllowed : boolean;
begin
   // Sum up the intervals between each degree for the mode
   modeIntervals := 0;
   scale := TScale.Create;
   try
      globalScaleRepository.GetScale(scale, scaleName, modesAllowed);
      for index := 0 to scaleMode - 2 do
         Inc(modeIntervals, scale.degreeInterval[index]);
   finally
      scale.Free;
   end;

   // Substract the mode intervals from the scale root
   result := THalfTone( (Ord(scaleRoot) + 12 + modeIntervals) mod 12 );
end;

function IsAlteration(quality : THalfToneQuality) : boolean;
begin
   result := (quality in [htqMinorSecond, htqDiminishedFifth, htqAugmentedFifth, htqMinorNinth, htqAugmentedNinth, htqAugmentedEleventh, htqMinorThirteenth]);
end;

function GetScaleKeyPreference(scaleName : string) : TKeyPreference;
var
   scale : TScale;
   modesAllowed : boolean;   
begin
   scale := TScale.Create;
   try
      if globalScaleRepository.GetScale(scale, scaleName, modesAllowed)
         then result := scale.keyPreference
         else result := kpNone;
   finally
      scale.Free;
   end;
end;

function CompareChordTemplates(list : TStringList; index1, index2 : integer) : integer;
var
   chordTemplate1, chordTemplate2 : TChordTemplate;

   function KindSort : integer;
   begin
           if chordTemplate1.kind > chordTemplate2.kind
              then result := 1
      else if chordTemplate1.kind < chordTemplate2.kind
              then result := -1
      else         result := 0;
   end;

   function ThirdKindSort : integer;
   begin
           if chordTemplate1.thirdKind > chordTemplate2.thirdKind
               then result := 1
      else if chordTemplate1.thirdKind < chordTemplate2.thirdKind
               then result := -1
      else          result := 0;
   end;

   function FifthKindSort : integer;
   begin
           if chordTemplate1.fifthKind > chordTemplate2.fifthKind
               then result := 1
      else if chordTemplate1.fifthKind < chordTemplate2.fifthKind
               then result := -1
      else          result := 0;
   end;

   function SixthSort : integer;
   begin
           if chordTemplate1.containsSixth > chordTemplate2.containsSixth
               then result := 1
      else if chordTemplate1.containsSixth < chordTemplate2.containsSixth
               then result := -1
      else          result := 0;
   end;

   function SeventhKindSort : integer;
   begin
           if chordTemplate1.seventhKind > chordTemplate2.seventhKind
               then result := 1
      else if chordTemplate1.seventhKind < chordTemplate2.seventhKind
               then result := -1
      else          result := 0;
   end;

   function SeventhExtensionSort : integer;
   begin
           if chordTemplate1.seventhExtensionKind > chordTemplate2.seventhExtensionKind
               then result := 1
      else if chordTemplate1.seventhExtensionKind < chordTemplate2.seventhExtensionKind
               then result := -1
      else          result := 0;
   end;


   function QualityCountSort : integer;
   begin
           if chordTemplate1.qualityCount > chordTemplate2.qualityCount
              then result := 1
      else if chordTemplate1.qualityCount < chordTemplate2.qualityCount
              then result := -1
      else         result := 0;
   end;

   function HighestQualitySort : integer;
   begin
           if chordTemplate1.highestQuality > chordTemplate2.highestQuality
              then result := 1
      else if chordTemplate1.highestQuality < chordTemplate2.highestQuality
              then result := -1
      else         result := 0;
   end;

   function StackedThirdsSort : integer;
   begin
           if chordTemplate1.containsOnlyStackedThirds < chordTemplate2.containsOnlyStackedThirds
              then result := 1
      else if chordTemplate1.containsOnlyStackedThirds > chordTemplate2.containsOnlyStackedThirds
              then result := -1
      else         result := 0;
   end;

   function AlterationsSort : integer;
   begin
           if chordTemplate1.alterations > chordTemplate2.alterations
              then result := 1
      else if chordTemplate1.alterations < chordTemplate2.alterations
              then result := -1
      else         result := 0;
   end;


begin
   // Inits
   Assert(list.objects[index1] is TChordTemplate);
   Assert(list.objects[index2] is TChordTemplate);
   chordTemplate1 := TChordTemplate(list.objects[index1]);
   chordTemplate2 := TChordTemplate(list.objects[index2]);

   // Sort
   result := StackedThirdsSort;
   if result <> 0
      then Exit;

   result := KindSort;
   if result <> 0
      then Exit;

   if (chordTemplate1.containsOnlyStackedThirds and chordTemplate1.containsOnlyStackedThirds)
      then begin
              result := HighestQualitySort;
              if result <> 0
                 then Exit;
           end;

   result := ThirdKindSort;
   if result <> 0
      then Exit;

   result := FifthKindSort;
   if result <> 0
      then Exit;

   result := SixthSort;
   if result <> 0
      then Exit;

   result := SeventhKindSort;
   if result <> 0
      then Exit;

   result := AlterationsSort;
   if result <> 0
      then Exit;

   result := QualityCountSort;
   if result <> 0
      then Exit;

   result := HighestQualitySort;
   if result <> 0
      then Exit;
end;


function CompareChordTemplatesEx(list : TStringList; index1, index2 : integer) : integer;
var
   chordTemplate1, chordTemplate2 : TChordTemplate;

   function KindSort : integer;
   begin
           if chordTemplate1.kind > chordTemplate2.kind
              then result := 1
      else if chordTemplate1.kind < chordTemplate2.kind
              then result := -1
      else         result := 0;
   end;

   function ThirdSort : integer;
   begin
           if chordTemplate1.thirdKind < chordTemplate2.thirdKind
               then result := 1
      else if chordTemplate1.thirdKind > chordTemplate2.thirdKind
               then result := -1
      else          result := 0;
   end;

   function SeventhKindSort : integer;
   begin
           if chordTemplate1.seventhKind > chordTemplate2.seventhKind
               then result := 1
      else if chordTemplate1.seventhKind < chordTemplate2.seventhKind
               then result := -1
      else          result := 0;
   end;

   function SeventhExtensionSort : integer;
   begin
           if chordTemplate1.seventhExtensionKind > chordTemplate2.seventhExtensionKind
               then result := 1
      else if chordTemplate1.seventhExtensionKind < chordTemplate2.seventhExtensionKind
               then result := -1
      else          result := 0;
   end;

   function FifthSort : integer;
   begin
           if chordTemplate1.fifthKind > chordTemplate2.fifthKind
               then result := 1
      else if chordTemplate1.fifthKind < chordTemplate2.fifthKind
               then result := -1
      else          result := 0;
   end;

   function SixthSort : integer;
   begin
           if chordTemplate1.containsSixth > chordTemplate2.containsSixth
               then result := 1
      else if chordTemplate1.containsSixth < chordTemplate2.containsSixth
               then result := -1
      else          result := 0;
   end;

   function NinthSort : integer;
   begin
           if chordTemplate1.fNinthKind > chordTemplate2.fNinthKind
               then result := 1
      else if chordTemplate1.fNinthKind < chordTemplate2.fNinthKind
               then result := -1
      else          result := 0;
   end;

   function QualityCountSort : integer;
   begin
           if chordTemplate1.qualityCount > chordTemplate2.qualityCount
              then result := 1
      else if chordTemplate1.qualityCount < chordTemplate2.qualityCount
              then result := -1
      else         result := 0;
   end;

   function HighestQualitySort : integer;
   begin
           if chordTemplate1.highestQuality > chordTemplate2.highestQuality
              then result := 1
      else if chordTemplate1.highestQuality < chordTemplate2.highestQuality
              then result := -1
      else         result := 0;
   end;

   function StackedThirdsSort : integer;
   begin
           if chordTemplate1.containsOnlyStackedThirds < chordTemplate2.containsOnlyStackedThirds
              then result := 1
      else if chordTemplate1.containsOnlyStackedThirds > chordTemplate2.containsOnlyStackedThirds
              then result := -1
      else         result := 0;
   end;

   function AlterationsSort : integer;
   begin
           if chordTemplate1.alterations > chordTemplate2.alterations
              then result := 1
      else if chordTemplate1.alterations < chordTemplate2.alterations
              then result := -1
      else         result := 0;
   end;


begin
   // Inits
   Assert(list.objects[index1] is TChordTemplate);
   Assert(list.objects[index2] is TChordTemplate);
   chordTemplate1 := TChordTemplate(list.objects[index1]);
   chordTemplate2 := TChordTemplate(list.objects[index2]);

   // Sort
   result := KindSort;
   if result <> 0
      then Exit;

   result := StackedThirdsSort;
   if result <> 0
      then Exit;

   if (chordTemplate1.containsOnlyStackedThirds and chordTemplate1.containsOnlyStackedThirds)
      then begin
              result := FifthSort;
              if result <> 0
                 then Exit;

              result := SeventhKindSort;
              if result <> 0
                 then Exit;

              result := SixthSort;
              if result <> 0
                 then Exit;

              result := HighestQualitySort;
              if result <> 0
                 then Exit;
           end;

   result := FifthSort;
   if result <> 0
      then Exit;

   result := SixthSort;
   if result <> 0
      then Exit;

   result := SeventhKindSort;
   if result <> 0
      then Exit;

   result := SeventhExtensionSort;
   if result <> 0
      then Exit;

   result := HighestQualitySort;
   if result <> 0
      then Exit;

   result := NinthSort;
   if result <> 0
      then Exit;

   result := ThirdSort;
   if result <> 0
      then Exit;

   result := AlterationsSort;
   if result <> 0
      then Exit;
end;


{ TScaleRepository }

constructor TScaleRepository.Create;
begin
   inherited Create;
   fScales := TStringList.Create;
end;

destructor TScaleRepository.Destroy;
begin
   while fScales.count > 0 do
      begin
         Assert(fScales.objects[0] <> nil);      
         fScales.objects[0].Free;
         fScales.Delete(0);
      end;

   fScales.Free;
   inherited;
end;

procedure TScaleRepository.GetNames(names : TStrings; suffix : string = '');
var
   index : integer;
begin
   if names <> nil
      then begin
              names.BeginUpdate;
              try
                 names.Clear;
                 for index := 0 to fScales.count - 1 do
                    begin
                       Assert(fScales.objects[index] is TScaleRepositoryItem);
                       if suffix = ''
                          then names.Add(fScales[index])
                          else names.Add(fScales[index] + ' ' + suffix);
                    end;
              finally
                 names.EndUpdate;
              end;
           end;
end;

procedure TScaleRepository.GetModes(scaleName : string; names : TStrings; numberPrefix : boolean; suffix : string);
var
   scale : TScale;
   index : integer;
   modeName : string;
   modesAllowed : boolean;
begin
   scale := TScale.Create;
   try
      if Assigned(names)
         and GetScale(scale, scaleName, modesAllowed)
         and modesAllowed
         then begin
                 if scale.fModeNames.count <> 0
                    then // Get mode names
                         begin
                            names.BeginUpdate;
                            try
                               names.Assign(scale.fModeNames);
                               for index := 0 to names.count - 1 do
                               begin
                                  modeName := LowerCase(names[index]);
{$WARNINGS OFF}
                                  if modeName <> ''
                                     then modeName[1] := UpCase(modeName[1]);
{$WARNINGS ON}
                                  if numberPrefix
                                     then modeName := IntToStr(index + 1) + ' - ' + modeName;
                                  if suffix <> ''
                                     then modeName := modeName + ' ' + suffix;
                                  names[index] := modeName;
                               end;
                            finally
                               names.EndUpdate;
                            end;
                         end
                    else // Generate mode names
                         begin
                         names.BeginUpdate;
                         try
                            names.Clear;
                            for index := 1 to scale.count + 1 do
                            names.Add('Mode ' + IntToStr(index));
                         finally
                            names.EndUpdate;
                         end;
                  end;
              end;

      finally
         scale.Free;
   end;
end;

function TScaleRepository.RegisterScale(scaleName : string;
                                        scaleIntervals : array of smallint;
                                        scaleModeNames : array of string;
                                        allowModes, visible : boolean;
                                        keyPreference : TKeyPreference) : boolean;
var
   item : TScaleRepositoryItem;
begin
   result := fScales.IndexOf(scaleName) = -1;
   if result
      then begin
              item := TScaleRepositoryItem.Create(scaleIntervals, scaleModeNames, allowModes, visible, keyPreference);
              fScales.AddObject(scaleName, item);
           end;
end;

function TScaleRepository.RegisterScale(scaleName : string;
                                        scaleIntervals : array of smallint;
                                        allowModes, visible : boolean;
                                        keyPreference : TKeyPreference) : boolean;
begin
   result := RegisterScale(scaleName, scaleIntervals, [], allowModes, visible, keyPreference);
end;

function TScaleRepository.UnregisterScale(scaleName : string) : boolean;
var
   index : integer;
begin
   index := fScales.IndexOf(scaleName);
   result := index <> -1;
   if result
      then begin
              Assert(fScales.objects[index] <> nil);
              fScales.objects[index].Free;
              fScales.Delete(index);
           end;
end;

function TScaleRepository.GetScale(scale : TScale; scaleName : string; out modesAllowed : boolean; mode : integer) : boolean;
var
   index : integer;
   item : TScaleRepositoryItem;
begin
   index := fScales.IndexOf(scaleName);
   result := index <> -1;
   if result
      then begin
              Assert(fScales.objects[index] is TScaleRepositoryItem);
              item := TScaleRepositoryItem(fScales.objects[index]);
              scale.SetScale(item.fScaleIntervals, mode);
              scale.fName := scaleName;
              scale.fKeyPreference := item.fKeyPreference;
              Assert(item.fScaleModes <> nil);
              scale.fModeNames.Assign(item.fScaleModes);
              scale.fMode := mode;
              scale.fModesAllowed := item.fAllowModes;
              scale.ComputeDegreeQualities;
              modesAllowed := scale.fModesAllowed;
           end;
end;

function TScaleRepository.GetScale(scale : TScale; scaleName : string; mode : integer = 1) : boolean; 
var
   modesAllowed : boolean;
begin
   result := GetScale(scale, scaleName, modesAllowed, mode);
end;

function TScaleRepository.AreModeAllowed(scaleName : string) : boolean;
var
   index : integer;
   item : TScaleRepositoryItem;
begin
   result := false;

   index := fScales.IndexOf(scaleName);
   if index <> -1
      then begin
              Assert(fScales.objects[index] is TScaleRepositoryItem);
              item := TScaleRepositoryItem(fScales.objects[index]);
              result := item.fAllowModes;
           end;
end;

function TScaleRepository.IsScaleVisible(scaleName : string) : boolean;
var
   index : integer;
   item : TScaleRepositoryItem;
begin
   result := false;

   index := fScales.IndexOf(scaleName);
   if index <> -1
      then begin
              Assert(fScales.objects[index] is TScaleRepositoryItem);
              item := TScaleRepositoryItem(fScales.objects[index]);
              result := item.fVisible;
           end;
end;

{ TScale }

constructor TScale.Create;
begin
   inherited Create;
   fModeNames := TStringList.Create;
   SetLength(fIntervals, 0);
   SetLength(fDegreeQualities, 0);
   SetLength(fDegreeAccidentals, 0);
   SetLength(fDegreeCharacterTones, 0);
   SetLength(fDegreeRefQualities, 0);
   fMatchingChords := TStringList.Create;
   fName := '';
   fMode := 0;
   fMinor := false;
   fStackedThirds := [];
   fStackedFourths := [];
   fAllowIncrementalMatchingChordList := false;
   fIncrementalMatchingChordListDone := false;
   TStringList(fMatchingChords).duplicates := dupIgnore;
   TStringList(fMatchingChords).sorted := true;
end;

destructor TScale.Destroy;
begin
   fModeNames.Free;
   SetLength(fIntervals, 0);
   SetLength(fDegreeQualities, 0);
   SetLength(fDegreeAccidentals, 0);
   SetLength(fDegreeCharacterTones, 0);
   SetLength(fDegreeRefQualities, 0);
   ClearMatchingChords;
   fMatchingChords.Free;
   inherited;
end;

procedure TScale.SetScale(scaleIntervals : array of smallint; mode : integer);
var
   index : integer;
begin
        if mode < 1
           then mode := 1
   else if mode > Length(scaleIntervals) + 1
           then mode := Length(scaleIntervals) + 1;
   SetLength(fIntervals, Length(scaleIntervals));
   for index := 0 to High(scaleIntervals) do
      fIntervals[index] := scaleIntervals[(index + mode - 1) mod (Length(scaleIntervals))];
   ComputeDegreeQualities;
end;

function TScale.GetFromRepository(scaleName : string; mode : integer = 1) : boolean;
var
   modesAllowed : boolean;
begin
   result := globalScaleRepository.GetScale(self, scaleName, modesAllowed, mode);
end;

function TScale.GetIntervalBetweenDegrees(fromDegree, toDegree : integer) : integer;
var
   index : integer;
begin
   result := 0;
        if toDegree > fromDegree
           then // Positive interval
                for index := fromDegree to toDegree - 1 do
                   result := result + degreeInterval[index]
   else if toDegree < fromDegree
           then // Negative interval
                for index := toDegree downto fromDegree - 1 do
                   result := result + degreeInterval[index];
end;

function TScale.GetScaleName(key : TKey) : string;
begin
   if fMinor
      then result := KEY_NAME_MINOR[key] + ' ' + fName
      else result := KEY_NAME_MAJOR[key] + ' ' + fName;
end;

function TScale.GetScaleName : string;
begin
   result := fName;
end;

function TScale.GetModeName : string;
begin
   if (fMode >= 0) and (fMode <= fModeNames.count)
      then result := fModeNames[fMode - 1]
      else result := '';
end;

function TScale.GetDegreeHalfTone(key : TKey; degree : integer) : THalfTone;
var
   keyRoot : THalfTone;
   quality : THalfToneQuality;
begin
   keyRoot := GetKeyRoot(key, false);
   quality := GetDegreeQuality(degree);
   result := THalfTone((Ord(keyRoot) + Ord(quality)) mod 12);
end;

function TScale.Count : integer;
begin
   result := High(fIntervals);
end;

function TScale.ContainsQualities(qualities : THalfToneQualities) : boolean;
begin
   result := fQualities >= qualities;
end;

function TScale.GetDegreeInterval(index : integer) : integer;
begin
   index := (index + (Length(fDegreeQualities) - 1)) mod (Length(fDegreeQualities) - 1);
   if (index >= 0) and (index <= High(fIntervals))
      then result := fIntervals[index]
      else result := 0;
end;

procedure TScale.ComputeDegreeQualities;
var
   index : integer;
   degreeQuality : THalfToneQuality;
   referenceQuality : THalfToneQuality;
   toneAccidental : TNoteAccidental;

   procedure ComputeDegreeToneAndAccidental(degreeQuality : THalfToneQuality;
                                            out referenceQuality : THalfToneQuality; out toneAccidental : TNoteAccidental);
   const
      MAJOR_SCALE_DEGREES : array[TToneQuality] of THalfToneQuality =
      (htqUnison, htqMajorSecond, htqMajorThird, htqPerfectFourth, htqPerfectFifth, htqMajorSixth, htqMajorSeventh,
       htqOctave, htqUnison, htqUnison, htqUnison, htqUnison, htqUnison);
      MINOR_SCALE_DEGREES : array[TToneQuality] of THalfToneQuality =
      (htqUnison, htqMajorSecond, htqMinorThird, htqPerfectFourth, htqPerfectFifth, htqAugmentedFifth, htqMinorSeventh,
       htqOctave, htqUnison, htqUnison, htqUnison, htqUnison, htqUnison);
   var
      degreeToneQuality : TToneQuality;
   begin
      degreeToneQuality := HalfToneQualityToToneQuality(degreeQuality);
      if fMinor
         then referenceQuality := MINOR_SCALE_DEGREES[degreeToneQuality]
         else referenceQuality := MAJOR_SCALE_DEGREES[degreeToneQuality];
      toneAccidental := TNoteAccidental(Ord(naNatural) + Ord(degreeQuality) - Ord(referenceQuality));
   end;

begin
   fMinor := false;
   SetLength(fDegreeQualities, Length(fIntervals) + 1);
   SetLength(fDegreeAccidentals, Length(fIntervals) + 1);
   SetLength(fDegreeCharacterTones, Length(fIntervals) + 1);
   SetLength(fDegreeRefQualities, Length(fIntervals) + 1);
   fStackedThirds := [];
   fStackedFourths := [];
   fQualities := [];

   // Maybe clear matching chords
   if (fMatchingChords.count > 0) and (not fAllowIncrementalMatchingChordList)
      then ClearMatchingChords;

   for index := 0 to Length(fIntervals) - 1 do
      begin
         degreeQuality := THalfToneQuality(GetIntervalBetweenDegrees(0, index));
         fDegreeQualities[index] := degreeQuality;
         fQualities := fQualities + [degreeQuality];
      end;

   for index := 0 to Length(fDegreeAccidentals) - 1 do
      begin
         degreeQuality := fDegreeQualities[index];
         ComputeDegreeToneAndAccidental(degreeQuality, referenceQuality, toneAccidental);
         fDegreeRefQualities[index] := referenceQuality;
         fDegreeAccidentals[index] := toneAccidental;

              if toneAccidental <> naNatural
                 then fDegreeCharacterTones[index] := ctPrimary
         else if degreeQuality in [htqMinorThird, htqMajorThird, htqMinorSeventh, htqMajorSeventh]
                 then fDegreeCharacterTones[index] := ctSecondary
         else         fDegreeCharacterTones[index] := ctNone;
      end;
end;

function TScale.GetDegreeQuality(index : integer) : THalfToneQuality;
begin
   index := (index + (Length(fDegreeQualities) - 1)) mod (Length(fDegreeQualities) - 1);
   if (index >= 0) and (index <= High(fDegreeQualities))
      then result := fDegreeQualities[index]
      else result := htqUnison;
end;

function TScale.GetStackedThirds : THalfToneQualities;
var
   quality : integer;
   degreeIndex : integer;
begin
   if fStackedThirds = []
      then // Compute stacked thirds if needed
           begin
              result := [htqUnison];
              quality := 0;
              degreeIndex := 0;

              repeat
                 quality := quality + GetIntervalBetweenDegrees(degreeIndex, degreeIndex + 2);
                 Inc(degreeIndex, 2);
                 if quality <= Ord(High(THalfToneQuality))
                    then result := result + [THalfToneQuality(quality)];
              until quality > Ord(High(THalfToneQuality));

              fStackedThirds := result;
           end
      else result := fStackedThirds;
end;

function TScale.GetStackedFourths : THalfToneQualities;
var
   quality : integer;
   degreeIndex : integer;
begin
   if fStackedFourths = []
      then // Compute stacked thirds if needed
           begin
              result := [htqUnison];
              quality := 0;
              degreeIndex := 0;

              repeat
                 quality := quality + GetIntervalBetweenDegrees(degreeIndex, degreeIndex + 3);
                 Inc(degreeIndex, 2);
                 if quality <= Ord(High(THalfToneQuality))
                    then result := result + [THalfToneQuality(quality)];
              until quality > Ord(High(THalfToneQuality));

              fStackedFourths := result;
           end
      else result := fStackedFourths;
end;

function TScale.GetMatchingChords : TStrings;
begin
   result := GetMatchingChordsEx([], ckNone);
end;

function TScale.GetMatchingChordsEx(requiredQualities : THalfToneQualities; chordKindFilter : TChordKind) : TStrings;
var
   index, i1, i2, chord : integer;
   quality : THalfToneQuality;
   containsAlterations : boolean;
   scaleQualities : THalfToneQualityArray;
   permittedQualities, initialPermittedQualities : THalfToneQualities;
   selectedQualities : THalfToneQualities;
   scaleDegree : THalfToneQuality;
   noteCount : integer;
   chordQualityNames : string;
   chordTemplate : TChordTemplate;

   function IsChordValid : boolean;
   begin
      result := false;
      if noteCount = 2
         then // Power chords
              result := htqPerfectFifth in selectedQualities
         else // Normal chords
              begin
                 // The chord must have 2 or 3 or 4
                 if not (
                    (htqMajorSecond in selectedQualities)
                    or (htqMinorThird in selectedQualities)
                    or (htqMajorThird in selectedQualities)
                    or (htqPerfectFourth in selectedQualities)
                 )  then Exit;

                 // The chord must have a b5, 5 or #5
                 if not (
                    (htqDiminishedFifth in selectedQualities)
                    or (htqPerfectFifth in selectedQualities)
                    or (htqAugmentedFifth in selectedQualities)
                 )  then Exit;

                 // Only Dom7 or Min chords can have 4 or 11 (dissonant with Maj/Maj7 chords)
                 if (htqMajorThird in selectedQualities) and not(htqMinorSeventh in selectedQualities)
                    and ((htqPerfectFourth in selectedQualities) or (htqPerfectEleventh in selectedQualities))
                    then Exit;

                 // half bb7, bb7, b7 or 7 needed when the chord contains b9, #9, #11, b13 alterations or contains a 13
                 if (containsAlterations or (htqMajorThirteenth in selectedQualities))
                    and not(
                       (
                           (htqMinorSeventh in selectedQualities) // b7 or half bb7
                           or (htqMajorSeventh in selectedQualities) // 7
                           or ((htqMajorSixth in selectedQualities) and (htqDiminishedFifth in selectedQualities)) // hb7
                       )
                    )
                    then Exit;

                 // b9 or #9 valid only if there's no 9 already
                 if ((htqMinorNinth in selectedQualities) or (htqAugmentedNinth in selectedQualities))
                    and (htqMajorNinth in selectedQualities)
                    then Exit;

                 // b2 and b9 -> repetition
                 if ((htqMinorSecond in selectedQualities) and (htqMinorNinth in selectedQualities))
                    then Exit;

                 // b3 and #9 -> repetition
                 if ((htqMinorThird in selectedQualities) and (htqAugmentedNinth in selectedQualities))
                    then Exit;

                 // Valid chord
                 result := true;
              end;

      // Eliminate chords that don't contain required qualities
      if result and (requiredQualities <> []) and (selectedQualities * requiredQualities <> requiredQualities)
         then result := false;
   end;

begin
   result := fMatchingChords;

   // Exit if cached result
   if ((not fAllowIncrementalMatchingChordList) and (fMatchingChords.count > 0))
      or fIncrementalMatchingChordListDone
      then Exit;

   // Compute initial permitted qualities
   initialPermittedQualities := [];
   for index := 0 to High(fDegreeQualities) do
      begin
         scaleDegree := fDegreeQualities[index];

         // Add scale degree up to octave (Exclude htqUnison)
         if (scaleDegree <> htqUnison) and (scaleDegree < htqOctave)
            then Include(initialPermittedQualities, scaleDegree);

         // Add corresponding extensions
         case scaleDegree of
            htqMinorSecond: // b2 -> b9, remove b2
               begin
                  Include(initialPermittedQualities, htqMinorNinth);
                  Exclude(initialPermittedQualities, htqMinorSecond);
               end;
            htqMajorSecond: // 2 -> 9
               Include(initialPermittedQualities, htqMajorNinth);
            htqMinorThird: // b3 -> #9
               Include(initialPermittedQualities, htqAugmentedNinth);
            htqPerfectFourth: // 4 -> 11
               Include(initialPermittedQualities, htqPerfectEleventh);
            htqDiminishedFifth: // (b5) #4 -> #11
               Include(initialPermittedQualities, htqAugmentedEleventh);
            htqAugmentedFifth: // (b6) #5 -> b13
               Include(initialPermittedQualities, htqMinorThirteenth);
            htqMajorSixth: // 6 -> 13
               Include(initialPermittedQualities, htqMajorThirteenth);
         end;
      end;

   // Compute ordered scale degrees
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         if THalfToneQuality(index) in initialPermittedQualities
            then begin
                    SetLength(scaleQualities, Length(scaleQualities) + 1);
                    scaleQualities[Length(scaleQualities) - 1] := THalfToneQuality(index);
                 end;
         end;

   // Compute matching chords
   for i1 := 1 to (1 shl Length(scaleQualities) - 1) do
      begin
         // Init
         selectedQualities := [htqUnison];
         containsAlterations := false;
         chord := i1;
         noteCount := 1;

         // Init permitted qualities
         permittedQualities := initialPermittedQualities;

         // Chord loop
         for i2 := 0 to Length(scaleQualities) do
            begin
               if (chord and 1) = 1
                  then begin
                          quality := scaleQualities[i2];
                          if quality in permittedQualities
                             then begin
                                     selectedQualities := selectedQualities + [quality];
                                     Inc(noteCount);

                                     // Check for alterations
                                     if not containsAlterations
                                        and (quality in [htqMinorNinth, htqAugmentedNinth, htqAugmentedEleventh, htqMinorThirteenth])
                                        then containsAlterations := true;


                                     case quality of
                                        htqMajorSecond : // 2 -> no b3, 3, 4 or 9
                                           permittedQualities :=
                                              permittedQualities - [htqMinorThird, htqMajorThird, htqPerfectFourth, htqMajorNinth];
                                        htqMinorThird : // b3 -> no 3 or 4
                                           permittedQualities :=
                                              permittedQualities - [htqMajorThird, htqPerfectFourth];
                                        htqMajorThird: // 3 -> no 4
                                           begin
                                              permittedQualities :=
                                                 permittedQualities - [htqPerfectFourth];
                                           end;
                                        htqPerfectFourth: // 4 -> no b5 or #5 and no 11
                                           permittedQualities :=
                                              permittedQualities - [htqDiminishedFifth, htqAugmentedFifth, htqPerfectEleventh];
                                        htqDiminishedFifth: // b5 (#4) -> no 5, #5 or #11
                                           permittedQualities :=
                                              permittedQualities - [htqPerfectFifth, htqAugmentedFifth, htqAugmentedEleventh];
                                        htqPerfectFifth: // 5 -> no #5
                                           permittedQualities :=
                                              permittedQualities - [htqAugmentedFifth];
                                        htqAugmentedFifth: // #5 -> no b13
                                           permittedQualities :=
                                              permittedQualities - [htqMinorThirteenth];
                                        htqMajorSixth: // 6 -> no 13
                                           permittedQualities :=
                                              permittedQualities - [htqMajorThirteenth];
                                        htqPerfectEleventh: // 11 -> no #11
                                           permittedQualities :=
                                              permittedQualities - [htqAugmentedEleventh];
                                        htqMinorThirteenth: // b13 -> no 13
                                           permittedQualities :=
                                              permittedQualities - [htqMajorThirteenth];
                                    end;
                                 end;
                       end;
               chord := chord shr 1;
            end;

      if IsChordValid
            then begin
                    TChordTemplate.FixupQualities(selectedQualities);
                    chordQualityNames := TChordTemplate.GetQualityNames(selectedQualities);

                    index := fMatchingChords.Add(chordQualityNames);
                    if fMatchingChords.objects[index] = nil
                       then // New chord
                            begin
                               chordTemplate := TChordTemplate.Create(selectedQualities, IsHalfToneQualityOrderedSubsetOf(selectedQualities, stackedThirds));
                               fMatchingChords.objects[index] := chordTemplate;

                               // Eliminate chords that don't match the chord kind filter
                               if (chordKindFilter <> ckNone) and (chordTemplate.kind <> chordKindFilter)
                                  then begin
                                          fMatchingChords.Delete(index);
                                          chordTemplate.Free;
                                       end;
                            end;
                 end;
      end;

   // Sort the list of chords
   if not fAllowIncrementalMatchingChordList
      then begin
              TStringList(fMatchingChords).sorted := false;
              TStringList(fMatchingChords).CustomSort(CompareChordTemplates);
              for index := 0 to fMatchingChords.count - 1 do
                 fMatchingChords[index] := IntToHex(index, 4);
              TStringList(fMatchingChords).sorted := true;
           end;
end;

procedure TScale.SortIncrementalMatchingChordList;
var
   index : integer;
begin
   TStringList(fMatchingChords).sorted := false;
   TStringList(fMatchingChords).CustomSort(CompareChordTemplatesEx);
//   TStringList(fMatchingChords).CustomSort(CompareChordTemplates);
   for index := 0 to fMatchingChords.count - 1 do
       fMatchingChords[index] := IntToHex(index, 4);
   TStringList(fMatchingChords).sorted := true;
   fIncrementalMatchingChordListDone := true;
end;

procedure TScale.SetAllowIncrementalMatchingChordList(value : boolean);
begin
   if value <> fAllowIncrementalMatchingChordList
      then begin
              fAllowIncrementalMatchingChordList := value;
              if value
                 then fIncrementalMatchingChordListDone := false;
           end;
end;

procedure TScale.ClearMatchingChords;
var
   index : integer;
begin
   for index := 0 to fMatchingChords.count - 1 do
      begin
         Assert(fMatchingChords.objects[index] is TChordTemplate);
         TChordTemplate(fMatchingChords.objects[index]).Free;
      end;
   fMatchingChords.Clear;
   fIncrementalMatchingChordListDone := false;
end;

{ TScaleRepositoryItem }

constructor TScaleRepositoryItem.Create(aScaleIntervals : array of smallint; aScaleModes : array of string;
                                        allowModes, visible : boolean; keyPreference : TKeyPreference);
var
   index : integer;
begin
   inherited Create;
   SetLength(fScaleIntervals, Length(aScaleIntervals));
   fScaleModes := TStringList.Create;
   for index := 0 to High(aScaleIntervals) do
      fScaleIntervals[index] := aScaleIntervals[index];
   for index := 0 to High(aScaleModes) do
      fScaleModes.Add(aScaleModes[index]);
   fAllowModes := allowModes;
   fVisible := visible;
   fKeyPreference := keyPreference;
end;

destructor TScaleRepositoryItem.Destroy;
begin
   SetLength(fScaleIntervals, 0);
   fScaleModes.Free;
   inherited;
end;

{ TChordTemplate }

constructor TChordTemplate.Create(qualities : THalfToneQualities; containsOnlyStackedThirds : boolean);
var
   index : integer;
   quality : THalfToneQuality;
   dimFifth, augFifth : boolean;
   minorSeventh, majorSeventh : boolean;
begin
   inherited Create;
   FixupQualities(qualities);

   // Set chord degree qualities
   fQualities := qualities;
   Include(fQualities, htqUnison);
   fQualityCount := 0;
   SetLength(fChordDegrees, 0);
   fChordDegreeCount := 0;
   fContainsOnlyStackedThirds := containsOnlyStackedThirds;

   // Compute chord degrees and highest quality
   fHighestQuality := htqUnison;
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         quality := THalfToneQuality(index);
         if quality in fQualities
            then begin
                    Inc(fQualityCount);
                    fHighestQuality := quality;
                    SetLength(fChordDegrees, Length(fChordDegrees) + 1);
                    fChordDegrees[Length(fChordDegrees) - 1] := quality;
                 end;
      end;
   fChordDegreeCount := Length(fChordDegrees);

   // Compute kind and third kind
   dimFifth := htqDiminishedFifth in fQualities;
   augFifth := htqAugmentedFifth in fQualities;
   minorSeventh := htqMinorSeventh in fQualities;
   majorSeventh := htqMajorSeventh in fQualities;
   fDimSeventh := (htqMinorThird in fQualities) // b3
                  and (htqDiminishedFifth in fQualities) // b5
                  and (
                         ((htqMajorSixth in fQualities) and not(minorSeventh or majorSeventh)) // bb7 enharmonic with 6
                      );
   fHalfDimSeventh := (htqMinorThird in fQualities) // b3
                      and (htqDiminishedFifth in fQualities) // b5
                      and (htqMinorSeventh in fQualities);
   fDimOrHalfDimSeventh := fDimSeventh or fHalfDimSeventh;
   fContainsSeventh := minorSeventh or majorSeventh or (fDimOrHalfDimSeventh and (htqMajorSixth in fQualities));
   fContainsNinth := [htqMinorNinth, htqMajorNinth, htqAugmentedNinth] <= fQualities;
   fContainsEleventh := [htqPerfectEleventh, htqAugmentedEleventh] <= fQualities;

   fContainsSixth := (htqMajorSixth in fQualities) and (not fDimSeventh);

        if ((fQualityCount = 2) and (htqPerfectFifth in fQualities))
           then begin
                   fThirdKind := ckPower;
                   fKind := ckSpecial;
                end
   else if htqMinorThird in fQualities
           then // Minor or dominant chord
                begin
                   if fDimOrHalfDimSeventh
                      then // Dominant
                           fKind := ckDominant
                      else // Minor
                           fKind := ckMinor;
                   fThirdKind := ckMinorThird;
                end
   else if htqMajorThird in fQualities
           then // Major or dominant chord
                begin
                   if minorSeventh
                      or (augFifth and (fQualityCount = 3))
                      then // Dominant
                           fKind := ckDominant
                      else // Major
                           fKind := ckMajor;
                   fThirdKind := ckMajorThird;
                end
   else if htqPerfectFourth in fQualities
           then // Sus 4 chord (dominant)
                begin
                   fKind := ckDominant;
                   fThirdKind := ckSus4;
                end
   else if (htqMajorSecond in fQualities)
           then // Sus 2 
                begin
                   fKind := ckSpecial;
                   fThirdKind := ckSus2;
                end
   else         Assert(false, 'Algorithm error');

   // Compute fifth kind
        if dimFifth
           then fFifthKind := ckDiminishedFifth
   else if augFifth
           then fFifthKind := ckAugmentedFifth
   else         fFifthKind := ckPerfectFifth;

   // Compute ninth kind
        if htqMinorNinth in fQualities
           then fNinthKind := ckMinorNinth
   else if htqMajorNinth in fQualities
           then fNinthKind := ckMajorNinth
   else if htqAugmentedNinth in fQualities
           then fNinthKind := ckAumentedNinth
   else         fNinthKind := ckNoNinth;

   // Compute seventh
   fSeventhExtensionKind := cseNone;
   if fContainsSeventh
      then // The chord contains seventh extensions
           begin
              fSeventhExtensionKind := cseSeventh;
              if (htqMinorNinth in fQualities)
                 or (htqMajorNinth in fQualities)
                 or (htqAugmentedNinth in fQualities)
                 then // The chord contains a pure or altered 9th
                      begin
                         if htqMajorNinth in fQualities
                            then // pure 9th
                                 fSeventhExtensionKind := cseNinth;
                         if (htqPerfectEleventh in fQualities)
                            or (htqAugmentedEleventh in fQualities)
                            then // The chord contains a pure or altered 11th
                                 begin
                                    if htqPerfectEleventh in fQualities
                                       then // pure 11th
                                            fSeventhExtensionKind := cseEleventh;
                                    if htqMajorThirteenth in fQualities
                                       then // The chord contains a pure 13th
                                            fSeventhExtensionKind := cseThirteenth;
                                 end;
                      end
           end;

   // Compute seventh kind
        if fHalfDimSeventh
           then fSeventhKind := ckHalfDimSeventh
   else if fDimSeventh
           then fSeventhKind := ckDimSeventh
   else if minorSeventh
           then fSeventhKind := ckMinorSeventh
   else if majorSeventh
           then begin
                   if fThirdKind = ckMinorThird
                      then fSeventhKind := ckMinorMajorSeventh
                      else fSeventhKind := ckMajorSeventh;
                end
   else         fSeventhKind := ckNoSeventh;

   // Alterations
   fAlterations := 0;
   if (htqDiminishedFifth in fQualities)
      or (htqAugmentedFifth in fQualities)
      then Inc(fAlterations);
   if (htqMinorNinth in fQualities)
      or (htqAugmentedNinth in fQualities)
      then Inc(fAlterations);
   if (htqAugmentedEleventh in fQualities)
      then Inc(fAlterations);
   if (htqMinorThirteenth in fQualities)
      then Inc(fAlterations);

   // Compute chord name
   fChordName := ComputeChordName;
end;

destructor TChordTemplate.Destroy;
begin
   SetLength(fChordDegrees, 0);
   inherited;
end;

function TChordTemplate.GetChordDegree(index: integer) : THalfToneQuality;
begin
   if (index >= 0) and (index <= High(fChordDegrees))
      then result := fChordDegrees[index]
      else result := htqUnison;
end;

function TChordTemplate.ComputeChordName : string;
var
   qualities : THalfToneQualities;
   finished : boolean;
   sus4Flag : boolean;
   forceAdditions : boolean;
   alterations : string;
   alterationCount : integer;
begin
   qualities := fQualities;
   finished := false;
   result := '';
   sus4Flag :=  fThirdKind = ckSus4;

   case fThirdKind of
      ckPower: // Power chord
         begin
            result := FIVE_CHORD_CHAR;
            finished := true;
         end;
      ckMinorThird: // minor, dim, min/aug, dim7, half-dim7, min/aug7 chords
         begin
                 if fHighestQuality <= htqAugmentedFifth
                    then // 3 notes chords with a b3
                         case fFifthKind of
                            ckDiminishedFifth:
                               begin
                                  qualities := qualities - [htqDiminishedFifth];
                                  result := DIM_CHORD_CHAR; // Diminished chord
                                  finished := true;
                               end;
                            ckPerfectFifth:
                               begin
                                  result := MIN_CHORD_CHAR;
                               end;
                            ckAugmentedFifth:
                               begin
                                  qualities := qualities - [htqAugmentedFifth];
                                  result := MIN_CHORD_CHAR + SLASH_CHORD_CHAR + AUG_CHORD_CHAR; // Min/Aug chord
                                  finished := true;
                               end;
                         end
            else if fHighestQuality <= htqMajorSeventh
                    then // 4 notes chords with a b3
                         case fFifthKind of
                            ckDiminishedFifth:
                                    if htqMajorSixth in qualities
                                       then begin
                                               qualities := qualities - [htqDiminishedFifth];
                                               result := DIM_CHORD_CHAR + SEVEN_CHORD_CHAR; // Dim7 chord
                                               finished := true;
                                            end
                               else if htqMinorSeventh in qualities
                                       then begin
                                               qualities := qualities - [htqDiminishedFifth];
                                               result := HALF_DIM_CHORD_CHAR + SEVEN_CHORD_CHAR; // Half-dim7 chord
                                               finished := true;
                                            end;
                            ckPerfectFifth:
                               begin
                                  result := MIN_CHORD_CHAR;
                               end;
                            ckAugmentedFifth:
                               if htqMinorSeventh in qualities
                                  then      begin
                                               qualities := qualities - [htqAugmentedFifth];
                                               result := MIN_CHORD_CHAR + SLASH_CHORD_CHAR + AUG_CHORD_CHAR + SEVEN_CHORD_CHAR; // Min/Aug7 chord
                                               finished := true;
                                            end;
                         end
            else if fDimOrHalfDimSeventh
                     then // Dim or Half dim
                          begin
                                  if fHalfDimSeventh
                                     then result := HALF_DIM_CHORD_CHAR
                             else if fDimSeventh
                                     then result := DIM_CHORD_CHAR
                             else         Assert(false, 'Chord naming error');
                             finished := false;
                          end
            else if not finished
                     then // Minor chord
                          result := MIN_CHORD_CHAR;
         end;

      ckMajorThird: // major, maj b5, aug, dom
         begin
                 if fHighestQuality <= htqAugmentedFifth
                    then // 3 notes chords with a 3
                         case fFifthKind of
                            ckDiminishedFifth:
                               begin
                                  qualities := qualities - [htqDiminishedFifth];
                                  result := MAJ_CHORD_CHAR + FLAT_FIVE_CHORD_CHAR; // Maj b5 chord
                                  finished := true;
                               end;
                            ckAugmentedFifth:
                               begin
                                  qualities := qualities - [htqAugmentedFifth];
                                  result := AUG_CHORD_CHAR; // Aug chord
                                  finished := true;
                               end;
                         end
            else if fHighestQuality <= htqMajorSeventh
                    then // 4 notes chords with a 3
                         begin
                            if (fFifthKind = ckAugmentedFifth) and (htqMinorSeventh in qualities)
                               then // Aug7
                                    begin
                                       qualities := qualities - [htqAugmentedFifth];                                    
                                       result := AUG_CHORD_CHAR + SEVEN_CHORD_CHAR;
                                       finished := true;
                                    end;
                         end;

            if not finished
               then // Dom or major chord
                    begin
                       if htqMinorSeventh in qualities
                          then // Dominant chord
                               result := DOM_CHORD_CHAR;
                    end;
         end;
   end;

   // Major 6/Major X/Sus X chord
   if htqMajorSeventh in qualities
      then begin
              if fThirdKind = ckMinorThird
                 then result := result + SLASH_CHORD_CHAR;
              result := result + MAJ_CHORD_CHAR;
           end;

   forceAdditions := false;
   if not finished
      then // 6/7, 6/9, 6/7/9 chords
           begin
                   if fContainsSixth and (htqMajorNinth in qualities)
                      then // 6/9 and 6/7/9
                           begin
                              qualities := qualities - [htqMajorNinth];
                              forceAdditions := true;
                              if fContainsSeventh
                                 then // 6/7/9
                                      result := result + SIX_CHORD_CHAR + SLASH_CHORD_CHAR + SEVEN_CHORD_CHAR + SLASH_CHORD_CHAR + NINE_CHORD_CHAR
                                 else // 6/9
                                      result := result + SIX_NINE_CHORD_CHAR;
                              finished := true;
                           end
              else if fContainsSixth and fContainsSeventh and (not fDimOrHalfDimSeventh)
                      then // Maj or min 6/7
                           begin
                              forceAdditions := true;
                              result := result + SIX_SEVEN_CHORD_CHAR;
                              finished := true;
                           end;
           end;

   // 7, 9, 11 and 13 chords
   if not finished
      then begin
              // Sus4
              if sus4Flag
                 and ((fSeventhExtensionKind <> cseNone) or (fHighestQuality = htqMajorSixth))
                 and not(htqMajorSeventh in qualities)
                 then begin
                         result := result + SUS_CHORD_CHAR;
                         sus4Flag := false;
                      end;

              // Min 6, Maj 6 or sus6
              if (htqMajorSixth in qualities) and (not forceAdditions) and (not fDimSeventh)
                 then result := result + SIX_CHORD_CHAR;

              // Min X, Maj X or sus X chord
              case fSeventhExtensionKind of
                         cseSeventh:
                            result := result + SEVEN_CHORD_CHAR;
                         cseNinth:
                            result := result + NINE_CHORD_CHAR;
                         cseEleventh:
                            result := result + ELEVEN_CHORD_CHAR;
                         cseThirteenth:
                            result := result + THIRTEEN_CHORD_CHAR;
                      end;
           end;

   // sus 2 and sus 4 suffix
        if fThirdKind = ckSus2
           then result := result + SUS2_CHORD_CHAR
   else if sus4Flag
           then result := result + SUS4_CHORD_CHAR;

   // Additions
   if (not finished) or forceAdditions
      then begin
              if (htqMajorNinth in qualities) and not(fContainsSeventh)
                 and (fSeventhExtensionKind < cseNinth)
                 then // Add 9
                      result := result + ADD_CHORD_CHAR + NINE_CHORD_CHAR;

              if (htqPerfectEleventh in qualities)
                 and not (fContainsSeventh and fContainsNinth)
                 and (fSeventhExtensionKind < cseEleventh)                 
                 then // Add 11
                      result := result + ADD_CHORD_CHAR + ELEVEN_CHORD_CHAR;
           end;

   // Alterations
    alterations := '';
    alterationCount := 0;

    // b5/#5
         if (htqDiminishedFifth in qualities) and (not fDimOrHalfDimSeventh)
            then begin
                    alterations := alterations + FLAT_FIVE_CHORD_CHAR;
                    Inc(alterationCount);
                 end
    else if htqAugmentedFifth in qualities
            then begin
                    alterations := alterations + SHARP_FIVE_CHORD_CHAR;
                    Inc(alterationCount);
                 end;

    // b9
    if htqMinorNinth in qualities
       then begin
               alterations := alterations + FLAT_NINE_CHORD_CHAR;
               Inc(alterationCount);
            end;

    // #9                      
    if htqAugmentedNinth in qualities
       then begin
               alterations := alterations + SHARP_NINE_CHORD_CHAR;
               Inc(alterationCount);
            end;

    // #11
    if htqAugmentedEleventh in qualities
       then begin
               alterations := alterations + SHARP_ELEVEN_CHORD_CHAR;
               Inc(alterationCount);
            end;

    // b13
    if htqMinorThirteenth in qualities
       then begin
               alterations := alterations + FLAT_THIRTEEN_CHORD_CHAR;
               Inc(alterationCount);
            end;

    // Add alterations to result
    if alterationCount <> 0
       then begin
               if alterationCount = 1
                  then result := result + alterations
                  else result := result + LEFT_BRACKET_CHORD_CHAR + alterations + RIGHT_BRACKET_CHORD_CHAR;
            end;
end;

class function TChordTemplate.GetQualityNames(qualities : THalfToneQualities) : string;
var
   index : integer;
   quality : THalfToneQuality;
begin
   FixupQualities(qualities);
   result := '';
   for index := 0 to Ord(High(THalfToneQuality)) do
      begin
         quality := THalfToneQuality(index);
         if quality in qualities
            then begin
                    if result <> ''
                       then result := result + ' ';
                    result := result + HALFTONE_QUALITY_NUMBER[quality];
                 end;
      end;
end;

class procedure TChordTemplate.FixupQualities(var qualities : THalfToneQualities);
var
   thirteenthExtension : boolean;
begin
   // Compute seventh
   thirteenthExtension :=
      ((htqMinorSeventh in qualities) or (htqMajorSeventh in qualities))
      and ((htqMinorNinth in qualities) or (htqMajorNinth in qualities) or (htqAugmentedNinth in qualities))
      and ((htqPerfectEleventh in qualities) or (htqAugmentedEleventh in qualities));

   // Replace add13 by a 6 (and vice-versa)
        if (not thirteenthExtension) and (htqMajorThirteenth in qualities)
           then begin
                   qualities := qualities - [htqMajorThirteenth];
                   qualities := qualities + [htqMajorSixth];
                end
   else if thirteenthExtension and (htqMajorSixth in qualities)
           then begin
                   qualities := qualities + [htqMajorThirteenth];
                   qualities := qualities - [htqMajorSixth];
                end;
end;

initialization
begin
   globalScaleRepository := TScaleRepository.Create;

   globalScaleRepository.RegisterScale('major', [2, 2, 1, 2, 2, 2, 1],
                                       ['Ionian',
                                        'Dorian',
                                        'Phrygian',
                                        'Lydian',
                                        'Myxolidian',
                                        'Aeolian',
                                        'Locrian'
                                       ], true, true, kpMajor
                                       );

{$IFNDEF FreeVersion}
   globalScaleRepository.RegisterScale('melodic minor', [2, 1, 2, 2, 2, 2, 1],
                                       ['Jazz minor',
                                       'Dorian b2',
                                       'Lydian Augmented',
                                       'Lydian Dominant',
                                       'Mixolydian b6',
                                       'Locrian #2',
                                       'Super Locrian'
                                       ], true, true, kpMajor
                                       );
{$ENDIF}

   globalScaleRepository.RegisterScale('natural minor', [2, 1, 2, 2, 1, 2, 2],
                                       ['Aeolian',
                                        'Locrian',
                                        'Ionian',
                                        'Dorian',
                                        'Phrygian',
                                        'Lydian',
                                        'Myxolidian'
                                       ], true, false, kpMinor // Natural minor is always invisible
                                       );


{$IFNDEF FreeVersion}
   globalScaleRepository.RegisterScale('harmonic minor', [2, 1, 2, 2, 1, 3, 1],
                                       ['Harmonic minor',
                                       'Locrian nat. 6',
                                       'Major #5',
                                       'Dorian #4',
                                       'Phrygian-dominant',
                                       'Lydian #2',
                                       'Locrian b4 bb7'
                                       ], true, true, kpMinor
                                      );
  globalScaleRepository.RegisterScale('harmonic major', [2, 2, 1, 2, 1, 3, 1],
                                      ['Harmonic major',
                                       'Dorian b5',
                                       'Phrygian b4',
                                       'Lydian b3',
                                       'Mixolydian b2',
                                       'Lydian augmented #2',
                                       'Locrian bb7'
                                      ], true, true, kpMajor
                                     );
{$ENDIF}

   globalScaleRepository.RegisterScale('minor pentatonic', [3, 2, 2, 3, 2], false, true, kpMinor);

{$IFNDEF FreeVersion}
   globalScaleRepository.RegisterScale('major pentatonic', [2, 2, 3, 2, 3], false, true, kpMajor);
   globalScaleRepository.RegisterScale('blues 1', [3, 2, 1, 1, 3, 2], false, true, kpMinor);

//   globalScaleRepository.RegisterScale('whole tone', [2, 2, 2, 2, 2, 2], false);
//   globalScaleRepository.RegisterScale('augmented', [3, 1, 3, 1, 3, 2], false);

//   globalScaleRepository.RegisterScale('bebop major', [2, 2, 1, 2, 2, 1, 1, 1], false);
//   globalScaleRepository.RegisterScale('bebop dominant', [2, 2, 1, 2, 1, 1, 1, 2], false);
//   globalScaleRepository.RegisterScale('bebop minor', [2, 1, 1, 1, 2, 2, 1, 2], false);
{$ENDIF}

(*
{$IFNDEF FreeVersion}
   globalScaleRepository.RegisterScale('whole-half diminished', [2, 1, 2, 1, 2, 1, 2, 2], false);
   globalScaleRepository.RegisterScale('half-whole diminished', [1, 2, 1, 2, 1, 2, 1, 2], false);
   globalScaleRepository.RegisterScale('neapolitan minor', [], false);
   globalScaleRepository.RegisterScale('neapolitan major', [], false);
   globalScaleRepository.RegisterScale('oriental', [], false);
   globalScaleRepository.RegisterScale('major locrian', [], false);
   globalScaleRepository.RegisterScale('hungarian minor', [], false);
   globalScaleRepository.RegisterScale('spanish (eight tone)', [], false);
   globalScaleRepository.RegisterScale('enigmatic', [1, 3, 2, 2, 2, 1, 2], false);   
{$ENDIF}
*)
end;

finalization
begin
   globalScaleRepository.Free;
end;

end.
