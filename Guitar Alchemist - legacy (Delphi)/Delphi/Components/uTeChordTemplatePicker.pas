unit uTeChordTemplatePicker;

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
   TTeDrawGridEx= class(TTeDrawGrid)
      published
         property onResize;
   end;

   TTeCustomChordTemplatePicker = class(TCustomControl)
      private
         fContentInvalidated : boolean;
         fRefreshingContent : boolean;
         fScaleRoot : THalfTone;
         fScaleName : string;
         fScaleMode : integer;
         fKey : TKey;
         fUseKey : boolean;
         fChordRootName : string;
         fTemplateIndex : integer;
         fDegreesOffset : integer;
         fRequiredQualities : THalfToneQualities;
         fChordKindFilter : TChordKind;
         fChordKindImageList : TImageList;
         fOnClick : TNotifyEvent;
         fOnDoubleClick : TNotifyEvent;
         procedure SetScaleMode(const value: integer);
         procedure SetScaleName(const value: string);
         procedure SetScaleRoot(const value: THalfTone);
         procedure SetKey(const value: TKey);
         procedure SetUseKey(const value: boolean);
         procedure SetTemplateIndex(const value: integer);
         procedure SetDegreesOffset(const value: integer);
         procedure SetChordKindImageList(value : TImageList);
         procedure SetRequiredQualities(value : THalfToneQualities); virtual;
         procedure SetChordKindFilter(value : TChordKind); virtual;
         procedure DoDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
      protected
         fGrid : TTeDrawGridEx;
         fScale : TScale;
         procedure Paint; override;
         procedure Resize; override;
         procedure Loaded; override;
         procedure DoClick(sender : TObject); dynamic;
         procedure DoDoubleClick(sender : TObject); dynamic;
         procedure DoMouseMove(sender : TObject; shift : TShiftState; x, y : integer); dynamic;
         procedure ChangeChordKindImageList(sender : TObject); virtual;
         property ScaleRoot : THalfTone read fScaleRoot write SetScaleRoot;
         property ScaleName : string read fScaleName write SetScaleName;
         property ScaleMode : integer read fScaleMode write SetScaleMode;
         property Key : TKey read fKey write SetKey;
         property UseKey : boolean read fUseKey write SetUseKey;
         property DegreesOffset : integer read fDegreesOffset write SetDegreesOffset;
         property ChordKindImageList : TImageList read fChordKindImageList write SetChordKindImageList;
         property RequiredQualities : THalfToneQualities read fRequiredQualities write SetRequiredQualities default [];
         property ChordKindFilter : TChordKind read fChordKindFilter write SetChordKindFilter default ckNone;
         property OnClick : TNotifyEvent read fOnClick write fOnClick;
         property OnDoubleClick : TNotifyEvent read fOnDoubleClick write fOnDoubleClick;
      public
         constructor Create(AOwner: TComponent); override;
         destructor Destroy; override;
         function SelectChordTemplate(chordTemplate : TChordTemplate) : boolean;
         function GetChordName(chordTemplate : TChordTemplate) : string;
         function GetSelectedChordTemplate : TChordTemplate;
         procedure PlaySelectedTemplate;
         procedure ShowTopRow;
         property TemplateIndex : integer read fTemplateIndex  write SetTemplateIndex;
   end;

   TTeChordTemplatePicker = class(TTeCustomChordTemplatePicker)
      published
         property Align;
         property ScaleRoot;
         property ScaleName;
         property ScaleMode;
         property Key;
         property UseKey;
         property DegreesOffset;
         property ChordKindImageList;
         property OnClick;
         property OnDoubleClick;
   end;

   TTeMultiChordTemplatePicker = class(TTeCustomChordTemplatePicker)
      private
         fMultiContentInvalidated : boolean;
         procedure SetRequiredQualities(value : THalfToneQualities); override;
         procedure SetChordKindFilter(value : TChordKind); override;
      protected
         procedure Paint; override;
      public
         constructor Create(AOwner: TComponent); override;
      published
         property Align;
         property ScaleRoot;
         property Key;
         property DegreesOffset;
         property ChordKindImageList;
         property RequiredQualities;
         property ChordKindFilter;
         property OnClick;
         property OnDoubleClick;
   end;

implementation

{ TTeChordTemplatePicker }

uses
   te_theme
   ,uMusicFontRoutines
   ,uMidi
   ;

constructor TTeCustomChordTemplatePicker.Create(AOwner: TComponent);
begin
   inherited;
   fContentInvalidated := true;
   fRefreshingContent := false;   

   fGrid := TTeDrawGridEx.Create(self);
   fGrid.parent := self;
   fGrid.align := alClient;
   fGrid.options := [];
   fGrid.fixedCols := 0;
   fGrid.fixedRows := 0;
   fGrid.colCount := 1;
   fGrid.defaultColWidth := 200;
   fGrid.defaultRowHeight := 16;
   fGrid.width := 230;
   fGrid.scrollBars := ssVertical; 

   fGrid.OnDrawCell := DoDrawCell;
   fGrid.OnClick := DoClick;
   fGrid.OnDblClick := DoDoubleClick;   
   fGrid.OnMouseMove := DoMouseMove;

   fRequiredQualities := [];
   fChordKindFilter := ckNone;

   fScale := TScale.Create;

   fScaleName := 'Major';
   fScaleMode := 1;
   fScaleRoot := htC;
   fKey := ksCMajorAMinor;
   fUseKey := false;
   fTemplateIndex := 0;
   fDegreesOffset := 200;   
   fChordKindImageList := nil;
   fOnClick := nil;
   fOnDoubleClick := nil;
end;

destructor TTeCustomChordTemplatePicker.Destroy;
begin
   fGrid.OnDrawCell := nil;
   if fChordKindImageList <> nil
      then fChordKindImageList.onChange := nil;
   fGrid.Free;
   fScale.Free;
   inherited;
end;

procedure TTeCustomChordTemplatePicker.DoDrawCell(sender: TObject; aCol, aRow: Integer; rect: TRect; state: TGridDrawState);
var
   x, y : integer;
   chordTemplate : TChordTemplate;
   chordName : string;
   index : integer;
   ch : char;
   quality : THalfToneQuality;
   incX : integer;
   tm : TTextMetric;
   bitmap : graphics.TBitmap;
begin
   if aRow >= fScale.matchingChords.count
      then Exit;

   if Assigned(CurrentTheme)
      then begin
                   if gdSelected in state
                      then fGrid.canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextSelected])
              else if gdFocused in state
                      then fGrid.canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextFocused])
              else         fGrid.canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextNormal]);
           end;

   fGrid.canvas.font.name := CHORDS_FONT_NAME;
   fGrid.canvas.font.size := 11;
   fGrid.canvas.brush.style := bsClear;

   // Retrieve the current chord template
   assert(aRow < fScale.matchingChords.count);
   Assert(fScale.matchingChords.objects[aRow] is TChordTemplate);
   chordTemplate := TChordTemplate(fScale.matchingChords.objects[aRow]);

   chordName := GetChordName(chordTemplate);
   if chordTemplate.containsOnlyStackedThirds
      then fGrid.canvas.font.style := [fsBold];

   x := rect.Left + 1;
   y := rect.top;
   fGrid.canvas.TextOut(x, y, chordName);

   // Draw chord kind
   x := Rect.Left + fDegreesOffset;
   if fChordKindImageList <> nil
      then begin
              bitmap := graphics.TBitmap.Create;
              try
                 bitmap.transparentMode := tmAuto;
                 bitmap.transparent := true;
                 fChordKindImageList.GetBitmap(Ord(chordTemplate.kind), bitmap);
                 y := rect.top + (fGrid.defaultRowHeight - bitmap.Height) div 2;
                 fGrid.canvas.Draw(x, y, bitmap);
              finally
                 bitmap.Free;
              end;
              Inc(x, 32);
           end;

   // Draw the chord degrees
   fGrid.canvas.font.name := STAFF_FONT_NAME;
   fGrid.canvas.font.height := -Round(fGrid.defaultRowHeight * 1.3);
   GetTextMetrics(fGrid.canvas.handle, tm);
   y := Rect.top + (fGrid.defaultRowHeight - tm.tmHeight) div 2 - 1;

   fGrid.canvas.font.style := [];
   incX := Round(fGrid.canvas.TextWidth(DEGREE_QUALITY_b13) * 1.15);
   for index := 0 to chordTemplate.chordDegreesCount - 1 do
      begin
         // Get quality char
         quality := chordTemplate.chordDegrees[index];
         if (quality = htqMajorSixth) and chordTemplate.dimSeventh
            then // Replace 6 by bb7 if diminished or half dim
                 ch := DEGREE_QUALITY_bb7
            else // Regular quality
                 ch := GetChordQualityChar(quality);

         // Alterations
         if IsAlteration(quality)
            then fGrid.canvas.font.style := [fsUnderline]
            else fGrid.canvas.font.style := [];

         // Draw degree quality
         fGrid.canvas.TextOut(x - fGrid.canvas.TextWidth(ch) div 2, y, ch);
         Inc(x, incX);
      end;
end;

function TTeCustomChordTemplatePicker.GetChordName(chordTemplate : TChordTemplate): string;
begin
   if chordTemplate <> nil
      then result := fChordRootName + ' ' + chordTemplate.chordName
      else result := '';
end;

function TTeCustomChordTemplatePicker.GetSelectedChordTemplate : TChordTemplate;
begin
   if (fScale.matchingChords.count > 0) and (fGrid.selection.top < fScale.matchingChords.count)
      then begin
              Assert(fGrid.selection.top <> -1);      
              Assert(fScale.matchingChords.objects[fGrid.selection.top] is TChordTemplate);
              result := TChordTemplate(fScale.matchingChords.objects[fGrid.selection.top]);
           end
      else result := nil;
end;

procedure TTeCustomChordTemplatePicker.Paint;
begin
   if fContentInvalidated
      then begin
              fRefreshingContent := true;
              try
                 if fScale.allowIncrementalMatchingChordList
                    then begin
                                 if fKey = ksCMajorAMinor
                                    then fChordRootName := HALFTONE_NAME_SMART[fScaleRoot]
                            else if fKey <= ksCSharpMajorASharpMinor
                                    then fChordRootName := HALFTONE_NAME_SHARP[fScaleRoot]
                            else         fChordRootName := HALFTONE_NAME_FLAT[fScaleRoot];
                         end
                    else begin
                            if fUseKey
                               then begin
                                       globalScaleRepository.GetScale(fScale, fScaleName);
                                       fScaleRoot := fScale.GetDegreeHalfTone(fKey, fScaleMode - 1);
                                    end;
                            globalScaleRepository.GetScale(fScale, fScaleName, fScaleMode);
                            if fUseKey
                               then begin
                                            if fKey = ksCMajorAMinor
                                               then fChordRootName := HALFTONE_NAME_SMART[fScaleRoot]
                                       else if fKey <= ksCSharpMajorASharpMinor
                                               then fChordRootName := HALFTONE_NAME_SHARP[fScaleRoot]
                                       else         fChordRootName := HALFTONE_NAME_FLAT[fScaleRoot];
                                    end
                               else begin
                                       if HALFTONE_IS_SHARP_KEY[fScaleRoot]
                                          then fChordRootName := HALFTONE_NAME_SHARP[fScaleRoot]
                                          else fChordRootName := HALFTONE_NAME_FLAT[fScaleRoot];
                                    end;
                            fGrid.topRow := 0;
                            fGrid.rowCount := fScale.matchingChords.count; // Retrieve the list of matching chords
                            fGrid.SelectCell(0, 0);
                            fGrid.FocusCell(0, 0, true);
                         end;
              finally
                 fRefreshingContent := false;
              end;

              fContentInvalidated := false;
           end;
   inherited;
end;

procedure TTeCustomChordTemplatePicker.Resize;
begin
   inherited;
   fGrid.defaultColWidth := clientWidth - 12;
   Invalidate;
end;

procedure TTeCustomChordTemplatePicker.Loaded;
begin
   inherited Loaded;
   Resize;
end;

procedure TTeCustomChordTemplatePicker.DoClick(sender : TObject);
begin
   fTemplateIndex := fGrid.selection.top;
   if Assigned(fOnClick) and (not fRefreshingContent)
      then onClick(self);
end;

procedure TTeCustomChordTemplatePicker.DoDoubleClick(sender : TObject);
begin
   if Assigned(fOnDoubleClick)
      then fOnDoubleClick(self);
end;

procedure TTeCustomChordTemplatePicker.DoMouseMove(sender : TObject; shift : TShiftState; x, y : integer);
begin
   if (fGrid.selection.top <> fTemplateIndex) and (ssShift in shift)
      then begin
              fTemplateIndex := fGrid.selection.top;
              Assert(fTemplateIndex <> -1);
              if Assigned(fOnClick)
                 then onClick(self);
           end;
end;

procedure TTeCustomChordTemplatePicker.SetScaleMode(const value: integer);
begin
   if value <> fScaleMode
      then begin
              fScaleMode := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetScaleName(const value: string);
begin
   if value <> fScaleName
      then begin
              fScaleName := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetScaleRoot(const value: THalfTone);
begin
   if value <> fScaleRoot
      then begin
              fScaleRoot := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetKey(const value: TKey);
begin
   if value <> fKey
      then begin
              fKey := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetUseKey(const value: boolean);
begin
   if value <> fUseKey
      then begin
              fUseKey := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetTemplateIndex(const value: integer);
var
   sel : TGridRect;
begin
   if value <> fTemplateIndex
      then begin
              if (fTemplateIndex >= 0) and (fTemplateIndex < fGrid.rowCount)
                 then begin
                         fTemplateIndex := value;
                         sel.top := value;
                         sel.bottom := value;
                         sel.left := 0;
                         sel.right := 0;
                         fGrid.selection := sel;
                         fGrid.FocusCell(0, value, false);                         
                      end;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetDegreesOffset(const value: integer);
begin
   if value <> fDegreesOffset
      then begin
             fDegreesOffset := value;
             Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetChordKindImageList(value : TImageList);
begin
   if value <> fChordKindImageList
      then begin
              fChordKindImageList := value;
              if fChordKindImageList <> nil
                 then fChordKindImageList.onChange := ChangeChordKindImageList;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetRequiredQualities(value : THalfToneQualities);
begin
   if value <> fRequiredQualities
      then begin
              fRequiredQualities := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.SetChordKindFilter(value : TChordKind);
begin
   if value <> fChordKindFilter
      then begin
              fChordKindFilter := value;
              fContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeCustomChordTemplatePicker.PlaySelectedTemplate;
begin
   globalMidi.PlayChordTemplate(GetSelectedChordTemplate, fScaleRoot);
end;

procedure TTeCustomChordTemplatePicker.ShowTopRow;
begin
   fGrid.topRow := 0;
end;

procedure TTeCustomChordTemplatePicker.ChangeChordKindImageList(sender: TObject);
begin
   if not (csDestroying in ComponentState)
      then Invalidate;
end;

{ TTeMultiChordTemplatePicker }

constructor TTeMultiChordTemplatePicker.Create(AOwner: TComponent);
begin
   inherited Create(AOwner);
   fMultiContentInvalidated := true;
end;

procedure TTeMultiChordTemplatePicker.Paint;
var
   index : integer;
begin
   if fMultiContentInvalidated
      then begin
              fGrid.rowCount := 0;
              fScale.ClearMatchingChords;
              fScale.allowIncrementalMatchingChordList := true;
              for index := 1 to 7 do
                 begin
                    fScale.GetFromRepository('Major', index);
                    fScale.GetMatchingChordsEx(fRequiredQualities, fChordKindFilter);
                 end;
              for index := 1 to 7 do
                 begin
                    fScale.GetFromRepository('Melodic minor', index);
                    fScale.GetMatchingChordsEx(fRequiredQualities, fChordKindFilter);
                 end;
              for index := 1 to 7 do
                 begin
                    fScale.GetFromRepository('Harmonic minor', index);
                    fScale.GetMatchingChordsEx(fRequiredQualities, fChordKindFilter);
                 end;
              for index := 1 to 7 do
                 begin
                    fScale.GetFromRepository('Harmonic major', index);
                    fScale.GetMatchingChordsEx(fRequiredQualities, fChordKindFilter);
                 end;
             fScale.SortIncrementalMatchingChordList;
             fGrid.rowCount := fScale.matchingChords.count;
             fMultiContentInvalidated := false;
             fContentInvalidated := true;
           end;
   inherited;
end;

procedure TTeMultiChordTemplatePicker.SetRequiredQualities(value : THalfToneQualities);
begin
   if value <> fRequiredQualities
      then begin
              fRequiredQualities := value;
              fMultiContentInvalidated := true;
              Invalidate;
           end;
end;

procedure TTeMultiChordTemplatePicker.SetChordKindFilter(value : TChordKind);
begin
   if value <> fChordKindFilter
      then begin
              fChordKindFilter := value;
              fMultiContentInvalidated := true;
              Invalidate;
           end;
end;

function TTeCustomChordTemplatePicker.SelectChordTemplate(chordTemplate : TChordTemplate) : boolean;
var
   index : integer;
   c : TChordTemplate;
begin
   result := false;
   for index := 0 to fScale.matchingChords.count - 1 do
      begin
         Assert(fScale.matchingChords.objects[index] is TChordTemplate);
         c := TChordTemplate(fScale.matchingChords.objects[index]);
         if c.qualities = chordTemplate.qualities
            then begin
                    TemplateIndex := index;
                    result := true;
                    Exit;
                 end;
      end;
end;

end.
