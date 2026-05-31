unit uGlobalTypes;

interface

uses
   Classes
   ,uMusicClasses
   ,ComCtrls
   ,se_controls
   ,KsSkinListBoxs
   ,uSeChordTemplateBox
   ,uSeVoicingListView
   ,uGuitarChord
   ,uStaff
   ;

type
   TProjectPage = class(TComponent)
      private
         fSuffix : string;
         fPage : TSeCustomTabSheet;
         fTreeNode : TTreeNode;
      protected
         procedure Changed; virtual;
         function GetTitle : string; virtual;
      public
         constructor Create(page : TSeCustomTabSheet; node : TTreeNode); reintroduce;
         procedure AfterConstruction; override;
         property page : TSeCustomTabSheet read fPage;
         property treeNode : TTreeNode read fTreeNode;
   end;

   TCustomScaleProjectPage = class(TProjectPage)
      private
         fKey : TKey;
         fScaleName : string;
         fScaleMode : integer;
         fMinorKey : boolean;
      protected
         procedure SetKey(value : TKey); virtual;
         procedure SetScaleName(value : string); virtual;
         procedure SetScaleMode(value : integer); virtual;

         procedure Changed; override;
         function GetTitle : string; override;
      public
         constructor Create(page : TSeCustomTabSheet; node : TTreeNode;
                            key : TKey; scaleName : string; scaleMode : integer);

         property key : TKey read fKey write SetKey;
         property scaleName : string read fScaleName write SetScaleName;
         property scaleMode : integer read fScaleMode write SetScaleMode;
         property minorKey : boolean read fMinorKey;
   end;

   TCustomHarmonizedScaleProjectPage = class(TCustomScaleProjectPage)
      private
         fNoteCount : TChordNoteCount;
         fHarmony : TChordHarmony;
      protected
         procedure SetNoteCount(value : TChordNoteCount); virtual;
         procedure SetHarmony(value : TChordHarmony); virtual;

         function GetTitle : string; override;
      public
         constructor Create(page : TSeCustomTabSheet; node : TTreeNode;
                            key : TKey; scaleName : string; scaleMode : integer;
                            noteCount : TChordNoteCount = 3; harmony : TChordHarmony = chTertian);
         property noteCount : TChordNoteCount read fNoteCount write SetNoteCount;
         property harmony : TChordHarmony read fHarmony write SetHarmony;
   end;


   TSimpleScalePage = class(TCustomScaleProjectPage)
      private
         fScaleChart : TScaleChart;
      protected
         procedure Changed; override;
      public
         constructor Create(page : TSeCustomTabSheet; node : TTreeNode;
                            key : TKey; scaleName : string; scaleMode : integer;
                            scaleChart : TScaleChart);
   end;

   TSimpleScaleMatchingChordsPage = class(TCustomScaleProjectPage)
      private
         fMatchingChords : TSeChordTemplateList;
         fVoicingList : TSeVoicingListView;
      protected
         procedure Changed; override;
      public
         constructor Create(page : TSeCustomTabSheet; node : TTreeNode;
                            key : TKey; scaleName : string; scaleMode : integer;
                            matchingChords : TSeChordTemplateList;
                            voicingList : TSeVoicingListView);
   end;

   THarmonizedScalePage = class(TCustomHarmonizedScaleProjectPage)
     private
        fScaleHarmonizer : TScaleHarmonizer;
     protected
        procedure SetKey(value : TKey); override;
        procedure SetScaleName(value : string); override;
        procedure SetScaleMode(value : integer); override;
        procedure SetNoteCount(value : TChordNoteCount); override;
        procedure SetHarmony(value : TChordHarmony); override;
     public
        constructor Create(page : TSeCustomTabSheet; node : TTreeNode;
                           key : TKey; scaleName : string; scaleMode : integer;
                           scaleHarmonizer : TScaleHarmonizer);
   end;



implementation

{ TProjectPage }

constructor TProjectPage.Create(page: TSeCustomTabSheet; node : TTreeNode);
begin
   inherited Create(page);
   fPage := page;
   fSuffix := '';
   fTreeNode := node;
end;

function TProjectPage.GetTitle : string;
begin
   result := 'New';
end;

procedure TProjectPage.Changed;
var
   title : string;
begin
   // Compute the page title
   title := GetTitle;
   if fSuffix = ''
      then fPage.caption := title
      else fPage.caption := title + ' ' + fSuffix;
   fTreeNode.text := title;
end;

procedure TProjectPage.AfterConstruction;
begin
   inherited;
   Changed;
end;

{ TCustomScaleProjectPage }

constructor TCustomScaleProjectPage.Create(page : TSeCustomTabSheet; node : TTreeNode;
                                           key : TKey; scaleName : string; scaleMode : integer);
begin
   inherited Create(page, node);
   fKey := key;
   fScaleName := scaleName;
   fScaleMode := scaleMode;
end;

procedure TCustomScaleProjectPage.SetKey(value: TKey);
begin
   if value <> fKey
      then begin
              fKey := value;
              Changed;
           end;
end;

procedure TCustomScaleProjectPage.SetScaleMode(value: integer);
begin
   if value <> fScaleMode
      then begin
              fScaleMode := value;
              Changed;
           end;
end;

procedure TCustomScaleProjectPage.SetScaleName(value: string);
begin
   if value <> fScaleName
      then begin
              fScaleName := value;
              Changed;
           end;
end;

function TCustomScaleProjectPage.GetTitle: string;
begin
   result := GetScaleFullName(fKey, fScaleName, fScaleMode);
end;

procedure TCustomScaleProjectPage.Changed;
begin
   fMinorKey := IsScaleMinor(fScaleName, fScaleMode);
   inherited Changed;
end;

{ TCustomHarmonizedScaleProjectPage }

constructor TCustomHarmonizedScaleProjectPage.Create(page : TSeCustomTabSheet; node : TTreeNode;
                                                     key : TKey; scaleName : string; scaleMode : integer;
                                                     noteCount : TChordNoteCount; harmony : TChordHarmony);
begin
   inherited Create(page, node, key, scaleName, scaleMode);
   fNoteCount := noteCount;
   fHarmony := harmony;
end;

function TCustomHarmonizedScaleProjectPage.GetTitle : string;
begin
   result := CHORD_NOTE_COUNT_NAME[fNoteCount];
   if fHarmony <> chTertian
      then result := result + '(' + CHORD_HARMONY_NAME[fHarmony] + ')';
   result := GetScaleFullName(fKey, fScaleName, fScaleMode) + ' ' + result;
end;

procedure TCustomHarmonizedScaleProjectPage.SetNoteCount(value : TChordNoteCount);
begin
   if value <> fNoteCount
      then begin
              fNoteCount := value;
              Changed;
           end;
end;

procedure TCustomHarmonizedScaleProjectPage.SetHarmony(value : TChordHarmony);
begin
   if value <> fHarmony
      then begin
              fHarmony := value;
              Changed;
           end;
end;

{ TSimpleScalePage }

procedure TSimpleScalePage.Changed;
begin
  inherited;

  fScaleChart.key := fKey;
  fScaleChart.ScaleName := fScaleName;
  fScaleChart.ScaleMode := fScaleMode;
end;

constructor TSimpleScalePage.Create(page: TSeCustomTabSheet; node : TTreeNode;
                                    key : TKey; scaleName : string; scaleMode : integer;
                                    scaleChart : TScaleChart);
begin
   inherited Create(page, node, key, scaleName, scaleMode);
   fScaleChart := scaleChart;
end;

{ THarmonizedScalePage }

constructor THarmonizedScalePage.Create(page: TSeCustomTabSheet;
                                        node: TTreeNode; key: TKey; scaleName: string; scaleMode: integer;
                                        scaleHarmonizer: TScaleHarmonizer);
begin
   inherited Create(page, node, key, scaleName, scaleMode);
   fScaleHarmonizer := scaleHarmonizer;
end;

procedure THarmonizedScalePage.SetKey(value: TKey);
begin
   if value <> fKey
      then begin
              inherited;
              fScaleHarmonizer.key := value;
           end;
end;

procedure THarmonizedScalePage.SetScaleMode(value: integer);
begin
   if value <> fScaleMode
      then begin
              inherited;
              fScaleHarmonizer.ScaleMode := value;
           end;
end;

procedure THarmonizedScalePage.SetScaleName(value: string);
begin
   if value <> fScaleName
      then begin
              inherited;
              fScaleHarmonizer.ScaleName := value;
           end;
end;

procedure THarmonizedScalePage.SetNoteCount(value: TChordNoteCount);
begin
   if value <> fNoteCount
      then begin
              inherited;
              fScaleHarmonizer.NoteCount := value;
           end;

end;

procedure THarmonizedScalePage.SetHarmony(value: TChordHarmony);
begin
   if value <> fHarmony
      then begin
              inherited;
              fScaleHarmonizer.Harmony := value; 
           end
end;


{ TSimpleScaleMatchingChordsPage }

procedure TSimpleScaleMatchingChordsPage.Changed;
var
   chordTemplate : TchordTemplate;
begin
   inherited;
   fMatchingChords.key := fKey;
   fMatchingChords.scaleName := fScaleName;
   fMatchingChords.scaleMode := fScaleMode;

   fVoicingList.key := fKey;
   fVoicingList.minorKey := fMinorKey;
   chordTemplate := fMatchingChords.GetSelectedChordTemplate;
   if chordTemplate <> nil
      then fVoicingList.qualities := chordTemplate.qualities;
end;

constructor TSimpleScaleMatchingChordsPage.Create(page: TSeCustomTabSheet;
                                                  node: TTreeNode; key: TKey; scaleName: string; scaleMode: integer;
                                                  matchingChords: TSeChordTemplateList; voicingList: TSeVoicingListView);
begin
   inherited Create(page, node, key, scaleName, scaleMode);
   fMatchingChords := matchingChords;
   fVoicingList := voicingList;
end;

end.
