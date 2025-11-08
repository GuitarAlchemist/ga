unit uTeNumFuncPicker;

interface

uses
   StdCtrls
   ,Classes
   ,Controls
   ,Graphics
   ,Windows
   ,ComCtrls
   ,uMusicClasses
   ,te_controls
   ,uChordVoicings
   ,Grids
   ,ExtCtrls
   ;

type
   TTeNumericFunctionPicker = class(TCustomControl)
      private
         fChordFunctions : TStringList;
         fChordModes : TStringList;
         fDegrees : TStringList;
         fRoots : TStringList;
         fGrid : TTeDrawGrid;
         fKey : TKey;
         fScaleName : string;
         fScaleMode : integer;
         fContentInvalidated : boolean;
         fSelectedDegree : integer;
         fSelectedChord : integer;
         fOnClick : TNotifyEvent;
         fOnDblClick : TNotifyEvent;
         procedure SetKey(const value : TKey);
         procedure SetScaleMode(const value : integer);
         procedure SetScaleName(const value : string);
         function GetCount : integer;
         procedure ComputeDegrees;
         procedure DoDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
         procedure DoClickCell(sender : TObject);
         procedure DoDblClickCell(sender : TObject);
      protected
         procedure Paint; override;
      public
         constructor Create(AOwner: TComponent); override;
         destructor Destroy; override;
         function GetSelectedChordTemplate : TChordTemplate;
         procedure PlaySelectedTemplate;                  
         property SelectedDegree : integer read fSelectedDegree;
         property SelectedChord : integer read fSelectedChord;
         property Count : integer read GetCount;
      published
         property Align;
         property Key : TKey read fKey write SetKey;
         property ScaleName : string read fScaleName write SetScaleName;
         property ScaleMode : integer read fScaleMode write SetScaleMode;
         property OnClick : TNotifyEvent read fOnClick write fOnClick;
         property OnDblClick : TNotifyEvent read fOnDblClick write fOnDblClick;
   end;

implementation

uses
   uMusicFontRoutines
   ,uMidi
   ;

{ TTeNumericFunctionPicker }

procedure TTeNumericFunctionPicker.ComputeDegrees;
var
   scale : TScale;
   degreeScale : TScale;
   index : integer;
   rootNote : shortint;
   diatonicRootNote : shortint;
   degreeNote : smallint;
   degreeQuality : smallint;
   degreeAccidental : smallint;
   s : string;
   keyRoot : THalfTone;
   chordRoot : THalfTone;
   sel : TGridRect;
begin
   // Inits
   keyRoot := MAJOR_KEY_ROOT[fKey];
   for index := 0 to fDegrees.count - 1 do
      begin
        fDegrees.Objects[index].Free;
        fDegrees.Objects[index] := nil;
      end;
   fRoots.Clear;      
   fDegrees.Clear;
   fChordFunctions.Clear;
   fChordModes.Clear;

   // Compute the chords and functions
   scale := TScale.Create;
   try
      if not globalScaleRepository.GetScale(scale, fScaleName)
         then Exit;
      rootNote := 0;
      for index := 0 to fScaleMode - 2 do
         rootNote := (rootNote + scale.degreeInterval[index]) mod 12;
      diatonicRootNote := DIATONIC_TONE[rootNote];
      degreeNote := rootNote;

      // Compute chord functions
      for index := 0 to scale.count do
         begin
            // Init
            s := '';

            // Degree accidental
            degreeAccidental := DEGREE_ACCIDENTAL[degreeNote] - KEY_ACCIDENTAL[rootNote, DIATONIC_TONE[degreeNote]];
            case degreeAccidental of
               -2: s := QUALITY_DIMINISHED;
               -1: s := QUALITY_FLAT;
               0: ; // Nothing
               1: s := QUALITY_SHARP;
               2: s := QUALITY_AUGMENTED;
               else Assert(false, 'Invalid accidental');
            end;

            // Compute the function and root
            degreeQuality := (DIATONIC_TONE[degreeNote] - diatonicRootNote + 7) mod 7;
            s := s + GetChordNumericRomanChar(degreeQuality);
            fChordFunctions.Add(s);
            chordRoot := THalfTone((Ord(keyRoot) + degreeNote) mod 12);
            fRoots.AddObject('', TObject(ord(chordRoot)));

            // Compute chord names
            degreeScale := TScale.Create;
            globalScaleRepository.GetScale(degreeScale, fScaleName, fScaleMode + index);
                 if fKey = ksCMajorAMinor
                    then s := HALFTONE_NAME_SMART[chordRoot]
            else if fKey <= ksCSharpMajorASharpMinor
                    then s := HALFTONE_NAME_SHARP[chordRoot]
            else         s := HALFTONE_NAME_FLAT[chordRoot];
            fDegrees.AddObject(s, degreeScale);

            // Pick up next degree note
            degreeNote := (degreeNote + scale.degreeInterval[fScaleMode + index - 1]) mod 12;
         end;
      fChordModes.Assign(scale.modeNames);

   finally
      scale.Free;
   end;

   sel.Left := 0;
   sel.Top := 0;
   sel.Right := 0;
   sel.Bottom := 0;
   fGrid.colCount := fChordFunctions.count;
   fGrid.width := fGrid.DefaultColWidth * fChordFunctions.count + 10;
   fGrid.Selection := sel;
end;

constructor TTeNumericFunctionPicker.Create(AOwner: TComponent);
begin
   inherited Create(AOwner);
   self.autoSize := true;

   fChordFunctions := TStringList.Create;
   fChordModes := TStringList.Create;
   fDegrees := TStringList.Create;
   fRoots := TStringList.Create;

   fGrid := TTeDrawGrid.Create(self);
   fGrid.parent := self;
   fGrid.rowCount := 4;
   fGrid.scrollBars := ssNone;   
   fGrid.fixedRows := 2;
   fGrid.fixedCols := 0;
   fGrid.defaultColWidth := 45;
   fGrid.defaultRowHeight := 12;
   fGrid.height := fGrid.rowCount * fGrid.defaultRowHeight + 7;
   fGrid.options := [goFixedVertLine, goFixedHorzLine, goVertLine];
   fGrid.OnDrawCell := DoDrawCell;
   fGrid.OnClick := DoClickCell;
   fGrid.OnDblClick := DoDblClickCell;

   fKey := ksCMajorAMinor;
   fScaleName := 'Major';
   fScaleMode := 1;

   fContentInvalidated := true;
   fSelectedDegree := 0;
   fSelectedChord := 0;
   fOnClick := nil;
   fOnDblClick := nil;
end;

destructor TTeNumericFunctionPicker.Destroy;
var
   index : integer;
begin
   fGrid.Free;
   fChordFunctions.Free;
   fChordModes.Free;

   for index := 0 to fDegrees.count - 1 do
      begin
        fDegrees.Objects[index].Free;
        fDegrees.Objects[index] := nil;
      end;
   fDegrees.Free;
   fRoots.Free;   
   inherited;
end;

procedure TTeNumericFunctionPicker.DoDrawCell(sender: TObject; aCol, aRow: Integer; rect: TRect; state: TGridDrawState);
var
   tm : TTextMetric;
   s : string;
   x, y : integer;
   degreeScale : TScale;
begin
        if aRow = 0
           then // Draw the numeric chord functions
                begin
                   fGrid.canvas.font.name := STAFF_FONT_NAME;
                   fGrid.canvas.font.size := 14;
                   fGrid.canvas.font.style := [fsBold];
                   fGrid.canvas.font.color := clDkGray;
                   GetTextMetrics(fGrid.canvas.handle, tm);

                   Assert(aCol < fChordFunctions.count);
                   s := fChordFunctions[aCol];
                   x := rect.Left + (fGrid.ColWidths[aCol]) div 2 - canvas.TextWidth(s);
                   y := rect.top - tm.tmDescent;
                end
   else if aRow = 1
           then // Draw the numeric chord functions
                begin
                   fGrid.canvas.font.name := 'Tahoma';
                   fGrid.canvas.font.size := 6;
                   fGrid.canvas.font.style := [];
                   fGrid.canvas.font.color := clBlack;
                   GetTextMetrics(fGrid.canvas.handle, tm);

                   Assert(aCol < fChordModes.count);
                   s := fChordModes[aCol];
                   x := rect.Left + 1;
                   y := rect.top;
                end
   else // Draw chord
                begin
                   fGrid.canvas.font.name := CHORDS_FONT_NAME;
                   fGrid.canvas.font.size := 8;
                   fGrid.canvas.font.style := [];
                   fGrid.canvas.font.color := clBlack;

                   degreeScale := TScale(fDegrees.Objects[aCol]);
                   s := fDegrees[aCol] + ' ' + TChordTemplate(degreeScale.matchingChords.objects[aRow - 2]).chordName;
                   x := rect.Left + 1;
                   y := rect.top;
                end;

   fGrid.canvas.brush.style := bsClear;           
   fGrid.canvas.TextOut(x, y, s);
end;

procedure TTeNumericFunctionPicker.DoClickCell(sender : TObject);
begin
   fSelectedDegree := fGrid.Selection.left;
   fSelectedChord := fGrid.Selection.top - 2;
   if Assigned(fOnClick)
      then onClick(self);
end;

procedure TTeNumericFunctionPicker.DoDblClickCell(sender : TObject);
begin
   if Assigned(fOnDblClick)
      then onDblClick(self);
end;

procedure TTeNumericFunctionPicker.Paint;
begin
   if fContentInvalidated
      then begin
              ComputeDegrees;
              fContentInvalidated := false;
           end;
   inherited;
end;

procedure TTeNumericFunctionPicker.SetKey(const value : TKey);
begin
   if value <> fKey
      then begin
              fKey := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeNumericFunctionPicker.SetScaleMode(const value: integer);
begin
   if value <> fScaleMode
      then begin
              fScaleMode := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeNumericFunctionPicker.SetScaleName(const value: string);
begin
   if value <> fScaleName
      then begin
              fScaleName := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

function TTeNumericFunctionPicker.GetCount : integer;
begin
   result := fDegrees.count;
end;

function TTeNumericFunctionPicker.GetSelectedChordTemplate: TChordTemplate;
var
   degreeScale : TScale;
begin
   degreeScale := TScale(fDegrees.Objects[fGrid.selection.left]);
   Assert(degreeScale.matchingChords.objects[fGrid.selection.top - 2] is TChordTemplate);
   result := TChordTemplate(degreeScale.matchingChords.objects[fGrid.selection.top - 2]);
end;

procedure TTeNumericFunctionPicker.PlaySelectedTemplate;
var
   root : THalfTone;
begin
   root := THalfTone(integer(fRoots.objects[fGrid.selection.left]));
   globalMidi.PlayChordTemplate(GetSelectedChordTemplate, root);
end;

end.
