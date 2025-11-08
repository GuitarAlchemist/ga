unit uSeVoicingListView;

interface

uses
   StdCtrls
   ,Classes
   ,Controls
   ,Graphics
   ,Windows
   ,ComCtrls
   ,uMusicClasses
   ,se_controls
   ,ksskinstdcontrol
   ,KsSkinGrids
   ,uChordVoicings
   ,ksskinheader
   ,KsSkinEngine
   ,Grids
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
         fFret : integer;
         fFretLabelPosition : integer;
         fColorDegrees : boolean;
         procedure SetVoicing(value : TChordVoicing);
         procedure SetFret(value : integer);
         procedure SetFretLabelPosition(value : integer);
         procedure SetColorDegrees(value : boolean);
      protected
         function GetStringX(str : integer) : integer;
         function GetFretY(fret : integer; dot : boolean = false) : integer;
         procedure Paint; override;
      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
         property voicing : TChordVoicing read fVoicing write SetVoicing;
         property fret : integer read fFret write SetFret;
         property fretLabelPosition : integer read fFretLabelPosition write SetFretLabelPosition;
         property colorDegrees : boolean read fColorDegrees write SetColorDegrees;
   end;

   TSeCustomVoicingListView = class(TCustomControl)
      private
         fSkinEngine : TSeSkinEngine;
         fHeaderControl : TSeSkinHeaderControl;
         fDrawGrid : TSeSkinDrawGrid;

         fAddColumns : boolean;
         fComputeContent : boolean;
         fChordVoicingCollection : TChordVoicingCollection;
         fMiniGuitarChord : TMiniGuitarChord;
         fColorDegrees : boolean;
         fDifficultyImageList : TImageList;
         fBrightnessImageList : TImageList;
         fContrastImageList : TImageList;
         fSortDirectionImageList : TImageList;
         fMaxDifficulty : TChordVoicingDifficulty;
         fNoMutedStrings : boolean;
         fNoOpenStrings : boolean;
         fMaxFret : integer;
         procedure RefreshColumnWidths;
         function GetQualities : THalfToneQualities;
         function GetKey : TKey;
         function GetMinorKey : boolean;
         function GetSelectedVoicing : TChordVoicing;
         procedure SetSkinEngine(value : TSeSkinEngine);
         procedure SetQualities(value : THalfToneQualities);
         procedure SetKey(value : TKey);
         procedure SetMinorKey(value : boolean);
         procedure SetColorDegrees(value : boolean);
         procedure SetDifficultyImageList(value : TImageList);
         procedure SetBrightnessImageList(value : TImageList);
         procedure SetContrastImageList(value : TImageList);
         procedure SetSortDirectionImageList(value : TImageList);
         procedure SetMaxDifficulty(value : TChordVoicingDifficulty);
         procedure SetNoMutedStrings(value : boolean);
         procedure SetNoOpenStrings(value : boolean);
         procedure SetMaxFret(value : integer);
         procedure DoDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
         procedure PaintVoicingListGridCell(voicing : TChordVoicing; sectionText : string; aRow : integer;
                                            canvas : TCanvas; rect : TRect; state : TGridDrawState);
         procedure DoHeaderSectionResize(headerControl : TSeCustomHeaderControl; section : TSeHeaderSection);
         procedure DoHeaderSectionDrag(sender : TObject; fromSection, toSection : TSeHeaderSection; var allowDrag : boolean);
         procedure DoHeaderSectionEndDrag(sender : TObject);
         procedure DoHeaderSectionClick(headerControl : TSeCustomHeaderControl; section: TSeHeaderSection);
         procedure DoVoicingDoubleClick(sender : TObject);

      private
         fOnSelectVoicing : TNotifyEvent;
         procedure DoVoicingClick(sender : TObject);

      protected
         procedure CreateWnd; override;
         procedure PopulateColumns;
         procedure PopulateList;
         procedure ChangeDifficultyImageList(sender : TObject); virtual;
         procedure ChangeBrightnessImageList(sender : TObject); virtual;
         procedure ChangeContrastImageList(sender : TObject); virtual;
         procedure ChangeSortDirectionImageList(sender : TObject); virtual;
         property Qualities : THalfToneQualities read GetQualities write SetQualities;
         property Key : TKey read GetKey write SetKey;
         property MinorKey : boolean read GetMinorKey write SetMinorKey;
         property ColorDegrees : boolean read fColorDegrees write SetColorDegrees;
         property DifficultyImageList : TImageList read fDifficultyImageList write SetDifficultyImageList;
         property BrightnessImageList : TImageList read fBrightnessImageList write SetBrightnessImageList;
         property ContrastImageList : TImageList read fContrastImageList write SetContrastImageList;
         property SortDirectionImageList : TImageList read fSortDirectionImageList write SetSortDirectionImageList;
         property MaxDifficulty : TChordVoicingDifficulty read fMaxDifficulty write SetMaxDifficulty;
         property NoMutedStrings : boolean read fNoMutedStrings write SetNoMutedStrings;
         property NoOpenStrings : boolean read fNoOpenStrings write SetNoOpenStrings;
         property MaxFret : integer read fMaxFret write SetMaxFret;
         property SkinEngine : TSeSkinEngine read fSkinEngine write SetSkinEngine;
         property OnSelectVoicing : TNotifyEvent read fOnSelectVoicing write fOnSelectVoicing;
      public
         constructor Create(AOwner: TComponent); override;
         destructor Destroy; override;
         property selectedVoicing : TChordVoicing read GetSelectedVoicing;
   end;

   TSeVoicingListView = class(TSeCustomVoicingListView)
      published
         property BrightnessImageList;
         property ContrastImageList;
         property ColorDegrees;
         property DifficultyImageList;
         property Key;
         property MinorKey;
         property MaxDifficulty;
         property MaxFret;
         property NoMutedStrings;
         property NoOpenStrings;
         property Qualities;
         property SortDirectionImageList;
         property SkinEngine;

         property OnSelectVoicing;
   end;

implementation

uses
   uMusicFontRoutines
   ,contnrs
   ,SysUtils
   ,uMidi
   ;

{ TSeCustomVoicingListView }

constructor TSeCustomVoicingListView.Create(AOwner: TComponent);
begin
   inherited Create(aOwner);
   fSkinEngine := nil;
   fMiniGuitarChord := TMiniGuitarChord.Create(self);
   fMiniGuitarChord.top := 10000;
   fMiniGuitarChord.visible := false;
   fMiniGuitarChord.parent := self;
   fHeaderControl := TSeSkinHeaderControl.Create(self);
   fHeaderControl.parent := self;
   fHeaderControl.font.name := 'tahoma';
   fHeaderControl.font.size := 8;
   fHeaderControl.dragReorder := true;
   fHeaderControl.OnSectionResize := DoHeaderSectionResize;
   fHeaderControl.OnSectionDrag := DoHeaderSectionDrag;
   fHeaderControl.OnSectionEndDrag := DoHeaderSectionEndDrag;
   fHeaderControl.OnSectionClick := DoHeaderSectionClick;
   fDrawGrid := TSeSkinDrawGrid.Create(self);
   fDrawGrid.parent := self;
   fDrawGrid.OnDblClick := DoVoicingDoubleClick;
   fDrawGrid.OnClick := DoVoicingClick;

   fAddColumns := true;
   fComputeContent := true;
   fColorDegrees := false;
   fChordVoicingCollection := TChordVoicingCollection.Create;
   fMaxDifficulty := cvdHard;
   fMaxFret := 15;
   fNoMutedStrings := false;
   fNoOpenStrings := false;
   fDifficultyImageList := nil;
   fBrightnessImageList := nil;
   fSortDirectionImageList := nil;
   fDrawGrid.DefaultRowHeight := fMiniGuitarChord.height;
   fDrawGrid.options := fDrawGrid.options + [goRowSelect] - [goRangeSelect];
   fDrawGrid.fixedCols := 0;
   fDrawGrid.fixedRows := 0;
   fDrawGrid.OnDrawCell := DoDrawCell;

   fOnSelectVoicing := nil;
end;

destructor TSeCustomVoicingListView.Destroy;
begin
   FreeAndNil(fMiniGuitarChord);
   FreeAndNil(fChordVoicingCollection);
   FreeAndNil(fDrawGrid);
   inherited;
end;

procedure TSeCustomVoicingListView.DoHeaderSectionResize(headerControl : TSeCustomHeaderControl; section : TSeHeaderSection);
begin
   RefreshColumnWidths;
   fDrawGrid.colWidths[section.index] := section.Width - 2
end;

procedure TSeCustomVoicingListView.DoHeaderSectionDrag(sender : TObject; fromSection, toSection : TSeHeaderSection; var allowDrag : boolean);
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
              temp := fDrawGrid.ColWidths[sectionIndex1];
              fDrawGrid.ColWidths[sectionIndex1] := fDrawGrid.ColWidths[sectionIndex2];
              fDrawGrid.ColWidths[sectionIndex2] := temp;
              fChordVoicingCollection.sortOrder.Move(sectionIndex1, sectionIndex2);

              // Sort and redraw the grid
              fChordVoicingCollection.SortVoicings;
              fDrawGrid.Invalidate;
           end;
end;

procedure TSeCustomVoicingListView.DoHeaderSectionEndDrag(sender : TObject);
begin
   RefreshColumnWidths;
end;

procedure TSeCustomVoicingListView.DoHeaderSectionClick(headerControl : TSeCustomHeaderControl; section: TSeHeaderSection);
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
   fDrawGrid.Invalidate;
end;

procedure TSeCustomVoicingListView.DoVoicingDoubleClick(sender : TObject);
begin
   PlayChordVoicing(selectedVoicing);
end;

procedure TSeCustomVoicingListView.DoVoicingClick(sender : TObject);
begin
   if Assigned(fOnSelectVoicing)
      then onSelectVoicing(self);
end;

procedure TSeCustomVoicingListView.PaintVoicingListGridCell(voicing : TChordVoicing; sectionText : string; aRow : integer;
                                                            canvas : TCanvas; rect : TRect; state : TGridDrawState);
var
   sortOrder : TStringList;
   voicingProperty : TChordVoicingProperty;
   index : integer;
   s : string;
   bitmap : graphics.TBitmap;   
begin
   sortOrder := voicing.collection.sortOrder;
   index := sortOrder.IndexOf(sectionText);
   if index = -1
      then Exit;

   // Draw the cell
   Assert(sortOrder.objects[index] is TChordVoicingProperty);
   voicingProperty := TChordVoicingProperty(sortOrder.objects[index]);
   Assert(voicingProperty <> nil);

        if voicingProperty is TChordVoicing_Voicing
           then begin
                   fMiniGuitarChord.voicing := voicing;
                   fMiniGuitarChord.PaintTo(canvas, rect.Left, rect.top);

{
                   s := IntToStr(voicing.voicingId);
                   canvas.TextRect(rect, rect.left, rect.bottom - canvas.TextHeight(s), s);
}                   
                end
   else if voicingProperty is TChordVoicing_Difficulty
           then begin
                   canvas.TextRect(rect, rect.left, rect.top + 12, IntToStr(voicing.score));
                   s := VOICING_DIFFICULTY[voicing.difficulty];
                   canvas.TextOut(rect.left, rect.bottom - canvas.TextHeight(s), s);
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
                   s := s + ' fret';
                   canvas.TextRect(rect, rect.left, rect.top, s);
                end
   else if voicingProperty is TChordVoicing_Inversion
           then begin
                   if voicing.inversion = 0
                      then canvas.TextRect(rect, rect.left, rect.top, 'Root')
                      else canvas.TextRect(rect, rect.left, rect.top, 'Inv: ' + IntToStr(voicing.inversion));
                end
   else if voicingProperty is TChordVoicing_MutedStrings
           then begin
                   canvas.font.name := STAFF_FONT_NAME;
                   canvas.font.size := 6;
                   s := StringOfChar(MUTED_POSITION, voicing.mutedStrings);
                   canvas.TextRect(rect, rect.left, rect.top - 4, s);
                end
   else if voicingProperty is TChordVoicing_OpenString
           then begin
                   canvas.font.name := STAFF_FONT_NAME;
                   canvas.font.size := 6;
                   s := StringOfChar(OPEN_POSITION, voicing.openStrings);
                   canvas.TextRect(rect, rect.left, rect.top - 4, s);
                end
   else if voicingProperty is TChordVoicing_Strings
           then begin
                   canvas.TextRect(rect, rect.left, rect.top, IntToStr(voicing.stringCount) + ' strings');
                end
   else if voicingProperty is TChordVoicing_Fingers
   
           then begin
                   canvas.TextRect(rect, rect.left, rect.top, IntToStr(voicing.fingerCount) + ' fingers');
                end
   else if voicingProperty is TChordVoicing_Brightness
           then begin
                   s := VOICING_BRIGHTNESS_NAMES[voicing.brightness];
                   canvas.TextRect(rect, rect.left, rect.bottom - canvas.TextHeight(s), s);
                   if fBrightnessImageList <> nil
                      then begin
                              bitmap := graphics.TBitmap.Create;
                              try
                                    fBrightnessImageList.GetBitmap(Ord(voicing.brightness), bitmap);
                                    bitmap.transparentMode := tmAuto;
                                    bitmap.transparent := true;
                                    canvas.Draw(rect.left, rect.top, bitmap);
                                 finally
                                    bitmap.Free;
                              end;
                           end;
                end
   else if voicingProperty is TChordVoicing_Contrast
           then begin
                   s := VOICING_CONTRAST_NAMES[voicing.contrast];
                   canvas.TextRect(rect, rect.left, rect.bottom - canvas.TextHeight(s), s);
                   if fContrastImageList <> nil
                      then begin
                              bitmap := graphics.TBitmap.Create;
                              try
                                    fContrastImageList.GetBitmap(Ord(voicing.contrast), bitmap);
                                    bitmap.transparentMode := tmAuto;
                                    bitmap.transparent := true;
                                    canvas.Draw(rect.left, rect.top, bitmap);
                                 finally
                                    bitmap.Free;
                              end;
                           end;
                end;
end;

procedure TSeCustomVoicingListView.SetKey(value : TKey);
begin
   if value <> fChordVoicingCollection.key
      then begin
              fChordVoicingCollection.key := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.SetMinorKey(value : boolean);
begin
   if value <> fChordVoicingCollection.minorKey
      then begin
              fChordVoicingCollection.minorKey := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.SetColorDegrees(value : boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              fMiniGuitarChord.colorDegrees := value;
              Invalidate;
           end;
end;

procedure TSeCustomVoicingListView.SetDifficultyImageList(value : TImageList);
begin
   if value <> fDifficultyImageList
      then begin
              fDifficultyImageList := value;
              if fDifficultyImageList = nil
                 then fDifficultyImageList.onChange := nil
                 else fDifficultyImageList.onChange := ChangeDifficultyImageList;
              Invalidate;
           end;
end;

procedure TSeCustomVoicingListView.SetBrightnessImageList(value : TImageList);
begin
   if value <> fBrightnessImageList
      then begin
              fBrightnessImageList := value;
              if fBrightnessImageList <> nil
                 then fBrightnessImageList.onChange := ChangeBrightnessImageList;
              Invalidate;
           end;
end;

procedure TSeCustomVoicingListView.SetContrastImageList(value : TImageList);
begin
   if value <> fContrastImageList
      then begin
              fContrastImageList := value;
              if fContrastImageList <> nil
                 then fContrastImageList.onChange := ChangeContrastImageList;
           end;
end;

procedure TSeCustomVoicingListView.SetSortDirectionImageList(value : TImageList);
begin
   if value <> fSortDirectionImageList
      then begin
              fSortDirectionImageList := value;
              fHeaderControl.Images := fSortDirectionImageList;
              if fSortDirectionImageList <> nil
                 then fSortDirectionImageList.onChange := ChangeSortDirectionImageList;
           end;
end;

procedure TSeCustomVoicingListView.SetMaxDifficulty(value : TChordVoicingDifficulty);
begin
   if value <> fMaxDifficulty
      then begin
              fMaxDifficulty := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.SetNoMutedStrings(value : boolean);
begin
   if value <> fNoMutedStrings
      then begin
              fNoMutedStrings := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.SetNoOpenStrings(value : boolean);
begin
   if value <> fNoOpenStrings
      then begin
              fNoOpenStrings := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.SetMaxFret(value : integer);
begin
   if value <> fMaxFret
      then begin
              fMaxFret := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.RefreshColumnWidths;
var
   index : integer;
   section : TSeHeaderSection;
begin
   for index := 0 to fHeaderControl.sections.count - 1 do
      begin
         section := fHeaderControl.sections[index];
         fDrawGrid.colWidths[index] := ((section.width) div 2) * 2 - 1;
      end;
end;

function TSeCustomVoicingListView.GetQualities : THalfToneQualities;
begin
   result := fChordVoicingCollection.qualities;
end;

function TSeCustomVoicingListView.GetKey : TKey;
begin
   result := fChordVoicingCollection.key;
end;

function TSeCustomVoicingListView.GetMinorKey : boolean;
begin
   result := fChordVoicingCollection.minorKey;
end;

function TSeCustomVoicingListView.GetSelectedVoicing : TChordVoicing;
begin
   if fDrawGrid.selection.top = -1
      then result := nil
      else result := fChordVoicingCollection.items[fDrawGrid.selection.top];
end;

procedure TSeCustomVoicingListView.SetSkinEngine(value : TSeSkinEngine);
begin
   if value <> fSkinEngine
      then begin
              fSkinEngine := value;
              fHeaderControl.SkinEngine := value;
              fDrawGrid.SkinEngine := value;
           end;
end;

procedure TSeCustomVoicingListView.SetQualities(value : THalfToneQualities);
begin
   if value <> fChordVoicingCollection.qualities
      then begin
              fChordVoicingCollection.qualities := value;
              PopulateList;
           end;
end;

procedure TSeCustomVoicingListView.PopulateColumns;
var
   index : integer;
   voicingProperty : TChordVoicingProperty;
   headerSection : TSeHeaderSection;
begin
   // Add the columns
   fDrawGrid.colCount := 1;
   for index := 0 to fChordVoicingCollection.sortOrder.count - 1 do
      begin
         // Retrieve the property
         Assert(fChordVoicingCollection.sortOrder.objects[index] is TChordVoicingProperty);
         voicingProperty := TChordVoicingProperty(fChordVoicingCollection.sortOrder.objects[index]);

         // Assign and index to the column
         headerSection := fHeaderControl.Sections.Add;
         headerSection.spacing := 0;
         headerSection.layout := hglGlyphRight;
         headerSection.margin := 2;
         headerSection.text := voicingProperty.caption;
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
         if voicingProperty.reverseOrder
            then headerSection.imageIndex := 1
            else headerSection.imageIndex := 0;
         fDrawGrid.colCount := fDrawGrid.colCount + 1;
      end;

   RefreshColumnWidths;
   fAddColumns := false;
end;

procedure TSeCustomVoicingListView.PopulateList;
begin
   if not(csReading in ComponentState)
      then begin
              if HandleAllocated
                 then begin
                         cursor := crHourGlass;
                         try
                               fChordVoicingCollection.maxDifficulty := fMaxDifficulty;
                               fChordVoicingCollection.maxFret := fMaxFret;
                               fChordVoicingCollection.noMutedStrings := fNoMutedStrings;
                               fChordVoicingCollection.noOpenStrings := fNoOpenStrings;
                               fChordVoicingCollection.ComputeVoicings;
                            finally
                               cursor := crDefault;
                         end;
                         fDrawGrid.rowCount := fChordVoicingCollection.count - 1;
                         fDrawGrid.Row := 0;
                         fComputeContent := false;
                      end
                 else fComputeContent := true;
           end;
end;

procedure TSeCustomVoicingListView.ChangeDifficultyImageList(sender : TObject);
begin
   if not (csDestroying in ComponentState)
      then fDrawGrid.Invalidate;
end;

procedure TSeCustomVoicingListView.ChangeBrightnessImageList(sender : TObject);
begin
   if not (csDestroying in ComponentState)
      then fDrawGrid.Invalidate;
end;

procedure TSeCustomVoicingListView.ChangeContrastImageList(sender : TObject);
begin
   if not (csDestroying in ComponentState)
      then fDrawGrid.Invalidate;
end;

procedure TSeCustomVoicingListView.ChangeSortDirectionImageList(sender : TObject);
begin
   if not (csDestroying in ComponentState)
      then
   fHeaderControl.Invalidate;
end;

procedure TSeCustomVoicingListView.CreateWnd;
begin
   inherited CreateWnd;
   fHeaderControl.align := alTop;
   fDrawGrid.align := alClient;
   if fAddColumns
      then PopulateColumns;
   if fComputeContent
      then PopulateList;
end;

procedure TSeCustomVoicingListView.DoDrawCell(sender : TObject; aCol, aRow : longint; rect : TRect; state : TGridDrawState);
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

   canvas := fDrawGrid.canvas;
   canvas.font.name := 'Tahoma';
   canvas.font.size := 8;
   if gdSelected in state
      then canvas.font.Color := clWhite
      else canvas.font.Color := clBlack;

   if aCol < fHeaderControl.Sections.count
      then begin
              sectionText := fHeaderControl.Sections[aCol].text;
              PaintVoicingListGridCell(voicing, sectionText, aRow, canvas, rect, state);
           end;
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
   fFret := 0;
   fFretLabelPosition := 0;
   fColorDegrees := false;

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
   tm : TTextMetric;
   ch : char;
   fretLabel : string;
   barre : boolean;

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
   inherited;

   if voicing = nil
      then Exit;

   canvas.brush.style := bsClear;
   canvas.brush.color := clWhite;
   canvas.FillRect(ClientRect);
   canvas.pen.style := psSolid;
   canvas.pen.mode := pmCopy;
   canvas.pen.color := clBlack;
   canvas.pen.width := 1;

   showNut := fMaxFret <= 5;
   barre := voicing.firstFingerBarreExtent > 0;

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

   // Draw dots
   canvas.brush.style := bsClear;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := - (STR_SPACE - 1);
   GetTextMetrics(canvas.handle, tm);
   for str := 0 to 5 do
      begin
         if fColorDegrees
            then canvas.font.color := GetQualityColor(fPositions[str].quality)
            else canvas.font.color := clBlack;
         fret := fPositions[str].fret;
         case fret of
            -1:
               begin
                  x := GetStringX(str);
                  y := GetFretY(-1, true);
                  ch := MUTED_POSITION;
                  canvas.font.height := - 5;
               end;
            0:
               begin
                  x := GetStringX(str);
                  y := GetFretY(-1, true);
                  ch := OPEN_POSITION;
                  canvas.font.height := - 5;
               end;
            else
               begin
                  if fMaxFret > 5
                     then fret := fret - fMinFret + 1;
                  x := GetStringX(str);
                  y := GetFretY(fret - 1, true);
                  ch := NORMAL_POSITION;
                  canvas.font.height := - (STR_SPACE - 1);
               end;
         end;
         canvas.TextOut(x - canvas.TextWidth(ch) div 2, y - tm.tmAscent, ch);
      end;

   // Maybe draw the first finger barre
   if barre
      then DrawBarre;

   // Maybe draw the fret label
   if fMaxFret > 5
      then begin
              fretLabel := IntToStr(fMinFret) + ' fr.';
              canvas.font.name := 'tahoma';
              canvas.font.size := 6;
              canvas.font.color := clBlack;
              x := GetStringX(6);
              y := GetFretY(fFretLabelPosition);
              canvas.TextOut(x, y, fretLabel);
           end;
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

procedure TMiniGuitarChord.SetFret(value : integer);
begin

end;

procedure TMiniGuitarChord.SetFretLabelPosition(value : integer);
begin
end;

procedure TMiniGuitarChord.SetColorDegrees(value : boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              Invalidate;
           end;
end;


end.
