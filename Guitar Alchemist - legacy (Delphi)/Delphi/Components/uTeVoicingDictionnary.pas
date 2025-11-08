unit uTeVoicingDictionnary;

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

const
   TOP_OFFSET = 8;
   RIGHT_OFFSET = 28;
   STR_SPACE = 6;
   FRET_SPACE = 7;

type
   TMiniGuitarChord = class(TCustomControl)
      private
         fVoicing : TChordVoicing;
         fPositions : TVoicingPositions;
         fMaxFret : integer;
         fMinFret : integer;

         fPosPics : TImageList;
         fDotBitmap : graphics.TBitmap;
         fOpenBitmap : graphics.TBitmap;
         fMutedBitmap : graphics.TBitmap;

         fDeltaXOpen : integer;
         fDeltaYOpen : integer;
         fDeltaXMuted : integer;
         fDeltaYMuted : integer;
         fDeltaXNormal : integer;
         fDeltaYNormal : integer;
         fInitialized : boolean;

         procedure SetVoicing(value : TChordVoicing);
      protected
         function GetStringX(str : integer) : integer;
         function GetFretY(fret : integer; dot : boolean = false) : integer;
         procedure Paint; override;
      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
         property voicing : TChordVoicing read fVoicing write SetVoicing;
   end;

   TTeDrawGridEx= class(TTeDrawGrid)
      published
         property onResize;
   end;

   TTeVoicingDictionnary = class(TCustomControl)
      private
         fPageControl : TTePageControl;
         fMenuPanel : TTePanel;
         fChordName : TLabel;
         fNumberOfChords : TLabel;
         fSimplePage : TTeTabSheet;
{$IFNDEF FreeVersion}
         fDetailedPage : TTeTabSheet;
         fHeaderControl : TTeHeaderControl;
         fDetailedDrawGrid : TTeDrawGridEx;
{$ENDIF}
         fDetailed : boolean;

         fSimpleDrawGrid : TTeDrawGridEx;

         fAddColumns : boolean;
         fComputeContent : boolean;
         fSettingChordTemplate : boolean;         
         fChordVoicingCollection : TChordVoicingCollection;
         fMiniGuitarChord : TMiniGuitarChord;
         fColorDegrees : boolean;
         fDifficultyImageList : TImageList;
         fDifficultySmallImageList : TImageList;
         fSortDirectionImageList : TImageList;
         fMaxDifficulty : TChordVoicingDifficulty;
         fNoMutedStrings : boolean;
         fNoOpenStrings : boolean;
         fNoBarres : boolean;         
         fVoicingFilter : TChordVoicingFilter;
         fMaxFret : integer;
         fChordTemplateName : string;
{$IFNDEF FreeVersion}
         procedure RefreshColumnWidths;
{$ENDIF}
         function GetRoot : THalfTone;
         function GetQualities : THalfToneQualities;
         function GetSelectedVoicing : TChordVoicing;
         procedure SetRoot(value : THalfTone);
         procedure SetDetailed(value : boolean);
         procedure SetQualities(value : THalfToneQualities);
         procedure SetDifficultyImageList(value : TImageList);
         procedure SetDifficultySmallImageList(value : TImageList);
         procedure SetSortDirectionImageList(value : TImageList);
         procedure SetMaxDifficulty(value : TChordVoicingDifficulty);
         procedure SetVoicingFilter(value : TChordVoicingFilter);
         procedure SetNoMutedStrings(value : boolean);
         procedure SetNoOpenStrings(value : boolean);
         procedure SetNoBarres(value : boolean);
         procedure SetMaxFret(value : integer);
         procedure DoSimpleDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
{$IFNDEF FreeVersion}
         procedure DoDetailedDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
         procedure PaintVoicingListGridCell(voicing : TChordVoicing; sectionText : string; aCol, aRow : integer;
                                            canvas : TCanvas; rect : TRect; state : TGridDrawState);
         procedure DoHeaderSectionResize(headerControl : TTeHeaderControl; section : TTeHeaderSection);
         procedure DoHeaderSectionDrag(sender : TObject; fromSection, toSection : TTeHeaderSection; var allowDrag : boolean);
         procedure DoHeaderSectionEndDrag(sender : TObject);
         procedure DoHeaderSectionClick(headerControl : TTeHeaderControl; section: TTeHeaderSection);
{$ENDIF}
      private
         fOnSelectVoicing : TNotifyEvent;
         procedure DoClick(Sender: TObject);
         procedure DoSimpleDrawGridResize(Sender: TObject);
         procedure UpdateChordName;
      protected
         procedure CreateWnd; override;
{$IFNDEF FreeVersion}
         procedure PopulateDetailColumns;
{$ENDIF}
         procedure PopulateList;
         procedure ChangeDifficultyImageList(sender : TObject); virtual;
         procedure ChangeDifficultySmallImageList(sender : TObject); virtual;
         procedure ChangeBrightnessImageList(sender : TObject); virtual;
         procedure ChangeSortDirectionImageList(sender : TObject); virtual;
      public
         constructor Create(AOwner: TComponent); override;
         destructor Destroy; override;
         procedure SetChordTemplate(root : THalfTone; chordTemplate : TChordTemplate; chordName : string = '');
         procedure SetFocus; override;
         property selectedVoicing : TChordVoicing read GetSelectedVoicing;
      published
         property Align;
         property root : THalfTone read GetRoot write SetRoot;
         property Detailed : boolean read fDetailed write SetDetailed;
         property Qualities : THalfToneQualities read GetQualities write SetQualities;
         property DifficultyImageList : TImageList read fDifficultyImageList write SetDifficultyImageList;
         property DifficultySmallImageList : TImageList read fDifficultySmallImageList write SetDifficultySmallImageList;
         property SortDirectionImageList : TImageList read fSortDirectionImageList write SetSortDirectionImageList;
         property MaxDifficulty : TChordVoicingDifficulty read fMaxDifficulty write SetMaxDifficulty;
         property VoicingFilter : TChordVoicingFilter read fVoicingFilter write SetVoicingFilter;
         property NoMutedStrings : boolean read fNoMutedStrings write SetNoMutedStrings;
         property NoOpenStrings : boolean read fNoOpenStrings write SetNoOpenStrings;
         property NoBarres : boolean read fNoBarres write SetNoBarres;
         property MaxFret : integer read fMaxFret write SetMaxFret;
         property OnSelectVoicing : TNotifyEvent read fOnSelectVoicing write fOnSelectVoicing;
         property DragKind;
         property DragMode;
         property OnDragOver;
         property OnStartDrag;
         property OnEndDrag;
   end;

implementation

uses
   uMusicFontRoutines
   ,contnrs
   ,SysUtils
   ,uMidi
   ,uTeChordTemplateBox
   ;

const
   DOT_BMP_INDEX = 0;
   OPEN_BMP_INDEX = 1;
   MUTED_BMP_INDEX = 2;

{ TTeVoicingDictionnary }

constructor TTeVoicingDictionnary.Create(AOwner: TComponent);
begin
   inherited Create(aOwner);
   ControlStyle := [csCaptureMouse, csClickEvents, csOpaque, csReplicatable];
   SetBounds(0, 0, 300, 350);
   fMiniGuitarChord := TMiniGuitarChord.Create(self);
   fMiniGuitarChord.top := 10000;
   fMiniGuitarChord.visible := false;
   fMiniGuitarChord.parent := self;
   fMenuPanel := TTePanel.Create(self);
   fMenuPanel.parent := self;
   fMenuPanel.align := alTop;
   fChordName := TLabel.Create(self);
   fChordName.transparent := true;
   fChordName.parent := fMenuPanel;
   fChordName.left := 5;
   fNumberOfChords := TLabel.Create(self);
   fNumberOfChords.transparent := true;
   fNumberOfChords.parent := fMenuPanel;
   fNumberOfChords.align := alRight;
   fPageControl := TTePageControl.Create(self);
   fPageControl.parent := self;
   fPageControl.align := alClient;
   fSimplePage := TTeTabSheet.Create(fPageControl);
   fSimplePage.parent := fPageControl;
   fSimplePage.pageVisible := false;
{$IFNDEF FreeVersion}
   fDetailedPage := TTeTabSheet.Create(fPageControl);
   fDetailedPage.parent := fPageControl;
   fDetailedPage.pageVisible := false;
   fHeaderControl := TTeHeaderControl.Create(fDetailedPage);
   fHeaderControl.parent := fDetailedPage;
   fHeaderControl.font.name := 'tahoma';
   fHeaderControl.font.size := 8;
   fHeaderControl.hint := 'Reorder the columns for changing the sorting order / Click on a column for changing the sorting direction';   
   fHeaderControl.dragReorder := true;
   fHeaderControl.OnSectionResize := DoHeaderSectionResize;
   fHeaderControl.OnSectionDrag := DoHeaderSectionDrag;
   fHeaderControl.OnSectionEndDrag := DoHeaderSectionEndDrag;
   fHeaderControl.OnSectionClick := DoHeaderSectionClick;
   fDetailedDrawGrid := TTeDrawGridEx.Create(fDetailedPage);
   fDetailedDrawGrid.parent := fDetailedPage;
   fDetailedDrawGrid.scrollBars := ssVertical;
   fDetailedDrawGrid.OnClick := DoClick;
{$ENDIF}
   fSimpleDrawGrid := TTeDrawGridEx.Create(fSimplePage);
   fSimpleDrawGrid.parent := fSimplePage;
   fSimpleDrawGrid.align := alClient;
   fSimpleDrawGrid.scrollBars := ssVertical;
   fSimpleDrawGrid.OnResize := DoSimpleDrawGridResize;
   fSimpleDrawGrid.OnClick := DoClick;
   fPageControl.activePage := fSimplePage;

   fDetailed := false;
   fAddColumns := true;
   fComputeContent := true;
   fColorDegrees := false;
   fChordVoicingCollection := TChordVoicingCollection.Create;
   fMaxDifficulty := cvdHard;
   fVoicingFilter := cvfAny;
   fMaxFret := 15;
   fNoMutedStrings := false;
   fNoOpenStrings := false;
   fNoBarres := false;
   fDifficultyImageList := nil;
   fDifficultySmallImageList := nil;   
   fSortDirectionImageList := nil;
   fSimpleDrawGrid.DefaultRowHeight := fMiniGuitarChord.height + 4;
   fSimpleDrawGrid.DefaultColWidth := fMiniGuitarChord.width + 4;
   fSimpleDrawGrid.options := [goDrawFocusSelected, goTabs, goThumbTracking{, goHorzLine, goVertLine}];
   fSimpleDrawGrid.fixedCols := 0;
   fSimpleDrawGrid.fixedRows := 0;
   fSimpleDrawGrid.colCount := 0;
   fSimpleDrawGrid.rowCount := 0;
   fSimpleDrawGrid.OnDrawCell := DoSimpleDrawCell;

{$IFNDEF FreeVersion}
   fDetailedDrawGrid.DefaultRowHeight := fMiniGuitarChord.height;
   fDetailedDrawGrid.options := fDetailedDrawGrid.options + [goRowSelect] - [goRangeSelect];
   fDetailedDrawGrid.fixedCols := 0;
   fDetailedDrawGrid.fixedRows := 0;
   fDetailedDrawGrid.OnDrawCell := DoDetailedDrawCell;
{$ENDIF}

   if HALFTONE_IS_SHARP_KEY[fChordVoicingCollection.root]
      then fChordTemplateName := HALFTONE_NAME_SHARP[fChordVoicingCollection.root]
      else fChordTemplateName := HALFTONE_NAME_FLAT[fChordVoicingCollection.root];

   fChordName.caption := fChordTemplateName;

   fOnSelectVoicing := nil;
end;

destructor TTeVoicingDictionnary.Destroy;
begin
   if fDifficultyImageList <> nil
      then fDifficultyImageList.onChange := nil;
   if fDifficultySmallImageList <> nil
      then fDifficultySmallImageList.onChange := nil;
   if fSortDirectionImageList <> nil
      then fSortDirectionImageList.onChange := nil;
   FreeAndNil(fMiniGuitarChord);
   FreeAndNil(fChordVoicingCollection);
{$IFNDEF FreeVersion}
   FreeAndNil(fDetailedDrawGrid);
   FreeAndNil(fHeaderControl);
{$ENDIF}   
   FreeAndNil(fChordName);
   FreeAndNil(fMenuPanel);
   FreeAndNil(fPageControl);
   inherited;
end;

procedure TTeVoicingDictionnary.SetChordTemplate(root : THalfTone; chordTemplate : TChordTemplate; chordName : string);
begin
   if (chordTemplate <> nil)
      and ((root <> fChordVoicingCollection.root) or (chordTemplate.qualities <> fChordVoicingCollection.qualities))
      then begin
              fSettingChordTemplate := true;
              try
                 fChordTemplateName := chordName;
                 fChordName.caption := chordName;
                 fChordVoicingCollection.root := root;
                 fChordVoicingCollection.qualities := chordTemplate.qualities;
                 fSimpleDrawGrid.Col := 0;
                 fSimpleDrawGrid.Row := 0;
                 fSimpleDrawGrid.SelectCell(0, 0);
                 fSimpleDrawGrid.FocusCell(0, 0, true);
{$IFNDEF FreeVersion}
                 fDetailedDrawGrid.Col := 0;
                 fDetailedDrawGrid.Row := 0;
                 fDetailedDrawGrid.SelectCell(0, 0);
                 fDetailedDrawGrid.FocusCell(0, 0, true);
{$ENDIF}
                 PopulateList;
              finally
                 fSettingChordTemplate := false;
              end;
           end;
end;

{$IFNDEF FreeVersion}
procedure TTeVoicingDictionnary.DoHeaderSectionResize(headerControl : TTeHeaderControl; section : TTeHeaderSection);
begin
   RefreshColumnWidths;
   fDetailedDrawGrid.colWidths[section.index] := section.Width - 2
end;

procedure TTeVoicingDictionnary.DoHeaderSectionDrag(sender : TObject; fromSection, toSection : TTeHeaderSection; var allowDrag : boolean);
var
   sortOrder : TStringList;
   sectionIndex1, sectionIndex2 : integer;
   voicingProperty1, voicingProperty2 : TChordVoicingProperty;
   temp : integer;
begin
   allowDrag := false;
   sortOrder := fChordVoicingCollection.sortOrder;
   sectionIndex1 := sortOrder.IndexOf(fromSection.text);
   sectionIndex2 := sortOrder.IndexOf(toSection.text);
   if (sectionIndex1 = -1) or (sectionIndex1 = -2)
      then Exit;

   Assert(fChordVoicingCollection.sortOrder.objects[sectionIndex1] is TChordVoicingProperty);
   Assert(fChordVoicingCollection.sortOrder.objects[sectionIndex2] is TChordVoicingProperty);
   voicingProperty1 := TChordVoicingProperty(fChordVoicingCollection.sortOrder.objects[sectionIndex1]);
   voicingProperty2 := TChordVoicingProperty(fChordVoicingCollection.sortOrder.objects[sectionIndex2]);

   // Maybe apply the drag
   allowDrag := (sectionIndex1 <> sectionIndex2)
                 and (voicingProperty1.movable and voicingProperty2.movable);
   if allowDrag
      then begin
              // Reorder
              temp := fDetailedDrawGrid.ColWidths[sectionIndex1];
              fDetailedDrawGrid.ColWidths[sectionIndex1] := fDetailedDrawGrid.ColWidths[sectionIndex2];
              fDetailedDrawGrid.ColWidths[sectionIndex2] := temp;
              fChordVoicingCollection.sortOrder.Move(sectionIndex1, sectionIndex2);

              // Sort and redraw the grid
              fChordVoicingCollection.SortVoicings;
              fDetailedDrawGrid.Invalidate;
           end;
end;

procedure TTeVoicingDictionnary.DoHeaderSectionEndDrag(sender : TObject);
begin
{$IFNDEF FreeVersion}
   RefreshColumnWidths;
{$ENDIF}   
end;

procedure TTeVoicingDictionnary.DoHeaderSectionClick(headerControl : TTeHeaderControl; section: TTeHeaderSection);
var
   index : integer;
   voicingProperty : TChordVoicingProperty;
begin
   // Don't sort if voicing column clicked or no column clicked
   index := fChordVoicingCollection.sortOrder.IndexOf(section.text);
   if index < 1
      then Exit;

   // Reverse the order for the clicked property
   Assert(fChordVoicingCollection.sortOrder.objects[index] is TChordVoicingProperty);
   voicingProperty := TChordVoicingProperty(fChordVoicingCollection.sortOrder.objects[index]);
   if not (voicingProperty is TChordVoicing_Voicing)
      then begin
              voicingProperty.reverseOrder := not voicingProperty.reverseOrder;
              if voicingProperty.reverseOrder
                 then section.imageIndex := 1
                 else section.imageIndex := 0;
           end;            

   // Sort and redraw the grid
   fChordVoicingCollection.SortVoicings;
   fDetailedDrawGrid.Invalidate;
end;
{$ENDIF}

procedure TTeVoicingDictionnary.DoClick(Sender: TObject);
{$IFNDEF FreeVersion}
var
   row : integer;
{$ENDIF}   
begin
   if fComputeContent or fSettingChordTemplate
      then Exit;

   // Synchronize grid positions
{$IFNDEF FreeVersion}
   if fDetailed
      then // Detailed grid
           begin
              // Synchronize the selected cell on the simple grid
              fSimpleDrawGrid.Row := fDetailedDrawGrid.row div fSimpleDrawGrid.ColCount;
              fSimpleDrawGrid.Col := fDetailedDrawGrid.row mod fSimpleDrawGrid.ColCount;
           end
      else // Simple grid
           begin
              // Synchronize the selected cell on the detailed grid
             row := fSimpleDrawGrid.Row * fSimpleDrawGrid.ColCount + fSimpleDrawGrid.col;
             if row < fDetailedDrawGrid.RowCount
                then fDetailedDrawGrid.Row := row;
           end;
{$ENDIF}

   // Update the selected chord name (Inversions)
   UpdateChordName;

   // Maybe play the selected chord
   if selectedVoicing <> nil
      then globalMidi.PlayChordVoicing(selectedVoicing);

   // Maybe fire OnSelectVoicing
   if Assigned(fOnSelectVoicing)
      then onSelectVoicing(self);
end;

procedure TTeVoicingDictionnary.DoSimpleDrawGridResize(Sender : TObject);
var
   colCount : integer;
   rowCount : integer;
begin
   colCount := (width div fMiniGuitarChord.width) - 1;
   if colCount <> fSimpleDrawGrid.colCount
      then fSimpleDrawGrid.colCount := colCount
      else begin
              if fChordVoicingCollection.count mod colCount > 0
                 then rowCount := fChordVoicingCollection.count div colCount + 1
                 else rowCount := fChordVoicingCollection.count div colCount;
              if fSimpleDrawGrid.rowCount <> rowCount
                 then fSimpleDrawGrid.rowCount := rowCount;
           end;
end;

{$IFNDEF FreeVersion}
procedure TTeVoicingDictionnary.PaintVoicingListGridCell(voicing : TChordVoicing; sectionText : string; aCol, aRow : integer;
                                                         canvas : TCanvas; rect : TRect; state : TGridDrawState);
var
   headerSection : TTeHeaderSection;
   sortOrder : TStringList;
   voicingProperty : TChordVoicingProperty;
   index : integer;
   s : string;
   c : integer;
   bitmap : graphics.TBitmap;
begin
   sortOrder := voicing.collection.sortOrder;
   index := sortOrder.IndexOf(sectionText);
   if index = -1
      then Exit;

   // Draw the cell
   Assert(sortOrder.objects[index] is TChordVoicingProperty);
   headerSection := fHeaderControl.Sections[aCol];
   voicingProperty := TChordVoicingProperty(sortOrder.objects[index]);
   Assert(voicingProperty <> nil);
   canvas.font.style := [];
   if not (gdSelected in state)
      then canvas.brush.color := clWhite;

        if voicingProperty is TChordVoicing_Voicing
           then begin
                   fMiniGuitarChord.voicing := voicing;
                   fMiniGuitarChord.PaintTo(canvas, rect.Left, rect.top);
{$IFDEF DebugVoicingComputing}
                   s := IntToStr(voicing.voicingId);
                   canvas.TextRect(rect, rect.left, rect.bottom - canvas.TextHeight(s), s);
{$ENDIF}
                end
   else if voicingProperty is TChordVoicing_Difficulty
           then begin
{$IFDEF DebugVoicingComputing}
                   canvas.TextRect(rect, rect.left + 20, rect.top, IntToStr(voicing.score));
                   s := VOICING_DIFFICULTY[voicing.difficulty];
                   canvas.TextOut(rect.left, rect.top + 16, s);
{$ENDIF}
                   if fDifficultyImageList <> nil
                      then begin
                              bitmap := graphics.TBitmap.Create;
                              try
                                 fDifficultyImageList.GetBitmap(Ord(voicing.difficulty), bitmap);
                                 bitmap.transparentMode := tmAuto;
                                 bitmap.transparent := true;
                                 canvas.Draw(rect.left, rect.top, bitmap);
                              finally
                                 bitmap.Free;
                              end;
                           end;
                end
   else if voicingProperty is TChordVoicing_Fret
           then begin
                   s := IntToStr(voicing.minFret);
                   if ((voicing.minFret div 10) <> 1)
                      then begin
                                   if (voicing.minFret mod 10) = 1
                                      then s := s + 'st'
                              else if (voicing.minFret mod 10) = 2
                                      then s := s + 'nd'
                              else if (voicing.minFret mod 10) = 3
                                      then s := s + 'rd'
                              else         s := s + 'th';
                           end
                      else s := s + 'th';
                   canvas.TextRect(rect, rect.left, rect.top, s);
                end
   else if voicingProperty is TChordVoicing_Inversion
           then begin
                   if voicing.inversion = 0
                      then s := 'Root'
                      else begin
                              s:= IntToStr(voicing.inversion);
                                   if (voicing.inversion mod 10) = 1
                                      then s := s + 'st'
                              else if (voicing.inversion mod 10) = 2
                                      then s := s + 'nd'
                              else if (voicing.inversion mod 10) = 3
                                      then s := s + 'rd'
                              else         s := s + 'th';
                              s := s + ' inv.';
                           end;
                   canvas.TextRect(rect, rect.left, rect.top, s);
                   if voicing.inversion <> 0
                      then begin
                              if HALFTONE_IS_SHARP_KEY[voicing.inversionNote]
                                 then s := '/' + HALFTONE_NAME_SHARP[voicing.inversionNote]
                                 else s := '/' + HALFTONE_NAME_FLAT[voicing.inversionNote];
                                 canvas.TextOut(rect.left, rect.top + 16, s);
                           end;
                end
   else if voicingProperty is TChordVoicing_MutedStrings
           then begin
                   canvas.font.name := STAFF_FONT_NAME;
                   canvas.font.size := 5;
                   s := StringOfChar(MUTED_POSITION, voicing.mutedStrings);
                   canvas.TextRect(rect, rect.left, rect.top - 2, s);
                end
   else if voicingProperty is TChordVoicing_OpenString
           then begin
                   canvas.font.name := STAFF_FONT_NAME;
                   canvas.font.size := 5;
                   s := StringOfChar(OPEN_POSITION, voicing.openStrings);
                   canvas.TextRect(rect, rect.left, rect.top - 2, s);
                end
   else if voicingProperty is TChordVoicing_Strings
           then begin
                   canvas.TextRect(rect, rect.left, rect.top, IntToStr(voicing.stringCount));
                end
   else if voicingProperty is TChordVoicing_Fingers
           then begin
                   canvas.TextRect(rect, rect.left, rect.top, IntToStr(voicing.fingerCount));
                end
   else if voicingProperty is TChordVoicing_Brightness
           then begin
                   s := VOICING_BRIGHTNESS_NAMES[voicing.brightness];
                   canvas.TextOut(rect.left, rect.top + 16, s);

                   c := Round(255 * (Ord(voicing.brightness)) / (Ord(High(TChordVoicingBrightness))));
                   canvas.pen.color := clBlack;
                   canvas.brush.color := RGB(c, c, 0);
                   canvas.Rectangle(classes.Rect(rect.left + 2, rect.top + 2 , rect.left + headerSection.width - 4, rect.top + 10));
                end
   else if voicingProperty is TChordVoicing_Contrast
           then begin
                   s := VOICING_CONTRAST_NAMES[voicing.contrast];
                   canvas.TextOut(rect.left, rect.top + 16, s);

                   c := 127 - Round(127 * (Ord(voicing.contrast)) / (Ord(High(TChordVoicingContrast))));
                   canvas.brush.color := RGB(0, c, c div 2);
                   canvas.FillRect(classes.Rect(rect.left + 2, rect.top + 2 , rect.left + headerSection.width div 2, rect.top + 10));

                   c := 127 + Round(127 * (Ord(voicing.contrast)) / (Ord(High(TChordVoicingContrast))));
                   canvas.brush.color := RGB(0, c, c div 2);
                   canvas.FillRect(classes.Rect(rect.left + headerSection.width div 2, rect.top + 2 , rect.left + headerSection.width - 4 , rect.top + 10));

                   canvas.pen.color := clBlack;
                   canvas.brush.style := bsClear;
                   canvas.Rectangle(classes.Rect(rect.left + 2, rect.top + 2 , rect.left + headerSection.width - 4, rect.top + 10));
                   canvas.brush.style := bsSolid;                   
                end;
end;
{$ENDIF}

procedure TTeVoicingDictionnary.SetDifficultyImageList(value : TImageList);
begin
   if value <> fDifficultyImageList
      then begin
              fDifficultyImageList := value;
              if fDifficultyImageList <> nil
                 then fDifficultyImageList.onChange := ChangeDifficultyImageList;
              Invalidate;
           end;
end;

procedure TTeVoicingDictionnary.SetDifficultySmallImageList(value : TImageList);
begin
   if value <> fDifficultySmallImageList
      then begin
              fDifficultySmallImageList := value;
              if fDifficultySmallImageList <> nil
                 then fDifficultySmallImageList.onChange := ChangeDifficultySmallImageList;
              Invalidate;
           end;
end;

procedure TTeVoicingDictionnary.SetSortDirectionImageList(value : TImageList);
begin
   if value <> fSortDirectionImageList
      then begin
              fSortDirectionImageList := value;
{$IFNDEF FreeVersion}
              fHeaderControl.Images := fSortDirectionImageList;
{$ENDIF}              
              if fSortDirectionImageList <> nil
                 then fSortDirectionImageList.onChange := ChangeSortDirectionImageList;
           end;
end;

procedure TTeVoicingDictionnary.SetMaxDifficulty(value : TChordVoicingDifficulty);
begin
   if value <> fMaxDifficulty
      then begin
              fMaxDifficulty := value;
              PopulateList;
           end;
end;

procedure TTeVoicingDictionnary.SetVoicingFilter(value : TChordVoicingFilter);
begin
   if value <> fVoicingFilter
      then begin
              fVoicingFilter := value;
              PopulateList;
           end;
end;

procedure TTeVoicingDictionnary.SetNoMutedStrings(value : boolean);
begin
   if value <> fNoMutedStrings
      then begin
              fNoMutedStrings := value;
              PopulateList;
           end;
end;

procedure TTeVoicingDictionnary.SetNoOpenStrings(value : boolean);
begin
   if value <> fNoOpenStrings
      then begin
              fNoOpenStrings := value;
              PopulateList;
           end;
end;

procedure TTeVoicingDictionnary.SetNoBarres(value : boolean);
begin
   if value <> fNoBarres
      then begin
              fNoBarres := value;
              PopulateList;
           end;
end;

procedure TTeVoicingDictionnary.SetMaxFret(value : integer);
begin
   if value <> fMaxFret
      then begin
              fMaxFret := value;
              PopulateList;
           end;
end;

{$IFNDEF FreeVersion}
procedure TTeVoicingDictionnary.RefreshColumnWidths;
var
   index : integer;
   section : TTeHeaderSection;
begin
   for index := 0 to fHeaderControl.sections.count - 1 do
      begin
         section := fHeaderControl.sections[index];
         fDetailedDrawGrid.colWidths[index] := ((section.width) div 2) * 2 - 1;
      end;
end;
{$ENDIF}

function TTeVoicingDictionnary.GetRoot : THalfTone;
begin
   result := fChordVoicingCollection.root;
end;

function TTeVoicingDictionnary.GetQualities : THalfToneQualities;
begin
   result := fChordVoicingCollection.qualities;
end;

function TTeVoicingDictionnary.GetSelectedVoicing : TChordVoicing;
begin
{$IFNDEF FreeVersion}
   if fDetailed
      then begin
              if fDetailedDrawGrid.row = -1
                 then result := nil
                 else result := fChordVoicingCollection.items[fDetailedDrawGrid.row];
           end
      else
{$ENDIF}      
           result := fChordVoicingCollection.items[fSimpleDrawGrid.Row * fSimpleDrawGrid.ColCount + fSimpleDrawGrid.col];
end;

procedure TTeVoicingDictionnary.SetRoot(value : THalfTone);
begin
   if value <> fChordVoicingCollection.root
      then begin
              fChordVoicingCollection.root := value;
              PopulateList;
           end;
end;

procedure TTeVoicingDictionnary.SetDetailed(value : boolean);
begin
   if value <> fDetailed
      then begin
              fDetailed := value;
{$IFNDEF FreeVersion}
              if value
                 then fPageControl.ActivePage := fDetailedPage
                 else fPageControl.ActivePage := fSimplePage
{$ENDIF}
           end;
end;

procedure TTeVoicingDictionnary.SetQualities(value : THalfToneQualities);
begin
   if value <> fChordVoicingCollection.qualities
      then begin
              fChordVoicingCollection.qualities := value;
              PopulateList;
           end;
end;

{$IFNDEF FreeVersion}
procedure TTeVoicingDictionnary.PopulateDetailColumns;
var
   index : integer;
   voicingProperty : TChordVoicingProperty;
   headerSection : TTeHeaderSection;
begin
   // Add the columns
   fDetailedDrawGrid.colCount := 1;
   for index := 0 to fChordVoicingCollection.sortOrder.count - 1 do
      begin
         // Retrieve the property
         Assert(fChordVoicingCollection.sortOrder.objects[index] is TChordVoicingProperty);
         voicingProperty := TChordVoicingProperty(fChordVoicingCollection.sortOrder.objects[index]);

         // Assign and index to the column
         headerSection := fHeaderControl.Sections.Add;
         headerSection.spacing := 0;
         headerSection.layout := hglGlyphRight;
         headerSection.margin := 1;
         headerSection.text := voicingProperty.caption;

         // Set the header section dimensions
              if index = 0
                 then headerSection.width := fMiniGuitarChord.width + 1
         else if voicingProperty.minWidth = -1
                 then headerSection.width := 60
         else         headerSection.width := voicingProperty.minWidth;
         if voicingProperty.minWidth = -1
            then headerSection.minWidth := headerSection.width
            else headerSection.minWidth := voicingProperty.minWidth;
         if voicingProperty.maxWidth = -1
            then headerSection.maxWidth := headerSection.width
            else headerSection.maxWidth := voicingProperty.maxWidth;

         // Set the sorting image for the header section
         if index = 0
            then // No direction for the mini guitar chord
                 headerSection.imageIndex := -1
            else // Show up/down image
                 begin
                    if voicingProperty.reverseOrder
                       then headerSection.imageIndex := 1
                       else headerSection.imageIndex := 0;
                 end;
      end;

   fDetailedDrawGrid.colCount := fChordVoicingCollection.sortOrder.count;

   RefreshColumnWidths;
   fAddColumns := false;
end;
{$ENDIF}

procedure TTeVoicingDictionnary.PopulateList;
begin
   if not(csReading in ComponentState)
      then begin
              if HandleAllocated
                 then begin
                         // Recompute the chord voicing collection
                         cursor := crHourGlass;
                         try
                            fSimpleDrawGrid.RowCount := 0;
                            fSimpleDrawGrid.ColCount := 0;
{$IFNDEF FreeVersion}
                            fDetailedDrawGrid.RowCount := 0;
{$ENDIF}
                            fChordVoicingCollection.maxDifficulty := fMaxDifficulty;
                            fChordVoicingCollection.maxFret := fMaxFret;
                            fChordVoicingCollection.noMutedStrings := fNoMutedStrings;
                            fChordVoicingCollection.noOpenStrings := fNoOpenStrings;
                            fChordVoicingCollection.noBarres := fNoBarres;
                            fChordVoicingCollection.voicingFilter := fVoicingFilter;
                            fChordVoicingCollection.ComputeVoicings;
                            fChordVoicingCollection.SortVoicings;
                         finally
                            cursor := crDefault;
                         end;

                         // Update the selected chord name
                         UpdateChordName;

                         // Simple grid
                         fSimpleDrawGrid.colCount := (width div fMiniGuitarChord.width) - 1;
                         if fChordVoicingCollection.count mod fSimpleDrawGrid.colCount > 0
                            then fSimpleDrawGrid.rowCount := fChordVoicingCollection.count div fSimpleDrawGrid.colCount + 1
                            else fSimpleDrawGrid.rowCount := fChordVoicingCollection.count div fSimpleDrawGrid.colCount;
                         fSimpleDrawGrid.Update;
                         fSimpleDrawGrid.Row := 0;
                         fSimpleDrawGrid.Col := 0;
                         fSimpleDrawGrid.SelectCell(0, 0);

{$IFNDEF FreeVersion}
                         // Detailed grid
                         fDetailedDrawGrid.rowCount := fChordVoicingCollection.count;
                         fDetailedDrawGrid.Row := 0;
                         fDetailedDrawGrid.SelectCell(0, 0);
                         fDetailedDrawGrid.Invalidate;
{$ENDIF}

                         // Update the number of chords label
                         if fChordVoicingCollection.count = 0
                            then fNumberOfChords.caption := 'No chords found'
                            else fNumberOfChords.caption := IntToStr(fChordVoicingCollection.count) + ' chords found';

                         // The content is now computed
                         fComputeContent := false;
                      end
                 else // The content cannot yet be computed
                      fComputeContent := true;
           end;
end;

procedure TTeVoicingDictionnary.ChangeDifficultyImageList(sender : TObject);
begin
{$IFNDEF FreeVersion}
   if not (csDestroying in ComponentState)
      then fDetailedDrawGrid.Invalidate;
{$ENDIF}
end;

procedure TTeVoicingDictionnary.ChangeBrightnessImageList(sender : TObject);
begin
{$IFNDEF FreeVersion}
   if not (csDestroying in ComponentState)
      then fDetailedDrawGrid.Invalidate;
{$ENDIF}
end;

procedure TTeVoicingDictionnary.ChangeSortDirectionImageList(sender : TObject);
begin
{$IFNDEF FreeVersion}
   if not (csDestroying in ComponentState)
      then fHeaderControl.Invalidate;
{$ENDIF}
end;

procedure TTeVoicingDictionnary.CreateWnd;
begin
   inherited CreateWnd;
   fSimplePage.TabControl := fPageControl;
   fMenuPanel.height := 28;
   fChordName.font.name := CHORDS_FONT_NAME;
   fChordName.font.style := [];
   fChordName.font.size := 16;
{$IFNDEF FreeVersion}
   fDetailedPage.TabControl := fPageControl;
   fHeaderControl.align := alTop;
   fDetailedDrawGrid.align := alClient;
   if fAddColumns
      then PopulateDetailColumns;
{$ENDIF}
   if fComputeContent
      then PopulateList;
end;

procedure TTeVoicingDictionnary.SetFocus;
begin
{$IFNDEF FreeVersion}
   if fDetailed
      then fDetailedDrawGrid.SetFocus
      else
{$ENDIF}      
           fSimpleDrawGrid.SetFocus;
end;

procedure TTeVoicingDictionnary.DoSimpleDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
var
   voicing : TChordVoicing;
   bitmap : graphics.TBitmap;
begin
   if fChordVoicingCollection.count = 0
      then Exit;

   voicing := fChordVoicingCollection.items[aRow * fSimpleDrawGrid.ColCount + aCol];
   if voicing = nil
      then Exit;

   fMiniGuitarChord.voicing := voicing;
   fMiniGuitarChord.PaintTo(fSimpleDrawGrid.canvas, rect.Left + 2, rect.top + 2);

   if fDifficultySmallImageList <> nil
      then begin
              bitmap := graphics.TBitmap.Create;
              try
                 fDifficultySmallImageList.GetBitmap(Ord(voicing.difficulty), bitmap);
                 bitmap.transparentMode := tmAuto;
                 bitmap.transparent := true;
                 fSimpleDrawGrid.canvas.Draw(rect.right - bitmap.width - 13, rect.top + 22, bitmap);
              finally
                 bitmap.Free;
              end;
           end;
end;

{$IFNDEF FreeVersion}
procedure TTeVoicingDictionnary.DoDetailedDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
var
   voicing : TChordVoicing;
   canvas : TCanvas;
   sectionText : string;
begin
   if fChordVoicingCollection.count = 0
      then Exit;

   voicing := fChordVoicingCollection.items[aRow];
   if voicing = nil
      then Exit;

   canvas := fDetailedDrawGrid.canvas;
   canvas.font.name := 'Tahoma';
   canvas.font.size := 8;
   if gdSelected in state
      then canvas.font.Color := clWhite
      else canvas.font.Color := clBlack;

   if aCol < fHeaderControl.Sections.count
      then begin
              sectionText := fHeaderControl.Sections[aCol].text;
              PaintVoicingListGridCell(voicing, sectionText, aCol, aRow, canvas, rect, state);
           end;
end;
{$ENDIF}

procedure TTeVoicingDictionnary.UpdateChordName;
var
   chordName : string;
begin
   if selectedVoicing = nil
      then Exit;

   // Maybe write slash chord
   if selectedVoicing.inversion > 0
      then // Chord inversion
           begin
              chordName := fChordTemplateName + SLASH_CHORD_CHAR;
              if HALFTONE_IS_SHARP_KEY[selectedVoicing.inversionNote]
                 then chordName := chordName + HALFTONE_NAME_SHARP[selectedVoicing.inversionNote]
                 else chordName := chordName + HALFTONE_NAME_FLAT[selectedVoicing.inversionNote];
           end
      else // Root position
           chordName := fChordTemplateName;

   // Change the chord name
   fChordName.caption := chordName;
end;

procedure TTeVoicingDictionnary.ChangeDifficultySmallImageList(sender : TObject);
begin
   if not (csDestroying in ComponentState)
      then fSimpleDrawGrid.Invalidate;
end;

{ TMiniGuitarChord }

constructor TMiniGuitarChord.Create(aOwner : TComponent);
var
   index : integer;
begin
   inherited Create(aOwner);
   SetBounds(left, top, GetStringX(5) + RIGHT_OFFSET, GetFretY(5) + 1);
   ControlStyle := [csOpaque, csFixedWidth, csFixedHeight];
   fVoicing := nil;

   // Draw all dot kinds in memory
   fPosPics := TImageList.Create(nil);
   fDotBitmap := graphics.TBitmap.Create;
   fOpenBitmap := graphics.TBitmap.Create;
   fMutedBitmap := graphics.TBitmap.Create;
   fInitialized := false;   

   // All open positions
   for index := 0 to 5 do
      begin
         fPositions[index].fret := 0;
         fPositions[index].halfTone.halfTone := htC;
         fPositions[index].halfTone.octave := 0;
         fPositions[index].quality := htqUnison;
      end
end;

destructor TMiniGuitarChord.Destroy;
begin
   fPosPics.Free;
   fDotBitmap.Free;
   fOpenBitmap.Free;
   fMutedBitmap.Free;

   inherited;
end;

function TMiniGuitarChord.GetStringX(str : integer) : integer;
begin
   result := Round(STR_SPACE * (str + 0.5));
end;

function TMiniGuitarChord.GetFretY(fret : integer; dot : boolean) : integer;
begin
   result := TOP_OFFSET + FRET_SPACE * fret;
   if dot
      then result := result + Round(FRET_SPACE / 2);
end;

procedure TMiniGuitarChord.Paint;
var
   str, fret : integer;
   x, y, x1, x2, y1, y2 : integer;
   showNut : boolean;
   fretLabel : string;
   s : string;
   inversionSuffix : string;
   tm : TTextMetric;
   bmpIndex : integer;

   procedure DrawBarre;
   var
      fret : integer;
      xRight, xLeft : integer;
      x, y, w, h : integer;
   begin
      fret := fMinFret;
      if fMaxFret > 5
         then fret := 1;
      xRight := GetStringX(voicing.firstFingerBarreStartingStr);
      xLeft := GetStringX(voicing.firstFingerBarreStartingStr - voicing.firstFingerBarreExtent + 1);
      x := xLeft;
      w := xRight - xLeft;
      y := GetFretY(fret - 1) - 2;
      h := GetFretY(0);

      canvas.pen.width := 3;
      canvas.Arc(x, y, x + w, y + h,
                 x + w, y + h div 2, x, y + h div 2);
   end;

begin
   if voicing = nil
      then Exit;

   if not fInitialized
      then begin
              fDotBitmap.width := 16;
              fDotBitmap.height := 16;
              fDotBitmap.canvas.font.height := - 5;
              fDotBitmap.canvas.font.name := STAFF_FONT_NAME;
              fDotBitmap.canvas.brush.style := bsClear;
              fDotBitmap.canvas.brush.color := clWhite;
              fDotBitmap.canvas.FillRect(Rect(0, 0, 16, 16));              
              fDotBitmap.canvas.TextOut(0, 0, NORMAL_POSITION);
              GetTextMetrics(fDotBitmap.canvas.handle, tm);
              fDeltaXNormal := canvas.TextWidth(NORMAL_POSITION) div 2 - 1;
              fDeltaYNormal := tm.tmAscent;

              fOpenBitmap.width := 16;
              fOpenBitmap.height := 16;
              fOpenBitmap.canvas.font.height := - 5;
              fOpenBitmap.canvas.font.name := STAFF_FONT_NAME;
              fOpenBitmap.canvas.brush.style := bsClear;
              fOpenBitmap.canvas.brush.color := clWhite;
              fOpenBitmap.canvas.FillRect(Rect(0, 0, 16, 16));
              fOpenBitmap.canvas.TextOut(0, 0, OPEN_POSITION);
              GetTextMetrics(fDotBitmap.canvas.handle, tm);
              fDeltaXOpen := canvas.TextWidth(OPEN_POSITION) div 2 - 1;
              fDeltaYOpen := tm.tmAscent;

              fMutedBitmap.width := 16;
              fMutedBitmap.height := 16;
              fMutedBitmap.canvas.font.height := - (STR_SPACE - 1);
              fMutedBitmap.canvas.font.name := STAFF_FONT_NAME;
              fMutedBitmap.canvas.brush.style := bsClear;
              fMutedBitmap.canvas.brush.color := clWhite;
              fMutedBitmap.canvas.FillRect(Rect(0, 0, 16, 16));
              fMutedBitmap.canvas.TextOut(0, 0, MUTED_POSITION);
              GetTextMetrics(fDotBitmap.canvas.handle, tm);
              fDeltaXMuted := canvas.TextWidth(MUTED_POSITION) div 2 - 1;
              fDeltaYMuted := tm.tmAscent;

              fPosPics.AddMasked(fDotBitmap, clWhite);
              fPosPics.AddMasked(fOpenBitmap, clWhite);
              fPosPics.AddMasked(fMutedBitmap, clWhite);

              fInitialized := true;
           end;

   canvas.brush.style := bsClear;
   canvas.brush.color := clWhite;
   canvas.FillRect(ClientRect);
   canvas.pen.style := psSolid;
   canvas.pen.mode := pmCopy;
   canvas.pen.color := clBlack;
   canvas.pen.width := 1;

   showNut := fMaxFret <= 5;

   // Draw strings
   for str := 0 to 5 do
      begin
         x := GetStringX(str);
         y1 := GetFretY(0);
         y2 := GetFretY(5);
         canvas.MoveTo(x, y1);
         canvas.LineTo(x, y2);
      end;

   // Draw frets
   for fret := 0 to 5 do
      begin
         x1 := GetStringX(0);
         x2 := GetStringX(5) + 1;
         y := GetFretY(fret);
         canvas.MoveTo(x1, y);
         canvas.LineTo(x2, y);
      end;

   // Maybe draw nut
   if showNut
      then begin
              x1 := GetStringX(0);
              x2 := GetStringX(5) + 1;
              y1 := GetFretY(0);
              y2 := y1 + 2;
              canvas.brush.style := bsSolid;
              canvas.brush.color := clBlack;
              canvas.FillRect(Rect(x1, y1, x2, y2));
           end;

   // Draw dots
   canvas.brush.style := bsClear;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := - (STR_SPACE - 1);
   canvas.font.style := [];
   canvas.font.color := clBlack;
   GetTextMetrics(canvas.Handle, tm);

      for str := 0 to 5 do
         begin
            fret := fPositions[str].fret;
            case fret of
               -1:
                  begin
                     x := GetStringX(str);
                     y := GetFretY(-1, true);
                     Dec(x, fDeltaXMuted);
                     Dec(y, fDeltaYMuted);
                     bmpIndex := MUTED_BMP_INDEX;
                  end;
               0:
                  begin
                     x := GetStringX(str);
                     y := GetFretY(-1, true);
                     Dec(x, fDeltaXOpen);
                     Dec(y, fDeltaYOpen);
                     bmpIndex := OPEN_BMP_INDEX;
                  end;
               else
                  begin
                     if fMaxFret > 5
                        then fret := fret - fMinFret + 1;
                     x := GetStringX(str);
                     y := GetFretY(fret - 1, true);
                     Dec(x, fDeltaXNormal);
                     Dec(y, fDeltaYNormal);
                     bmpIndex := DOT_BMP_INDEX;
                  end;
            end;
            canvas.brush.color := clWhite;
            fPosPics.Draw(canvas, x, y, bmpIndex);
         end;

   // Maybe draw the first finger barre
   if voicing.firstFingerBarreExtent > 0
      then DrawBarre;

   // Maybe draw the fret label
   canvas.font.name := 'verdana';
   canvas.font.size := 6;
   canvas.font.color := clBlack;
   if fMaxFret > 5
      then begin
              fretLabel := IntToStr(fMinFret);
              x := GetStringX(6);
              y := GetFretY(0);
              canvas.font.style := [fsBold];
              canvas.TextOut(x, y, fretLabel);
           end;

   // Maybe draw the inversion
   if voicing.inversion <> 0
      then begin
              x := GetStringX(6);
              y := GetFretY(0) + 17;
              canvas.font.style := [fsItalic, fsBold];
              canvas.font.color := clDkGray;

                   if voicing.inversion = 1
                      then inversionSuffix := 'st'
              else if voicing.inversion = 2
                      then inversionSuffix := 'nd'
              else if voicing.inversion = 2
                      then inversionSuffix := 'rd'
              else         inversionSuffix := 'th';
              s := IntToStr(voicing.inversion) + inversionSuffix;
              canvas.TextOut(x, y, s);

              Inc(y, 10);
              if HALFTONE_IS_SHARP_KEY[voicing.inversionNote]
                 then s := '/' + HALFTONE_NAME_SHARP[voicing.inversionNote]
                 else s := '/' + HALFTONE_NAME_FLAT[voicing.inversionNote];
              canvas.TextOut(x, y, s);
           end;


{$IFDEF DebugVoicingsComputing}
   if fVoicing.eliminated
      then begin
              canvas.pen.color := clRed;
              canvas.pen.Width := 1;
              canvas.MoveTo(0, 0);
              canvas.LineTo(ClientRect.Right, ClientRect.Bottom);
           end;
{$ENDIF}
end;

procedure TMiniGuitarChord.SetVoicing(value : TChordVoicing);
var
   str : integer;
   fret : integer;
begin
   fVoicing := value;
   fPositions := value.positions;

   // Compute the min and max fret
   fMinFret := MaxInt;
   fMaxFret := 0;
   for str := 0 to 5 do
      begin
         fret := fPositions[str].fret;
         if (fret > 0) and (fret < fMinFret)
            then fMinFret := fret;
         if fret > fMaxFret
            then fMaxFret := fret;
      end;
   Invalidate;
end;


end.
