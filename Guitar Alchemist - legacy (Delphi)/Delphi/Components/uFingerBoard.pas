unit uFingerBoard;

interface

uses
   Classes
   ,Controls
   ,Windows
   ,Graphics
   ,Forms
   ,contnrs
   ,uScalePatternFinder
   ,uMusicClasses
   ,uChordVoicings   
   ;

const
   DEFAULT_CURSOR_COLOR = clSkyBlue;
   DEFAULT_CLICKED_CURSOR_COLOR = clGreen;

type
   TFingerBoardMode = (fmScalePattern, fmScaleChord);

   TCustomFingerBoard = class(TCustomControl)
      private
         fMetricsComputed : boolean;
         fPositionsDrawn : boolean;
         fNutOffset : integer;
         fFretSpace : integer;
         fBmpNut : TBitmap;
         fBmpNutBottom : TBitmap;
         fBmpFret : TBitmap;
         fBmpFretBottom : TBitmap;
         fBmpString1 : TBitmap;
         fBmpString2 : TBitmap;
         fBmpString3 : TBitmap;
         fBmpString4 : TBitmap;
         fBmpString5 : TBitmap;
         fBmpString6 : TBitmap;
         fBmpStringNylon1 : TBitmap;
         fBmpStringNylon2 : TBitmap;
         fBmpStringNylon3 : TBitmap;
         fBmpStringNylon4 : TBitmap;
         fBmpStringNylon5 : TBitmap;
         fBmpStringNylon6 : TBitmap;
         fBmpFingerBoard : TBitmap;
         fBmpPositions : TBitmap;
         fStringBitmaps : TStringList;
         fBackgroundPicture : TPicture;
         fBackgroungMedianColor : TColor;
         fDrawing : boolean;
         fLeftHanded : boolean;
         fNylonStrings : boolean;
         fShowRForRoot : boolean;
         fRootColor : TColor;
         fTopOffset : integer;
         fBottomOffset : integer;
         fUsefulTop : integer;
         fUsefulHeight : integer;
         fStrDistance : integer;
         fOctaveMarkerDistance : integer;
         fInlaysColor : TColor;
         fScalePatternFinder : TScalePatternFinder;
         fAlphaBlendValue : integer;
         fShowAllScalePositions : boolean;
         fShowChordFingers : boolean;
         fScalePatternChangeLink : TScalePatternChangeLink;
         fClear : boolean;
         procedure ScalePatternChange(sender : TObject);         
         procedure PaintBackground;
         procedure DrawNutAndFrets;
         procedure DrawFretMarkers;
         procedure DrawFretMarkersOnEdge;
         procedure DrawStrings;
         procedure BackgroundChanged(sender : TObject);
         procedure SetBackgroundPicture(value : TPicture);
         procedure SetLeftHanded(value : boolean);
         procedure SetNylonStrings(value : boolean);
         procedure SetShowRForRoot(value : boolean);
         procedure SetRootColor(value : TColor);
         procedure SetInlaysColor(value : TColor);
         procedure SetAlphaBlendValue(value : integer);
         procedure SetShowAllScalePositions(value : boolean);
         procedure SetShowChordFingers(value : boolean);
         procedure SetScalePatternFinder(value : TScalePatternFinder);
         function GetFretX(fret : extended) : integer;
         function GetStringY(str : extended) : integer;
      protected
         procedure ComputeMetrics; virtual;
         procedure InvalidateMetrics;
         function GetPalette : HPALETTE; override;
         procedure Paint; override;
         procedure DrawScalePatternPositions; virtual;
         procedure DrawScaleChordPositions; virtual;
         function CanResize(var NewWidth, NewHeight: Integer): Boolean; override;
         procedure Resize; override;
         procedure Notification(aComponent : TComponent; operation : TOperation); override;
         property LeftHanded : boolean read fLeftHanded write SetLeftHanded;
         property NylonStrings : boolean read fNylonStrings write SetNylonStrings;
         property ShowRForRoot : boolean read fShowRForRoot write SetShowRForRoot;
         property BackgroundPicture : TPicture read fBackgroundPicture write SetBackgroundPicture;
         property InlaysColor : TColor read fInlaysColor write SetInlaysColor;
         property RootColor : TColor read fRootColor write SetRootColor;
         property AlphaBlendValue : integer read fAlphaBlendValue write SetAlphaBlendValue;
         property ShowAllScalePositions : boolean read fShowAllScalePositions write SetShowAllScalePositions;
         property ShowChordFingers : boolean read fShowChordFingers write SetShowChordFingers;
      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
         function GetPositionPoint(str, fret : integer) : TPoint;
         property scalePatternFinder : TScalePatternFinder read fScalePatternFinder write SetScalePatternFinder;
         property clear : boolean read fClear;

      // Mouse focus and selection
      private
         fOnCursorMoved : TNotifyEvent;
         fOnCursorClicked : TNotifyEvent;
         fOnCursorReleased : TNotifyEvent;
         fCursorClicked : boolean;
         fFocusedFret : integer;
         fFocusedString : integer;
         fCursorColor : TColor;
         fCursorClickedColor : TColor;
         procedure SetCursorColor(value : TColor);
         procedure SetCursorClickedColor(value : TColor);
      protected
         procedure DoCursorMoved; virtual;
         procedure DoCursorClicked; virtual;
         procedure DoCursorReleased; virtual;
         procedure DoExit; override;
         procedure DrawFocusedAndSelectedPosition;
         procedure MouseMove(shift : TShiftState; x, y : integer); override;
         procedure MouseUp(button: TMouseButton; shift : TShiftState; x, y : integer); override;
         procedure MouseDown(button : TMouseButton; shift : TShiftState; x, y : integer); override;
         property CursorColor : TColor read fCursorColor write SetCursorColor default DEFAULT_CURSOR_COLOR;
         property CursorClickedColor : TColor read fCursorClickedColor write SetCursorClickedColor default DEFAULT_CLICKED_CURSOR_COLOR;
      public
         procedure SetCursorPosition(str, fret : integer);
         property CursorClicked : boolean read fCursorClicked;
         property FocusedString : integer read fFocusedString;
         property FocusedFret : integer read fFocusedFret;
      published
         property OnCursorMoved : TNotifyEvent read fOnCursorMoved write fOnCursorMoved;
         property OnCursorClicked : TNotifyEvent read fOnCursorClicked write fOnCursorClicked;
         property OnCursorReleased : TNotifyEvent read fOnCursorReleased write fOnCursorReleased;

      // Mode and chords
      private
         fMode : TFingerBoardMode;
         fChordPositions : TVoicingPositions;
         fChordFret : integer;
         fChordFirstFingerBarreStartingStr : integer;
         fChordFirstFingerBarreExtent : integer;
         fExtendedQualitiesTable : array[0..11] of integer; // First octave quality <-> extended quality correspondances
         fDimSeventh : boolean;
         procedure SetMode(value : TFingerBoardMode);
      public
         procedure SetChordVoicing(chordVoicing : TChordVoicing; chordQualities : THalfToneQualities);
         property Mode : TFingerBoardMode read fMode write SetMode;
         property DimSeventh : boolean read fDimSeventh write fDimSeventh;
   end;

   TFingerBoard = class(TCustomFingerBoard)
      published
         property Align;
         property BackgroundPicture;
         property InlaysColor;
         property RootColor;
         property NylonStrings;
         property ShowRForRoot;
         property LeftHanded;
         property PopupMenu;
         property AlphaBlendValue;
         property ShowAllScalePositions;
         property ShowChordFingers;
         property Anchors;
         property CursorColor;
         property CursorClickedColor;
         property OnResize;
   end;

implementation

{$R pics.res}

uses
   uMiscRoutines
   ,uMusicFontRoutines
   ,Messages
   ,SysUtils
   ,te_bitmap
   ;

const
   DEFAULT_ALPHA_BLEND_VALUE = 128;
   DEFAULT_SHOW_CHORD_SCALE = true;
   DEFAULT_SHOW_CHORD_FINGERS = false;

constructor TCustomFingerBoard.Create(aOwner : TComponent);
var
   index : integer;
begin
   inherited Create(aOwner);
   align := alTop;
   width := 300;
   doubleBuffered := true;
   fCursorClicked := false;
   fMetricsComputed := false;
   fPositionsDrawn := false;
   fClear := true;
   fBmpNut := TBitmap.Create;
   fBmpNutBottom := TBitmap.Create;
   fBmpFret := TBitmap.Create;
   fBmpFretBottom := TBitmap.Create;
   fBmpString1 := TBitmap.Create;
   fBmpString2 := TBitmap.Create;
   fBmpString3 := TBitmap.Create;
   fBmpString4 := TBitmap.Create;
   fBmpString5 := TBitmap.Create;
   fBmpString6 := TBitmap.Create;
   fBmpStringNylon1 := TBitmap.Create;
   fBmpStringNylon2 := TBitmap.Create;
   fBmpStringNylon3 := TBitmap.Create;
   fBmpStringNylon4 := TBitmap.Create;
   fBmpStringNylon5 := TBitmap.Create;
   fBmpStringNylon6 := TBitmap.Create;
   fBmpFingerBoard := TBitmap.Create;
   fBmpPositions := TBitmap.Create;
   fBmpNut.LoadFromResourceName(hInstance, 'nut');
   fBmpNutBottom.LoadFromResourceName(hInstance, 'nutbottom');
   fBmpFret.LoadFromResourceName(hInstance, 'fret');
   fBmpFretBottom.LoadFromResourceName(hInstance, 'fretbottom');
   fBmpString1.LoadFromResourceName(hInstance, 'string1');
   fBmpString2.LoadFromResourceName(hInstance, 'string2');
   fBmpString3.LoadFromResourceName(hInstance, 'string3');
   fBmpString4.LoadFromResourceName(hInstance, 'string4');
   fBmpString5.LoadFromResourceName(hInstance, 'string5');
   fBmpString6.LoadFromResourceName(hInstance, 'string6');
   fBmpStringNylon1.LoadFromResourceName(hInstance, 'stringnylon1');
   fBmpStringNylon2.LoadFromResourceName(hInstance, 'stringnylon2');
   fBmpStringNylon3.LoadFromResourceName(hInstance, 'stringnylon3');
   fBmpStringNylon4.LoadFromResourceName(hInstance, 'stringnylon4');
   fBmpStringNylon5.LoadFromResourceName(hInstance, 'stringnylon5');
   fBmpStringNylon6.LoadFromResourceName(hInstance, 'stringnylon6');
   fStringBitmaps := TStringList.Create;
   fStringBitmaps.AddObject('string1', fBmpString1);
   fStringBitmaps.AddObject('string2', fBmpString2);
   fStringBitmaps.AddObject('string3', fBmpString3);
   fStringBitmaps.AddObject('string4', fBmpString4);
   fStringBitmaps.AddObject('string5', fBmpString5);
   fStringBitmaps.AddObject('string6', fBmpString6);
   fStringBitmaps.AddObject('stringnylon1', fBmpStringNylon1);
   fStringBitmaps.AddObject('stringnylon2', fBmpStringNylon2);
   fStringBitmaps.AddObject('stringnylon3', fBmpStringNylon3);
   fStringBitmaps.AddObject('stringnylon4', fBmpStringNylon4);
   fStringBitmaps.AddObject('stringnylon5', fBmpStringNylon5);
   fStringBitmaps.AddObject('stringnylon6', fBmpStringNylon6);
   for index := 0 to fStringBitmaps.count - 1 do
      TBitmap(fStringBitmaps.objects[index]).transparent := true;

   fBackgroundPicture := TPicture.Create;
   fBackgroundPicture.onChange := BackgroundChanged;
   fScalePatternFinder := nil;

   fScalePatternChangeLink := TScalePatternChangeLink.Create;
   fScalePatternChangeLink.onChange := ScalePatternChange;

   fLeftHanded := false;
   fNylonStrings := false;
   fShowRForRoot := false;
   fInlaysColor := clBlack;
   fRootColor := clBlack;   

   fFocusedFret := -1;

   fAlphaBlendValue := DEFAULT_ALPHA_BLEND_VALUE;
   fShowAllScalePositions := DEFAULT_SHOW_CHORD_SCALE;
   fShowChordFingers := DEFAULT_SHOW_CHORD_FINGERS;

   fMode := fmScalePattern;
   fDimSeventh := false;   
   for index := 0 to 5 do
      fChordPositions[index].fret := -2;

   fCursorColor := DEFAULT_CURSOR_COLOR;
   fCursorClickedColor := DEFAULT_CLICKED_CURSOR_COLOR;

   fOnCursorMoved := nil;
   fOnCursorClicked := nil;
   fOnCursorReleased := nil;   

   cursor := crNone;
end;

destructor TCustomFingerBoard.Destroy;
begin
   FreeAndNil(fScalePatternChangeLink);
   fBackgroundPicture.Free;
   fStringBitmaps.Free;
   fBmpNut.Free;
   fBmpNutBottom.Free;
   fBmpFret.Free;
   fBmpFretBottom.Free;
   fBmpString1.Free;
   fBmpString2.Free;
   fBmpString3.Free;
   fBmpString4.Free;
   fBmpString5.Free;
   fBmpString6.Free;
   fBmpStringNylon1.Free;
   fBmpStringNylon2.Free;
   fBmpStringNylon3.Free;
   fBmpStringNylon4.Free;
   fBmpStringNylon5.Free;
   fBmpStringNylon6.Free;
   fBmpPositions.Free;
   fBmpFingerBoard.Free;
   inherited Destroy;
end;

procedure TCustomFingerBoard.ComputeMetrics;
const
   TOP_OFFSET_PERCENTAGE = 10;
   BOTTOM_OFFSET_PERCENTAGE = 5;
begin
   fTopOffset := Round(height * TOP_OFFSET_PERCENTAGE / 100);
   fBottomOffset := Round(height * BOTTOM_OFFSET_PERCENTAGE / 100);
   fUsefulTop := fTopOffset;

   fUsefulHeight := height - fTopOffset - fBottomOffset;
   fStrDistance := Round(fUsefulHeight/6);
   fNutOffset := Round(fStrDistance * 1.65);
   fFretSpace := width - fNutOffset - fBmpNut.width;   

   fOctaveMarkerDistance := Round(fUsefulHeight * 0.16);
end;

function TCustomFingerBoard.GetPalette : HPALETTE;
begin
  result := 0;
  if fBackgroundPicture.graphic <> nil
     then result := fBackgroundPicture.graphic.palette;
end;

procedure TCustomFingerBoard.Paint;
var
   refreshBmpFingerboard : boolean;
begin
   // Init
   refreshBmpFingerboard := false;

   // Maybe compute metrics
   if not fMetricsComputed
      then try
              ComputeMetrics;
              fBmpFingerBoard.width := width;
              fBmpFingerBoard.height := height;
              fBmpPositions.width := width;
              fBmpPositions.height := height;
              refreshBmpFingerboard := true;
              fPositionsDrawn := false;              
           finally
              fMetricsComputed := true;
           end;

   // Maybe refresh the fingerboard picture
   if refreshBmpFingerboard
      then begin
              fBmpFingerBoard.canvas.brush.color := clWhite;
              fBmpFingerBoard.canvas.brush.style := bsSolid;              
              fBmpFingerBoard.canvas.FillRect(Rect(0, 0, width, height));
              PaintBackground;
              DrawFretMarkers;
              DrawNutAndFrets;
              DrawStrings;
              fBmpFingerBoard.canvas.brush.color := clWhite;
              fBmpFingerBoard.canvas.brush.style := bsSolid;
              fBmpFingerBoard.canvas.FillRect(Rect(0, height - fBottomOffset, width, height));
              DrawFretMarkersOnEdge;              
           end;

        if (fMode = fmScalePattern) and (fScalePatternFinder <> nil) and (fScalePatternFinder.patternPositions.count <> 0)
           then // Draw the fingerboard with positions
                begin
                   // Compose the positions layer
                   if not fPositionsDrawn
                      then begin
                              DrawScalePatternPositions;
                              fPositionsDrawn := true;
                           end;

                   // Draw both the fingerboard and the positions
                   canvas.Draw(0, 0, fBmpPositions);

                   fClear := false;
                end
   else if fMode = fmScaleChord
           then begin
                   // Compose the positions layer
                   if not fPositionsDrawn
                      then begin
                              DrawScaleChordPositions;
                              fPositionsDrawn := true;
                           end;

                   // Draw both the fingerboard and the positions
                   canvas.Draw(0, 0, fBmpPositions);

                   fClear := false;
                end
   else         begin
                   canvas.Draw(0, 0, fBmpFingerBoard);
                   fClear := true;
                end;

   // Draw the focused and selected position
   DrawFocusedAndSelectedPosition;
end;

procedure TCustomFingerBoard.DrawScalePatternPositions;
var
   bmp : TBitmap;
   alphaBlendBmp : TTeBitmap;
   index : integer;
   position : TFretBoardPosition;
   tm : TTextMetric;
   ch : char;
   normalPosWidth : integer;   
   qualityColor : TColor;
   x, y : integer;
begin
   // Draw the empty fingerboard
   fBmpPositions.canvas.Draw(0, 0, fBmpFingerBoard);

   // Initialization for pattern positions
   fBmpPositions.canvas.font.name := STAFF_FONT_NAME;
   fBmpPositions.canvas.font.Style := [fsBold];
   fBmpPositions.canvas.brush.style := bsClear;
   fBmpPositions.canvas.font.height := Round(height * 0.30);
   GetTextMetrics(fBmpPositions.canvas.handle, tm);

   // Draw scale and pattern positions
   if fScalePatternFinder <> nil
      then begin
              // Maybe draw other positions (Alpha blended)
              if fShowAllScalePositions and (fScalePatternFinder.otherPositions.count <> 0)
                 then begin
                         bmp := TBitmap.Create;
                         alphaBlendBmp := TTeBitmap.Create;
                         try
                            // Initialization for non-pattern positions
                            alphaBlendBmp.AlphaBlend := true;
                            bmp.width := 32;
                            bmp.height := 64;
                            bmp.canvas.font.name := STAFF_FONT_NAME;
                            bmp.canvas.font.Style := [fsBold];
                            bmp.canvas.brush.style := bsClear;
                            bmp.canvas.font.height := Round(height * 0.30);

                            // Draw other positions
                            for index := 0 to fScalePatternFinder.otherPositions.count - 1 do
                               begin
                                  position := TFretBoardPosition(fScalePatternFinder.otherPositions[index]);
                                  ch := GetToneQualityChar(position.quality, position.qualityAccidental, fShowRForRoot);
                                  x := GetFretX(position.fret - 0.5);
                                  y := GetStringY(position.str + 0.5);
                                  normalPosWidth := bmp.canvas.TextWidth(ch);
                                  bmp.canvas.brush.style := bsClear;
                                  bmp.canvas.CopyRect(bmp.canvas.ClipRect, fBmpPositions.Canvas,
                                                      Rect(x - normalPosWidth div 2, y - tm.tmAscent,
                                                           x - normalPosWidth div 2 + 32, y - tm.tmAscent + 64)
                                                      );


                                  if position.quality = tqUnison
                                     then begin
                                             bmp.canvas.font.color := fRootColor xor $FFFFFF;
                                             bmp.canvas.TextOut(0, 0, NORMAL_POSITION);
                                             bmp.canvas.font.color := fRootColor;
                                             bmp.canvas.TextOut(0, 0, ch);
                                          end
                                     else begin
                                             bmp.canvas.font.color := clWhite;
                                             bmp.canvas.TextOut(0, 0, NORMAL_POSITION);
                                             bmp.canvas.font.color := clBlack;
                                             bmp.canvas.TextOut(0, 0, ch);
                                          end;

                                  alphaBlendBmp.SetSize(32, 64);
                                  alphaBlendBmp.DrawGraphic(bmp, bmp.canvas.ClipRect);
                                  alphaBlendBmp.SetAlpha(fAlphaBlendValue);
                                  alphaBlendBmp.Draw(fBmpPositions.Canvas, x - normalPosWidth div 2, y - tm.tmAscent);
                               end;

                         finally
                            bmp.Free;
                            alphaBlendBmp.Free;
                         end;
                      end;

              // Draw pattern positions
              for index := 0 to fScalePatternFinder.patternPositions.count - 1 do
                 begin
                    position := TFretBoardPosition(fScalePatternFinder.patternPositions[index]);
                    qualityColor := GetQualityColor(position.quality, position.qualityAccidental);
                    x := GetFretX(position.fret - 0.5);
                    y := GetStringY(position.str + 0.5);
                    ch := GetToneQualityChar(position.quality, position.qualityAccidental, fShowRForRoot);
                    normalPosWidth := fBmpPositions.canvas.TextWidth(ch);
                    if position.quality = tqUnison
                       then // Root position
                            begin
                               fBmpPositions.canvas.font.color := fRootColor xor $FFFFFF;
                               fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, NORMAL_POSITION);
                               fBmpPositions.canvas.font.color := fRootColor;
                               fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, ch);
                            end
                       else begin
                               fBmpPositions.canvas.font.color := clWhite;
                               fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, NORMAL_POSITION);
                               fBmpPositions.canvas.font.color := qualityColor;
                               fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, ch);
                            end;


                    // Draw the character tone
                    if position.characterTone <> ctNone
                       then begin
                               if position.characterTone = ctPrimary
                                  then ch := CHARACTER_TONE_PRIMARY
                                  else ch := CHARACTER_TONE_SECONDARY;
                               normalPosWidth := fBmpPositions.canvas.TextWidth(ch);
                               fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, ch);
                            end;
                 end;
           end;
end;

procedure TCustomFingerBoard.DrawScaleChordPositions;
var
   bmp : TBitmap;
   alphaBlendBmp : TTeBitmap;
   index : integer;
   position : TFretBoardPosition;
   chordPosition : RStringPosition;
   quality : THalfToneQuality;
   tm : TTextMetric;
   ch : char;
   s : string;
   normalPosWidth : integer;
   x, y, w, h : integer;
   strPosition : RStringPosition;
   qualityColor : TColor;
   chordQualityCandidate : integer;

   procedure DrawBarre;
   var
      x, y : integer;
      ch : char;
   begin
      fBmpPositions.canvas.font.name := STAFF_FONT_NAME;
      fBmpPositions.canvas.font.style := [fsBold];
      fBmpPositions.canvas.font.color := fRootColor;
      fBmpPositions.canvas.font.height := Round(height * 0.77);

      ch := GetVertBarreChar(fChordFirstFingerBarreExtent);
      x := GetFretX(fChordFret - 0.5) - fBmpPositions.canvas.TextWidth(ch) div 2 - round(height * 0.08);
      y := GetStringY(fChordfirstFingerBarreStartingStr + 0.5);

      fBmpPositions.canvas.TextOut(x, y, ch);
   end;

begin
   // Draw the empty fingerboard
   fBmpPositions.canvas.Draw(0, 0, fBmpFingerBoard);

   // Initialization for pattern positions
   fBmpPositions.canvas.font.name := STAFF_FONT_NAME;
   fBmpPositions.canvas.font.Style := [fsBold];
   fBmpPositions.canvas.brush.style := bsClear;
   fBmpPositions.canvas.font.height := Round(height * 0.30);
   GetTextMetrics(fBmpPositions.canvas.handle, tm);

   // Draw scale positions (blended)
   if fScalePatternFinder <> nil
      then begin
              // Maybe draw other positions (Alpha blended)
              if fScalePatternFinder.otherPositions.count <> 0
                 then begin
                         bmp := TBitmap.Create;
                         alphaBlendBmp := TTeBitmap.Create;
                         try
                            // Initialization for non-pattern positions
                            alphaBlendBmp.AlphaBlend := true;
                            bmp.width := 32;
                            bmp.height := 64;
                            bmp.canvas.font.Style := [fsBold];
                            bmp.canvas.brush.style := bsClear;

                            // Draw other positions
                            for index := 0 to fScalePatternFinder.otherPositions.count - 1 do
                               begin
                                  position := TFretBoardPosition(fScalePatternFinder.otherPositions[index]);
                                  chordPosition := fChordPositions[position.str];
                                  quality := THalfToneQuality(Ord(ToneToHalftoneQuality(position.quality)) + Ord(position.qualityAccidental) - Ord(naNatural));
                                  chordQualityCandidate := fExtendedQualitiesTable[Ord(quality) mod 12];
                                  if chordQualityCandidate <> -1
                                     then // This position belongs to the chord
                                          begin
                                             quality := THalfToneQuality(chordQualityCandidate);

                                             if (quality = htqMajorSixth) and fdimSeventh
                                                then // Replace 6 by bb7 if diminished or half dim
                                                     ch := DIM_SEVENTH_CHAR
                                                else ch := GetHalfToneQualityChar(quality, fShowRForRoot);
                                          end
                                     else // Position does not belong to the chord
                                          ch := GetToneQualityChar(position.quality, position.qualityAccidental, fShowRForRoot);

                                  x := GetFretX(position.fret - 0.5);
                                  y := GetStringY(position.str + 0.5);
                                  bmp.canvas.brush.style := bsClear;
                                  bmp.canvas.font.name := STAFF_FONT_NAME;
                                  bmp.canvas.font.height := Round(height * 0.30);
                                  normalPosWidth := bmp.canvas.TextWidth(ch);

                                       if fChordPositions[position.str].fret = position.fret
                                          then // The position belongs to the chord
                                               begin
                                                  // Draw the position (plain)
                                                  qualityColor := GetQualityColor(position.quality, position.qualityAccidental);
                                                  fBmpPositions.canvas.brush.style := bsClear;
                                                  fBmpPositions.canvas.font.name := STAFF_FONT_NAME;
                                                  fBmpPositions.canvas.font.height := Round(height * 0.30);
                                                  if position.quality = tqUnison
                                                     then begin
                                                             fBmpPositions.canvas.font.color := fRootColor xor $FFFFFF;
                                                             fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, NORMAL_POSITION);
                                                             fBmpPositions.canvas.font.color := fRootColor;
                                                             fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, ch);
                                                          end
                                                     else begin
                                                             fBmpPositions.canvas.font.color := clWhite;
                                                             fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, NORMAL_POSITION);
                                                             fBmpPositions.canvas.font.color := qualityColor;
                                                             fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, ch);
                                                          end;

                                                  x := x + fBmpPositions.canvas.TextWidth(NORMAL_POSITION);

                                                  // Maybe draw the finger number
                                                  if fShowChordFingers and (chordPosition.finger > 0)
                                                     then begin
                                                             fBmpPositions.canvas.font.name := 'Verdana';
                                                             fBmpPositions.canvas.font.height := Round(height * 0.08);
                                                             fBmpPositions.canvas.pen.color := clGray;
                                                             fBmpPositions.canvas.brush.color := clWhite;

                                                             s := IntToStr(chordPosition.finger);
                                                             w := fBmpPositions.canvas.TextWidth(s);
                                                             h := fBmpPositions.canvas.TextHeight(s);
                                                             y := y - h div 2;
                                                             fBmpPositions.canvas.Rectangle(x - 1, y - 1, x + h + 2, y + h + 2);
                                                             fBmpPositions.canvas.TextOut(x + (h - w) div 2, y, s);                                                             
                                                          end;
                                               end
                                  else if fShowAllScalePositions
                                          then // The position belongs to the scale, draw it blended
                                               begin
                                                  bmp.canvas.brush.style := bsClear;
                                                  bmp.canvas.CopyRect(bmp.canvas.ClipRect, fBmpPositions.Canvas,
                                                                      Rect(x - normalPosWidth div 2, y - tm.tmAscent,
                                                                      x - normalPosWidth div 2 + 32, y - tm.tmAscent + 64)
                                                                      );
                                                  if position.quality = tqUnison
                                                     then begin
                                                             bmp.canvas.font.color := fRootColor;
                                                             bmp.canvas.TextOut(0, 0, NORMAL_POSITION);
                                                          end
                                                     else begin
                                                             bmp.canvas.font.color := clWhite;
                                                             bmp.canvas.TextOut(0, 0, NORMAL_POSITION);
                                                             bmp.canvas.font.color := clBlack;
                                                             bmp.canvas.TextOut(0, 0, ch);
                                                          end;
                                                  alphaBlendBmp.SetSize(32, 64);
                                                  alphaBlendBmp.DrawGraphic(bmp, bmp.canvas.ClipRect);
                                                  alphaBlendBmp.SetAlpha(fAlphaBlendValue);
                                                  alphaBlendBmp.Draw(fBmpPositions.Canvas, x - normalPosWidth div 2, y - tm.tmAscent);
                                               end;
                               end;

                         finally
                            bmp.Free;
                            alphaBlendBmp.Free;
                         end;
                      end;
           end;

   // Draw chord muted positions
   fBmpPositions.canvas.font.name := STAFF_FONT_NAME;
   fBmpPositions.canvas.font.height := Round(height * 0.25);
   fBmpPositions.canvas.brush.style := bsClear;   
   GetTextMetrics(fBmpPositions.canvas.handle, tm);      
   for index := 0 to 5 do
      begin
         strPosition := fChordPositions[index];
         if strPosition.fret = -1
            then begin
                    ch := MUTED_POSITION;
                    y := GetStringY(index + 0.5);
                    x := GetFretX(strPosition.fret - 0.5);
                    normalPosWidth := fBmpPositions.canvas.TextWidth(ch);
                    fBmpPositions.canvas.font.color := fRootColor;
                    fBmpPositions.canvas.TextOut(x - normalPosWidth div 2, y - tm.tmAscent, ch);
                 end;
      end;

   // Maybe draw the barre
   if fChordFirstFingerBarreExtent > 0
      then DrawBarre;
end;

procedure TCustomFingerBoard.PaintBackground;
var
   save : boolean;
   x, y : integer;
begin
   if (fBackgroundPicture <> nil)
      and (fBackgroundPicture.width <> 0)
      and (fBackgroundPicture.height <> 0)
      then // Paint picture tiles
           begin
              save := fDrawing;
              fDrawing := True;
              try
                 for x := 0 to width div fBackgroundPicture.width do
                    for y := 0 to fUsefulHeight div fBackgroundPicture.height do
                       fBmpFingerBoard.canvas.Draw(x * fBackgroundPicture.width, fUsefulTop + y * fBackgroundPicture.height, fBackgroundPicture.Graphic);
              finally
                 fDrawing := Save;
              end;
           end;
end;

procedure TCustomFingerBoard.DrawNutAndFrets;
var
   r : TRect;
   fret : integer;
   x : integer;
   s : string;
   w : integer;
begin
   // Inits
   fBmpFingerBoard.canvas.font.name := 'Tahoma';
   fBmpFingerBoard.canvas.font.height := Round(fTopOffset * 0.94);

   // Draw the nut
   x := fNutOffset;
   if fLeftHanded
      then x := width - x;
   w := fBmpNut.width * fUsefulHeight div 120;
   r.left := (x - w div 2);
   r.top := fUsefulTop;
   r.right := r.left + w;
   r.bottom := r.top + fBmpNut.height;
   //fBmpFingerBoard.canvas.Draw(x - w div 2, fUsefulTop, fBmpNut);
   fBmpFingerBoard.canvas.StretchDraw(r, fBmpNut);

   r.top := fUsefulTop + fUsefulHeight;
   //fBmpFingerBoard.canvas.Draw(x - w div 2, fUsefulTop + fUsefulHeight, fBmpNutBottom);
   fBmpFingerBoard.canvas.StretchDraw(r, fBmpNutBottom);

   // Draw the frets
   for fret := 1 to FRET_COUNT do
      begin
         x := GetFretX(fret);
         w := fBmpFret.width * fUsefulHeight div 120;
         r.left := (x - w div 2);         
         r.right := r.left + w;
         r.top := fUsefulTop;
         fBmpFingerBoard.canvas.StretchDraw(r, fBmpFret);
         //fBmpFingerBoard.canvas.Draw(x - w div 2, fUsefulTop, fBmpFret);

         r.top := fUsefulTop + fUsefulHeight;
         fBmpFingerBoard.canvas.StretchDraw(r, fBmpFret);
         //fBmpFingerBoard.canvas.Draw(x - w div 2, fUsefulTop + fUsefulHeight, fBmpFretBottom);

         x := GetFretX(fret - 0.5);
         s := IntToStr(fret);
         w := fBmpFingerBoard.canvas.TextWidth(s);
         fBmpFingerBoard.canvas.TextRect(Rect(x - w div 2, 0, x + w div 2, fTopOffset - 1), x - w div 2, 0, s);
      end;
end;

procedure TCustomFingerBoard.DrawFretMarkers;
var
   fret : integer;
   s : string;
   x, y : integer;
   starWidth : integer;
   tm : TTextMetric;
begin
   fBmpFingerBoard.canvas.brush.Style := bsClear;
   fBmpFingerBoard.canvas.font.name := STAFF_FONT_NAME;
   fBmpFingerBoard.canvas.font.height := Round(fBottomOffset * 3);
   fBmpFingerBoard.canvas.font.color := fInlaysColor;
   s := FRET_MARKER_STAR;
   GetTextMetrics(fBmpFingerBoard.canvas.handle, tm);
   starWidth := fBmpFingerBoard.canvas.TextWidth(s);
   for fret := 1 to FRET_COUNT do
      begin
         x := GetFretX(fret - 0.5);
              if fret in [3, 5, 7, 9, 15, 17, 19]
                 then begin
                         // Star on fingerboard
                         y := fTopOffset + Round((fUsefulHeight) / 2);
                         Dec(x, starWidth div 2);
                         Dec(y, tm.tmAscent);
                         fBmpFingerBoard.canvas.Textout(x, y, s);
                      end
         else if fret = 12
                 then begin
                         // Double star on fingeboard
                         y := fTopOffset + Round((fUsefulHeight) / 2);
                         Dec(x, starWidth div 2);
                         Dec(y, tm.tmAscent);
                         fBmpFingerBoard.canvas.Textout(x, y - fOctaveMarkerDistance, s);
                         fBmpFingerBoard.canvas.Textout(x, y + fOctaveMarkerDistance, s);
                      end;
      end;
   fBmpFingerBoard.canvas.font.color := clBlack;      
end;

procedure TCustomFingerBoard.DrawFretMarkersOnEdge;
var
   fret : integer;
   fretPosition : integer;
   s : string;
   x, y : integer;
begin
   fBmpFingerBoard.canvas.brush.Style := bsClear;
   fBmpFingerBoard.canvas.font.name := STAFF_FONT_NAME;
   fBmpFingerBoard.canvas.font.height := fBottomOffset;
   for fret := 1 to FRET_COUNT do
      begin
         fretPosition := GetFretX(fret - 0.5);
              if fret in [3, 5, 7, 9, 15, 17, 19]
                 then begin
                         // Dot on edge
                         s := FRET_MARKER_DOT;
                         x := fretPosition - fBmpFingerBoard.canvas.TextWidth(s) div 2;
                         y := height - fBottomOffset + 1;
                         fBmpFingerBoard.canvas.Textout(x, y, s);
                      end
         else if fret = 12
                 then begin
                         // Double dot on edge
                         s := FRET_MARKER_DOUBLE_DOT;
                         x := fretPosition - fBmpFingerBoard.canvas.TextWidth(s) div 2;
                         y := height - fBottomOffset + 1;
                         fBmpFingerBoard.canvas.Textout(x, y, s);
                      end;


      end;
end;

procedure TCustomFingerBoard.DrawStrings;
var
   str : integer;
   bmpIndex : integer;
   bmp : TBitmap;
   y : integer;

   procedure DrawString(y : integer; bmp : TBitmap);
   var
      index : integer;
   begin
      for index := 0 to width div bmp.width do
         fBmpFingerBoard.canvas.Draw(index * bmp.width, y, bmp);
   end;

begin
   for str := 0 to 5 do
      begin
         bmpIndex := str;
         if fNylonStrings
            then Inc(bmpIndex, 6);
         Assert(fStringBitmaps.objects[bmpIndex] is TBitmap);
         bmp := TBitmap(fStringBitmaps.objects[bmpIndex]);
         y := fUsefulTop + Round(fUsefulHeight * (str + 0.5) / 6 - bmp.height / 2);
         DrawString(y, bmp);
      end;
end;

procedure TCustomFingerBoard.SetBackgroundPicture(value : TPicture);
begin
   fBackgroundPicture.Assign(value);
end;

procedure TCustomFingerBoard.SetLeftHanded(value : boolean);
begin
   if value <> fLeftHanded
      then begin
              fLeftHanded := value;
              InvalidateMetrics;
           end;
end;

procedure TCustomFingerBoard.SetNylonStrings(value : boolean);
begin
   if value <> fNylonStrings
      then begin
              fNylonStrings := value;
              InvalidateMetrics;
           end;
end;

procedure TCustomFingerBoard.SetShowRForRoot(value : boolean);
begin
   if value <> fShowRForRoot
      then begin
              fShowRForRoot := value;
              fPositionsDrawn := false;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetRootColor(value : TColor);
begin
   if value <> fRootColor
      then begin
              fRootColor := value;
              fPositionsDrawn := false;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetInlaysColor(value : TColor);
begin
   if value <> fInlaysColor
      then begin
              fInlaysColor := value;
              InvalidateMetrics;
           end;
end;

procedure TCustomFingerBoard.SetAlphaBlendValue(value : integer);
begin
   if value <> fAlphaBlendValue
      then begin
              fAlphaBlendValue := value;
              fPositionsDrawn := false;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetShowAllScalePositions(value : boolean);
begin
   if value <> fShowAllScalePositions
      then begin
              fShowAllScalePositions := value;
              fPositionsDrawn := false;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetShowChordFingers(value : boolean);
begin
   if value <> fShowChordFingers
      then begin
              fShowChordFingers := value;
              fPositionsDrawn := false;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetScalePatternFinder(value : TScalePatternFinder);
begin
   if value <> fScalePatternFinder
      then begin
              // Unregister old scale pattern change link
              if fScalePatternFinder <> nil
                 then fScalePatternFinder.UnRegisterChanges(fScalePatternChangeLink);

              // Set the new scale pattern finder
              fScalePatternFinder := value;
              fPositionsDrawn := false; // Redraw the positions

              // Register the new scale pattern change link
              if fScalePatternFinder <> nil
                 then begin
                         fScalePatternFinder.RegisterChanges(fScalePatternChangeLink);
                         fScalePatternFinder.FreeNotification(self);
                      end;

              Invalidate;                         
           end;
end;

procedure TCustomFingerBoard.BackgroundChanged(sender : TObject);
var
   bmp : TBitmap;
   row, col : integer;
   scanLine : PByteArray;
   rSum, gSum, bSum : double;
   rLineSum, gLineSum, bLineSum : double;
begin
   // Extract the median color of the picture
   bmp := TBitmap.Create;
   try
      bmp.PixelFormat := pf32bit;
      bmp.width := fBackgroundPicture.width;
      bmp.height := fBackgroundPicture.height;
      bmp.canvas.Draw(0, 0, fBackgroundPicture.Graphic);
      rSum := 0;
      gSum := 0;
      bSum := 0;
      for row := 0 to bmp.Height - 1 do
         begin
            rLineSum := 0;
            gLineSum := 0;
            bLineSum := 0;
            scanLine := bmp.ScanLine[row];
            for col := 0 to bmp.Width - 1 do
               begin
                  rLineSum := rLineSum + scanLine^[0] / bmp.Width;
                  gLineSum := gLineSum + scanLine^[1] / bmp.Width;
                  bLineSum := bLineSum + scanLine^[2] / bmp.Width;
               end;
            rSum := rSum + rLineSum / bmp.height;
            gSum := gSum + gLineSum / bmp.height;
            bSum := bSum + bLineSum / bmp.height;
         end;
      fBackgroungMedianColor := RGB(Trunc(rSum), Trunc(gSum), Trunc(bSum));
   finally
      bmp.Free;
   end;

   InvalidateMetrics;
end;

function TCustomFingerBoard.CanResize(var NewWidth, NewHeight: Integer): Boolean;
begin
  if NewWidth <> Width then
    NewHeight := Round(NewWidth * 0.15)
  else
    NewWidth := Round(NewHeight / 0.15);
   result := true;
end;

procedure TCustomFingerBoard.Resize;
begin
   inherited Resize;
   fMetricsComputed := false;
end;

procedure TCustomFingerBoard.Notification(aComponent: TComponent; operation : TOperation);
begin
   inherited Notification(AComponent, operation);
   if (operation = opRemove) and (aComponent = fScalePatternFinder)
      then SetScalePatternFinder(nil);
end;

procedure TCustomFingerBoard.InvalidateMetrics;
begin
   fMetricsComputed := false;
   Invalidate;
end;

function TCustomFingerBoard.GetPositionPoint(str, fret : integer) : TPoint;
var
   fretPosition : integer;
   previousFretPosition : integer;
begin
   fretPosition := fNutOffset + fFretSpace
                   - Round(GetFretPosition(fret, fFretSpace, FRET_COUNT));
   previousFretPosition := fNutOffset + fFretSpace - Round(GetFretPosition(fret - 1, fFretSpace, FRET_COUNT));
   result.x := Round((fretPosition + previousFretPosition + fBmpFret.width) / 2);
   result.y := fUsefulTop + Round(fUsefulHeight * ((5 - str) + 0.5) / 6);
end;

procedure TCustomFingerBoard.MouseDown(button: TMouseButton; shift: TShiftState; x, y: integer);
begin
   fCursorClicked := true;
   DoCursorClicked;
   Invalidate;
end;

procedure TCustomFingerBoard.SetCursorPosition(str, fret : integer);
begin
   if (str >= 1) and (str <= 6)
      and (fret >= 0) and (fret < FRET_COUNT)
      then begin
              fFocusedString := str;
              fFocusedFret := fret;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetCursorColor(value : TColor);
begin
   if value <> fCursorColor
      then begin
              fCursorColor := value;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetCursorClickedColor(value : TColor);
begin
   if value <> fCursorClickedColor
      then begin
              fCursorClickedColor := value;
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.MouseMove(shift: TShiftState; x, y: integer);
var
   oldFocusedFret : integer;
   oldFocusedString : integer;
begin
   x := fNutOffset + fFretSpace - x;
   if fLeftHanded
      then x := width - x;
   y := y - fTopOffset;
   if y < fTopOffset + fUsefulHeight
      then begin
              oldFocusedFret := fFocusedFret;
              oldFocusedString := fFocusedString;
              fFocusedFret := GetPositionFret(x, fFretSpace, FRET_COUNT);
              fFocusedString := 6 - Round(6 * (y - fTopOffset) / fUsefulHeight);
              if (oldFocusedFret <> fFocusedFret) or (oldFocusedString <> fFocusedString)
                 then begin
                         Invalidate;
                         DoCursorMoved;
                      end;
           end;
end;

procedure TCustomFingerBoard.MouseUp(button: TMouseButton; shift: TShiftState; x, y: integer);
begin
   fCursorClicked := false;
   Invalidate;
   DoCursorReleased;
end;

procedure TCustomFingerBoard.DrawFocusedAndSelectedPosition;
var
   cursorRect : TRect;
   cx, cy : integer;
   bmp : TBitmap;
   alphaBlendBmp : TTeBitmap;
   diamondSize : integer;
   x : integer;
   s : string;
   w : integer;
begin
   if (fFocusedFret >= 0) and (fFocusedString > 0)
      and (fFocusedFret <= FRET_COUNT) and (fFocusedString <= 6)
      then begin
              if fFocusedFret > 0
                 then begin
                         x := GetFretX(fFocusedFret - 0.5);
                         s := IntToStr(fFocusedFret);
                         canvas.font.name := 'Tahoma';
                         canvas.font.height := Round(fTopOffset * 0.94);
                         canvas.font.style := [fsBold, fsUnderline];
                         w := canvas.TextWidth(s);
                         canvas.TextRect(Rect(x - w div 2, 0, x + w div 2, fTopOffset - 1), x - w div 2, 0, s);
                      end;

              bmp := TBitmap.Create;
              alphaBlendBmp := TTeBitmap.Create;
              try
                 bmp.width := 64;
                 bmp.height := 64;
                 cursorRect := Rect(0, 0, 64, 64);

                 // Transparent diamond
                 cx := cursorRect.right div 2 + 1;
                 cy := cursorRect.bottom div 2;

                 diamondSize := Trunc(height * 0.14);
                 bmp.canvas.Pen.color := clGray;
                 if fCursorClicked
                    then bmp.canvas.Brush.color := fCursorClickedColor
                    else bmp.canvas.Brush.color := fCursorColor;
                 bmp.canvas.Polygon([Point(cx - diamondSize, cy), Point(cx, cy - diamondSize), Point(cx + diamondSize, cy), Point(cx, cy + diamondSize)]);

                 diamondSize := Trunc(height * 0.1);
                 bmp.canvas.Brush.color := $FEFEFE;
                 bmp.canvas.Polygon([Point(cx - diamondSize, cy), Point(cx, cy - diamondSize), Point(cx + diamondSize, cy), Point(cx, cy + diamondSize)]);

                 alphaBlendBmp.AlphaBlend := true;
                 alphaBlendBmp.SetSize(cursorRect.right - cursorRect.left, cursorRect.bottom - cursorRect.left);
                 alphaBlendBmp.DrawGraphic(bmp, bmp.canvas.ClipRect);
                 alphaBlendBmp.SetAlpha(192);
                 alphaBlendBmp.CheckingTransparent($FEFEFE);
                 alphaBlendBmp.Draw(Canvas, GetFretX(fFocusedFret - 0.5) - cx, GetStringY(fFocusedString - 0.5) - cy)

              finally
                 alphaBlendBmp.Free;
                 bmp.Free;                 
              end;
           end;
end;

function TCustomFingerBoard.GetFretX(fret : extended): integer;
begin
   if fret < 0.5
      then result := fNutOffset - fStrDistance
      else result := fNutOffset + fFretSpace - Round(GetFretPosition(fret, fFretSpace, FRET_COUNT));
   if fLeftHanded
      then result := width - result;
end;

function TCustomFingerBoard.GetStringY(str : extended) : integer;
begin
    result := fTopOffset + Round(fUsefulHeight * (6 - str) / 6);
end;

procedure TCustomFingerBoard.DoExit;
begin
   fFocusedFret := -1;
   fFocusedString := -1;
   Invalidate;
   inherited;
end;

procedure TCustomFingerBoard.DoCursorMoved;
begin
   if Assigned(fOnCursorMoved)
      then OnCursorMoved(self);
end;

procedure TCustomFingerBoard.DoCursorClicked;
begin
   if Assigned(fOnCursorClicked)
      then OnCursorClicked(self);
end;

procedure TCustomFingerBoard.DoCursorReleased;
begin
   if Assigned(fOnCursorReleased)
      then OnCursorReleased(self);
end;

procedure TCustomFingerBoard.SetMode(value: TFingerBoardMode);
begin
   if value <> fMode
      then begin
              fMode := value;
              fPositionsDrawn := false; // Redraw the position layer
              Invalidate;
           end;
end;

procedure TCustomFingerBoard.SetChordVoicing(chordVoicing : TChordVoicing; chordQualities : THalfToneQualities);
var
   str : integer;
   index : integer;
begin
   if chordVoicing = nil
      then for str := 0 to 5 do
              fChordPositions[str].fret := -2
      else begin
              fChordFret := chordVoicing.minFret;
              fChordFirstFingerBarreStartingStr := chordVoicing.firstFingerBarreStartingStr;
              fChordFirstFingerBarreExtent := chordVoicing.firstFingerBarreExtent;
              for str := 0 to 5 do
                 fChordPositions[str] := chordVoicing.positions[str];
              fPositionsDrawn := false; // Redraw the position layer

              // Compute extended qualities
              for index := 0 to High(fExtendedQualitiesTable) do
                 fExtendedQualitiesTable[index] := -1;
              for index := 0 to Ord(High(THalfToneQuality)) do
                 if THalfToneQuality(index) in chordQualities
                    then fExtendedQualitiesTable[index mod 12] := Ord(THalfToneQuality(index));

              Invalidate;
           end;
end;

procedure TCustomFingerBoard.ScalePatternChange(sender : TObject);
begin
   fPositionsDrawn := false;
   Invalidate;
end;

end.
