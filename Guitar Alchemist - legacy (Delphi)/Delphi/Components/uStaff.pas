unit uStaff;

interface

uses
   Controls
   ,Classes
   ,Contnrs
   ,Graphics
   ,Messages
   ,uLayout
   ,uMusicClasses
   ,uScalePatternFinder
   ;

const
   DEFAULT_STAFF_LOWER_TONE : RTone = (tone:tE; octave:0);
   DEFAULT_STAFF_HIGHER_TONE : RTone = (tone:tC; octave:4);
   FIRST_STAFF_LINE_TONE : RTone = (tone:tE; octave:1);


const
   DEFAULT_STAFF_CAPTION_HEIGHT_PERCENTAGE = 10;
   DEFAULT_STAFF_LINE_SPACE = 8;
   DEFAULT_SCALESTAFF_DEGREES_HEIGHT_PERCENTAGE = 20;

const
   TAB_STRING_COUNT = 6;

type

   TStaffNote = class
      private
         fAbsTone : smallint;
         fAccidental : TNoteAccidental;
         fAlteration : TNoteAccidental;
         fAbsHalftone : smallint;
         fQuality : TToneQuality;
         fQualityAccidental : TNoteAccidental;
         fCharacterTone : TCharacterTone;
      public
         property absTone : smallint read fAbsTone;
         property accidental : TNoteAccidental read fAccidental; // Accidental on the staff
         property alteration : TNoteAccidental read fAlteration; // Accidental taking into accound the key
         property absHalftone : smallint read fAbsHalfTone;
         property quality : TToneQuality read fQuality;
         property qualityAccidental : TNoteAccidental read fQualityAccidental;
         property characterTone : TCharacterTone read fCharacterTone;
   end;

   TStaff = class(TCustomLayout)
      private
         fLowerTone : RTone;
         fHigherTone : RTone;
         fMinPosition : integer;
         fMaxPosition : integer;
         fToneDiff : integer;

         fFirstLineY : integer;
         fLineSpaceHeight : integer;
         fSharpCharWidth : integer;
         fXAfterKeySignature : integer;
         fXSpaceBetweenDegrees : integer;
         fKey : TKey;
         fNoteWidth : integer;
         fOffStaffLineWidth : integer;
         fAscent : integer;
         fDescent : integer;
         fAlignment : TAlignment;
         fShowKeyAlterations : boolean;
         function GetPositionY(position : integer) : integer;
         function CheckPosition(position : integer) : boolean;
         procedure SetLowerTone(value : RTone);
         procedure SetHigherTone(value : RTone);
         procedure SetAlignment(value : TAlignment);
         procedure SetShowKeyAlterations(value : boolean);
         procedure MaybeDrawNoteExtraLines(x : integer; position : integer);
         procedure DrawStaffLine(lineIndex : integer);
      protected
         procedure ComputeMetrics; override;
         procedure Paint; override;
         property Aligment : TAlignment read fAlignment write SetAlignment default taLeftJustify;
      public
         constructor Create(aOwner : TComponent); override;
         procedure DrawNote(var x : integer; note : TStaffNote); virtual;
         property LowerTone : RTone read fLowerTone write SetLowerTone;
         property HigherTone : RTone read fHigherTone write SetHigherTone;
      published
         property ShowKeyAlterations : boolean read fShowKeyAlterations write SetShowKeyAlterations;

      // Events
      private
         fChanged : boolean;
         fOnChange : TNotifyEvent;
      protected
         procedure DoChange; virtual;
         property OnChange : TNotifyEvent read fOnChange write fOnChange;
   end;


type
  TCustomScaleStaff = class(TStaff)
     private
        fDegrees : TObjectList;
        fItemIndex : integer;
        fHotTrack : boolean;
        fMouseDown : boolean;
        fCursorColor : TColor;
        fClickedColor : TColor;
        procedure SetItemIndexInternal(value : integer);
        procedure SetItemIndex(value : integer);
        procedure SetHotTrack(value : boolean);
        procedure SetCursorColor(value : TColor);
        procedure SetClickedColor(value : TColor);
     protected
        procedure MouseMove(shift : TShiftState; x, y : integer); override;
        procedure MouseDown(button: TMouseButton; shift : TShiftState; x, y : integer); override;
        procedure MouseUp(button: TMouseButton; shift : TShiftState; x, y : integer); override;
        procedure PlayDegree(index : integer); virtual;
        procedure Paint; override;

        property HotTrack : boolean read fHotTrack write SetHotTrack;
        property CursorColor : TColor read fCursorColor write SetCursorColor;
        property ClickedColor : TColor read fClickedColor write SetClickedColor;
     public
        constructor Create(aOwner : TComponent); override;
        destructor Destroy; override;
        property ItemIndex : integer read fItemIndex write SetItemIndex;
        property Clicked : boolean read fMouseDown;
        property degrees : TObjectList read fDegrees;
     published
        property Aligment;
        property Font;
        property OnMouseDown;
        property OnMouseUp;
  end;

  TToneAlterations = array[0..6] of integer;

  TScalePatternChart = class(TCustomScaleStaff)
     private
        fToneAlterations : TToneAlterations;
     protected
        procedure PlayDegree(index : integer); override;
     public
        constructor Create(aOwner : TComponent); override;
        procedure SetPattern(scaleFingeringFinder : TScalePatternFinder; key : TKey; minorKey : boolean = false);
        procedure Clear;
        procedure DrawNote(var x : integer; note : TStaffNote); override;
        property toneAlterations : TToneAlterations read fToneAlterations;
     published
        property Align;
        property HotTrack;
        property ItemIndex;
        property OnChange;
        property Visible;
        property CursorColor;
        property ClickedColor;
  end;




implementation

uses
   Windows
   ,SysUtils
   ,Forms
   ,uMusicFontRoutines
   ,uMidi
   ,te_bitmap
   ;

const
   DEFAULT_NOTE_CHARACTER = WHOLE_NOTE_CHARACTER;

const
   MAJOR_SCALE_INTERVALS : array[0..6] of shortint =
      (2, 2, 1, 2, 2, 2, 1);
   MINOR_SCALE_INTERVALS : array[0..6] of shortint =
      (2, 1, 2, 2, 1, 2, 2);
   NOTE_ACCIDENTAL_TABLE : array[-2..2, -1..1] of TNoteAccidental =
      (
         (naDoubleFlat, naDoubleFlat, naDoubleFlat),
         (naNone, naFlat, naFlat),
         (naNatural, naNone, naNatural),
         (naSharp, naSharp, naNone),
         (naDoubleSharp, naDoubleSharp, naDoubleSharp)
      );


procedure DrawKeySignature(canvas : TCanvas; left, top, staffLineSpaceHeight : integer; key : TKey);
const
   SHARP_POSITIONS : array[0..6] of integer =
      (8, 5, 9, 6, 3, 7, 4);
   FLAT_POSITIONS : array[0..6] of integer =
      (4, 7, 3, 6, 2, 5, 1);
   KEY_SIGNATURE_ALTERATIONS : array[TKey] of integer =
      (0, 0, 1, 2, 3, 4, 5, 6, 7, -1, -2, -3, -4, -5, -6, -7);
var
   alterationCount : integer;
   alterationPosition : integer;
   alterationY : integer;
   xIncrement : integer;
   index : integer;
   sharp : boolean;
   ch : char;
   tm : TTextMetric;
begin
   alterationCount := KEY_SIGNATURE_ALTERATIONS[key];
   if alterationCount <> 0
      then // The key contains some alterations
           begin
              // Choose the right alteration character
              if alterationCount > 0
                 then begin
                         ch := SHARP_CHARACTER;
                         sharp := true;
                      end
                 else begin
                         ch := FLAT_CHARACTER;
                         sharp := false;
                         alterationCount := - alterationCount;
                      end;

              // Prepare the font
              canvas.brush.style := bsClear;
              canvas.pen.width := 1;              
              canvas.pen.color := clBlack;
              canvas.pen.style := psSolid;
              canvas.pen.mode := pmCopy;
              canvas.font.charset := SYMBOL_CHARSET;
              canvas.font.name := STAFF_FONT_NAME;
              canvas.font.height := staffLineSpaceHeight * 8;
              GetTextMetrics(canvas.handle, tm);
              xIncrement := canvas.TextWidth(SHARP_CHARACTER);

              // Draw the alterations
              for index := 0 to alterationCount - 1 do
                 begin
                    if sharp
                       then alterationPosition := SHARP_POSITIONS[index]
                       else alterationPosition := FLAT_POSITIONS[index];
                    alterationY := top - (alterationPosition + 0)* staffLineSpaceHeight div 2;
                    canvas.TextOut(left, alterationY - tm.tmAscent, ch);
                    Inc(left, xIncrement);
                 end;
           end;
end;


{ TStaff }

constructor TStaff.Create(aOwner : TComponent);
begin
   inherited;

   controlStyle := [csOpaque, csCaptureMouse, csClickEvents, csSetCaption];
   SetBounds(0, 0, 500, 148);

   topPropOffsetPercentage := DEFAULT_STAFF_CAPTION_HEIGHT_PERCENTAGE;
   fLowerTone := DEFAULT_STAFF_LOWER_TONE;
   fHigherTone := DEFAULT_STAFF_HIGHER_TONE;
   fToneDiff := GetToneDifference(fHigherTone, fLowerTone) + 3;
   fLineSpaceHeight := DEFAULT_STAFF_LINE_SPACE;

   fKey := ksCMajorAMinor;
   fXSpaceBetweenDegrees := 0;
   fShowKeyAlterations := false;

   fChanged := false;
   fOnChange := nil;
end;

procedure TStaff.Paint;
const
  Alignments : array[TAlignment] of Longint = (DT_LEFT, DT_RIGHT, DT_CENTER);
var
   tm : TextMetric;
   index : integer;
begin
   inherited Paint;

   // Clear background
   canvas.brush.color := clWhite;
   canvas.brush.style := bsSolid;
   canvas.FillRect(Rect(0, 0, width, height));
   canvas.brush.style := bsClear;
   canvas.pen.width := 1;
   canvas.pen.color := clBlack;
   canvas.pen.style := psSolid;
   canvas.pen.mode := pmCopy;

   // Draw the staff lines
   canvas.pen.width := fLineSpaceHeight div 5;
   for index := 0 to 4 do
      DrawStaffLine(index);

   // Draw the G key
   canvas.font.charset := SYMBOL_CHARSET;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := 8 * fLineSpaceHeight;
   canvas.font.color := clBlack;
   canvas.font.style := [];
   GetTextMetrics(canvas.handle, tm);
   canvas.TextOut(Round(canvas.font.height * 0.05), GetPositionY(2) - tm.tmAscent + 1, G_KEY_CHARACTER);

   // Draw key signature
   DrawKeySignature(canvas,
                    canvas.TextWidth(G_KEY_CHARACTER) * 3 div 2,
                    fFirstLineY, fLineSpaceHeight, fKey);

   // Maybe fire onChange event
   if fChanged
      then DoChange;
end;

procedure TStaff.ComputeMetrics;
var
   tm : TTextMetric;
   abcStructs : ABC;
begin
   inherited ComputeMetrics;

   // Adjust font height
   font.height := - Round(topPropOffset * 0.95);

   // Compute staff metrics
   fToneDiff := GetToneDifference(fHigherTone, fLowerTone) + 3;
   fMinPosition := Ord(fLowerTone.tone) + (fLowerTone.octave - 1) * 7 - 2;
   fMaxPosition := Ord(fHigherTone.tone) + (fHigherTone.octave - 1) * 7 - 2;
   fLineSpaceHeight := Round(2 * LayoutHeight / fToneDiff);
   fFirstLineY := actualTopOffset + actualTopPropOffset + Trunc((GetToneDifference(fHigherTone, FIRST_STAFF_LINE_TONE) + 2) * fLineSpaceHeight / 2);
   canvas.font.charset := SYMBOL_CHARSET;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := fLineSpaceHeight * 8;
   canvas.font.style := [];
   GetTextMetrics(canvas.handle, tm);
   fSharpCharWidth := canvas.TextWidth(SHARP_CHARACTER);
   fXAfterKeySignature := 16 * fSharpCharWidth;
   fXSpaceBetweenDegrees := Round(fSharpCharWidth * 4.2);
   GetCharABCWidths(canvas.handle, Ord(DEFAULT_NOTE_CHARACTER), Ord(DEFAULT_NOTE_CHARACTER), abcStructs);
   fNoteWidth := abcStructs.abcB;
   fOffStaffLineWidth := fNoteWidth * 3 div 2;
   fAscent := tm.tmAscent;
   fDescent := tm.tmDescent;
end;

function TStaff.GetPositionY(position : integer) : integer;
begin
   result := fFirstLineY - Trunc((position  * fLineSpaceHeight) / 2);
end;

function TStaff.CheckPosition(position : integer) : boolean;
begin
   result := (position >= fMinPosition) and (position <= fMaxPosition);
end;

procedure TStaff.SetLowerTone(value : RTone);
const
   MIN_LOWER_TONE : RTone = (tone:tC; octave:0);
   MAX_LOWER_TONE : RTone = (tone:tE; octave:0);
begin
   if (GetToneDifference(value, fLowerTone) <> 0)
      and (GetToneDifference(value, MIN_LOWER_TONE) > 0)
      and (GetToneDifference(MAX_LOWER_TONE, value) > 0)
      then begin
              fLowerTone := value;
              MetricsChanged;
           end;
end;

procedure TStaff.SetHigherTone(value : RTone);
const
   MIN_HIGHER_TONE : RTone = (tone:tC; octave:4);
   MAX_HIGHER_TONE : RTone = (tone:tC; octave:6);
begin
   if (GetToneDifference(value, fHigherTone) <> 0)
      and (GetToneDifference(value, MIN_HIGHER_TONE) > 0)
      and (GetToneDifference(MAX_HIGHER_TONE, value) > 0)
      then begin
              fHigherTone := value;
              MetricsChanged;
           end;
end;

procedure TStaff.SetAlignment(value : TAlignment);
begin
   if value <> fAlignment
      then begin
              fAlignment := value;
              Invalidate;
           end;
end;

procedure TStaff.SetShowKeyAlterations(value : boolean);
begin
   if value <> fShowKeyAlterations
      then begin
              fShowKeyAlterations := value;
              Invalidate;
           end;
end;

procedure TStaff.DrawStaffLine(lineIndex : integer);
var
   y : integer;
begin
   if CheckPosition(lineIndex * 2)
      then begin
              y := GetPositionY(lineIndex * 2);
              canvas.MoveTo(0, y);
              canvas.LineTo(width , y);
           end;
end;

procedure TStaff.MaybeDrawNoteExtraLines(x : integer; position : integer);
var
   index : integer;
   y : integer;
begin
   canvas.pen.width := fLineSpaceHeight div 5;
   position := position div 2;
      if position < 0
         then begin
               for index := position to -1 do
                  begin
                     if CheckPosition(index * 2)
                        then begin
                                y := GetPositionY(index * 2);
                                canvas.MoveTo(x - fOffStaffLineWidth div 2, y);
                                canvas.LineTo(x + fOffStaffLineWidth div 2, y);
                             end;
                  end;
              end;
     if position > 4
        then begin
               for index := 5 to position do
                  begin
                     if CheckPosition(index * 2)
                        then begin
                                y := GetPositionY(index * 2);
                                canvas.MoveTo(x - fOffStaffLineWidth div 2, y);
                                canvas.LineTo(x + fOffStaffLineWidth div 2, y);
                             end;   
                  end;
            end;
end;

procedure TStaff.DrawNote(var x : integer; note : TStaffNote);
var
   position : integer;
   y : integer;
   ch : char;
   accidentedInKey : boolean;
begin
   // Draw the note
   canvas.font.style := [];   
   canvas.font.color := clBlack;
   position := note.absTone - 9;
   if CheckPosition(position)
      then begin
              y := GetPositionY(position);
              canvas.TextOut(x - fNoteWidth div 2 , y - fAscent, DEFAULT_NOTE_CHARACTER);

              // Draw extra lines
              MaybeDrawNoteExtraLines(x, position);

              // Draw the note accidental
              y := GetPositionY(position);
              accidentedInKey := KEY_TONE_IS_ACCIDENTED[fKey, TTone(note.absTone mod 7)];
              if (note.accidental = naNone) and accidentedInKey and fShowKeyAlterations
                 then begin
                         canvas.font.color := $7070FE; 
                         if fKey < ksFMajorDMinor
                            then // Sharp key
                                 ch := SHARP_CHARACTER
                            else // Flat key
                                 ch := FLAT_CHARACTER;
                      end
                 else ch := ACCIDENTAL_CHARACTERS[note.accidental];
              canvas.TextOut(x - fNoteWidth div 2 - canvas.TextWidth(ch), y - fAscent, ch);
           end;

   // Go to next position
   Inc(x, fXSpaceBetweenDegrees);
end;

procedure TStaff.DoChange;
begin
   fChanged := false;
   if Assigned(fOnChange)
      then OnChange(self);
end;

{ TCustomScaleStaff}

constructor TCustomScaleStaff.Create(aOwner: TComponent);
begin
   inherited Create(aOwner);

   fDegrees := TObjectList.Create(true);

   bottomPropOffsetPercentage := DEFAULT_SCALESTAFF_DEGREES_HEIGHT_PERCENTAGE;

   fMouseDown := false;
   fItemIndex := -1;

   fCursorColor := clSkyBlue;
   fClickedColor := clGreen;
end;

destructor TCustomScaleStaff.Destroy;
begin
   fDegrees.Free;
   inherited;
end;

procedure TCustomScaleStaff.MouseMove(shift : TShiftState; x, y : integer);
var
   degreeIndex : integer;
begin
   if fHotTrack
   then begin
           if (x > fXAfterKeySignature - fXSpaceBetweenDegrees + fXSpaceBetweenDegrees div 2) and
              (x < fXAfterKeySignature + fDegrees.count * fXSpaceBetweenDegrees - fXSpaceBetweenDegrees div 2) and
              (y > 20) and (y < height)
              then begin
                      mouseCapture := true;
                      screen.cursor := crHandPoint;
                      degreeIndex := (x - fXAfterKeySignature + fXSpaceBetweenDegrees div 2) div fXSpaceBetweenDegrees;
                      if ItemIndex <> degreeIndex
                         then begin
                                 SetItemIndexInternal(degreeIndex);
                                 if ssLeft in shift
                                    then MouseDown(mbLeft, shift, x, y);
                              end;
                   end
              else begin
                      screen.cursor := crDefault;
                      cursor := crDefault;
                      SetItemIndexInternal(-1);
                      mouseCapture := false;
                   end;
        end;

   fMouseDown := ssLeft in shift;

   inherited MouseMove(Shift, X, Y);
end;

procedure TCustomScaleStaff.MouseDown(button: TMouseButton; shift : TShiftState; x, y : integer);
begin
   if not (ssDouble in shift)
      then begin
              fMouseDown := true;
              if (ItemIndex <> -1) and fHotTrack
                 then begin
                         PlayDegree(ItemIndex);
                         Invalidate;
                      end;
           end;
   inherited MouseDown(button, shift, x, y);
end;

procedure TCustomScaleStaff.MouseUp(button: TMouseButton; shift : TShiftState; x, y : integer);
begin
   fMouseDown := false;
   Invalidate;
   globalMidi.AllNotesOff;
   inherited MouseUp(button, shift, x, y);
end;

procedure TCustomScaleStaff.Paint;
var
   x : integer;
   r : TRect;
   w : integer;
   index : integer;
   degree : TStaffNote;
   bmp : graphics.TBitmap;
   alphaBlendBmp : TTeBitmap;
begin
   inherited Paint;

   // Draw selected degree frame
   if fItemIndex <> -1
      then begin
              bmp := graphics.TBitmap.Create;
              alphaBlendBmp := TTeBitmap.Create;
              try
                 bmp.width := fXSpaceBetweenDegrees;
                 bmp.height := height;
                 r := Rect(0, 0, fXSpaceBetweenDegrees, height);

                 w := Round(fXSpaceBetweenDegrees * 1.2);
                 x := fXAfterKeySignature + fItemIndex * fXSpaceBetweenDegrees - w div 2;
                 if fMouseDown
                    then canvas.pen.color := fClickedColor
                    else canvas.pen.color := fCursorColor;

                 r := Rect(0, 0, fXSpaceBetweenDegrees, height);
                 if fMouseDown
                    then bmp.canvas.brush.color := fClickedColor
                    else bmp.canvas.brush.color := fCursorColor;
                 bmp.canvas.FillRect(r);
                 bmp.canvas.pen.color := clBlack;
                 bmp.canvas.Rectangle(r);
                 

                 r := Rect(x, 0, x + w, height);
                 alphaBlendBmp.AlphaBlend := true;
                 alphaBlendBmp.SetSize(fXSpaceBetweenDegrees, height);
                 alphaBlendBmp.DrawGraphic(bmp, bmp.canvas.ClipRect);
                 alphaBlendBmp.SetAlpha(64);
                 alphaBlendBmp.CheckingTransparent(clWhite);
                 alphaBlendBmp.Draw(Canvas, r.left, 0);

              finally
                 alphaBlendBmp.Free;
                 bmp.Free;                 
              end;
           end;

   // Draw the degrees
   canvas.pen.color := clBlack;
   canvas.pen.width := 1;
   canvas.font.charset := SYMBOL_CHARSET;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := fLineSpaceHeight * 8;
   canvas.font.color := clBlack;
   canvas.font.style := [];
   x := fXAfterKeySignature;
   for index := 0 to fDegrees.count - 1 do
      begin
         degree := TStaffNote(fDegrees[index]);
         DrawNote(x, degree);
      end;
end;

procedure TCustomScaleStaff.PlayDegree(index : integer);
begin
   // To be implemented by descendent
end;

procedure TCustomScaleStaff.SetItemIndexInternal(value : integer);
begin
        if value < -1
           then value := -1
   else if value > fDegrees.count - 1
           then value := fDegrees.count - 1;
   if value <> fItemIndex
      then begin
              fItemIndex := value;
              fChanged := true;
              Invalidate;
           end;
end;

procedure TCustomScaleStaff.SetItemIndex(value : integer);
begin
        if value < -1
           then value := -1
   else if value > fDegrees.count - 1
           then value := fDegrees.count - 1;
   if value <> fItemIndex
      then begin
              fItemIndex := value;
              Invalidate;
           end;
end;

procedure TCustomScaleStaff.SetHotTrack(value : boolean);
begin
   if value <> fHotTrack
      then begin
              fHotTrack := value;
           end;
end;

procedure TCustomScaleStaff.SetCursorColor(value : TColor);
begin
   if value <> fCursorColor
      then begin
              fCursorColor := value;
              Invalidate;
           end;
end;

procedure TCustomScaleStaff.SetClickedColor(value : TColor);
begin
   if value <> fClickedColor
      then begin
              fClickedColor := value;
              Invalidate;
           end;
end;

constructor TScalePatternChart.Create(aOwner: TComponent);
begin
   inherited Create(aOwner);
   bottomPropOffsetPercentage := DEFAULT_SCALESTAFF_DEGREES_HEIGHT_PERCENTAGE * 3;   
   DoubleBuffered := true;
end;

procedure TScalePatternChart.SetPattern(scaleFingeringFinder : TScalePatternFinder; key : TKey; minorKey : boolean);
var
   index : integer;
   keyRootHalfTone, keyRootTone : shortint;
   halfTone, tone : shortint;
   degreeInterval : shortint;
   degreeAlteration : shortint;
   toneAlteration : shortint;
   staffNote : TStaffNote;
   firstPosHalftone : integer;
   currentPosHalfTone : integer;
   patternPosition : TFretBoardPosition;
   positionIndex : integer;
   degreeIndex : integer;

   procedure FindEasiestWriting(var tone : shortint; halftone : shortint);
   var
      t : shortint;
      degreeAlteration : shortint;
      toneAlteration : shortint;
      noteAccidental : TNoteAccidental;
   begin

      if halfTone < 0
         then Inc(halfTone, 12);

      t := (tone + 7) mod 7;
      if (TONE_ALTERATIONS_TABLE[t, halfTone mod 12] <> 0)
         then begin
                 if TONE_ALTERATIONS_TABLE[t, halfTone mod 12] = fToneAlterations[t]
                    then // The tone is in the key
                         Exit;

                 // Next note
                 t := (tone + 1 + 7) mod 7;
                 if (TONE_ALTERATIONS_TABLE[t, halfTone mod 12] = fToneAlterations[t])
                    then // Next note is in key
                         begin
                            Inc(tone);
                            Exit;
                         end;

                 degreeAlteration := TONE_ALTERATIONS_TABLE[t mod 7, halfTone mod 12];
                 toneAlteration := fToneAlterations[t mod 7];
                 if (degreeAlteration >= -2)
                    and (degreeAlteration <= 2)
                    and (toneAlteration >= -1)
                    and (toneAlteration <= 1)
                    then begin
                            noteAccidental := NOTE_ACCIDENTAL_TABLE[degreeAlteration, toneAlteration];
                            if (TONE_ALTERATIONS_TABLE[t, halfTone mod 12] = 0)
                               and (fToneAlterations[t] = 0)
                               and (noteAccidental <> naNatural)
                               then // Next note is unaltered and not accidented in key
                                    begin
                                       Inc(tone);
                                       Exit;
                                    end;
                         end;

                 // Previous note
                 t := (tone - 1 + 7) mod 7;
                 if (TONE_ALTERATIONS_TABLE[t, halfTone mod 12] = fToneAlterations[t])
                    then // Previous note is in key
                         begin
                            Dec(tone);
                            Exit;
                         end;

                 degreeAlteration := TONE_ALTERATIONS_TABLE[t mod 7, halfTone mod 12];
                 toneAlteration := fToneAlterations[t mod 7];
                 if (degreeAlteration >= -2)
                    and (degreeAlteration <= 2)
                    and (toneAlteration >= -1)
                    and (toneAlteration <= 1)
                    then begin
                            noteAccidental := NOTE_ACCIDENTAL_TABLE[degreeAlteration, toneAlteration];
                            if (TONE_ALTERATIONS_TABLE[t, halfTone mod 12] = 0)
                               and (fToneAlterations[t] = 0)
                               and (noteAccidental <> naNatural)
                               then // Previous note is unaltered and not accidented in key
                                    begin
                                       Dec(tone);
                                       Exit;
                                    end;
                         end;
              end;
   end;

begin
   // Inits
   ItemIndex := -1;
   fKey := key;
   if scaleFingeringFinder.patternPositions.count = 0
      then // Exit right away if no valid pattern found
           Exit;

   // Find the half tone and tone for the key root
   if minorKey
      then begin
              keyRootHalfTone := KEYROOT_HALFTONE_MINOR[key];
              keyRootTone := KEYROOT_TONE_MINOR[key];
           end
      else begin
              keyRootHalfTone := KEYROOT_HALFTONE_MAJOR[key];
              keyRootTone := KEYROOT_TONE_MAJOR[key];
           end;

   // Initialize the tone alterations (Key signature)
   halfTone := keyRootHalfTone;
   tone := keyRootTone;
   if minorKey
      then begin
              for index := 0 to High(MINOR_SCALE_INTERVALS) do
                 begin
                    fToneAlterations[tone] := TONE_ALTERATIONS_TABLE[tone, halfTone];
                    halftone := (halftone + MINOR_SCALE_INTERVALS[index]) mod 12; // Next scale degree
                    tone := (tone + 1) mod 7; // One diatonic tone further
                 end;
           end
      else begin
              for index := 0 to High(MAJOR_SCALE_INTERVALS) do
                 begin
                    fToneAlterations[tone] := TONE_ALTERATIONS_TABLE[tone, halfTone];
                    halftone := (halftone + MAJOR_SCALE_INTERVALS[index]) mod 12; // Next scale degree
                    tone := (tone + 1) mod 7; // One diatonic tone further
                 end;
           end;

   // Find the first note of the scale
   firstPosHalftone := Ord(STANDARD_GUITAR_TUNING[0].halfTone)
                       + STANDARD_GUITAR_TUNING[0].octave * 12
                       + Ord(TFretBoardPosition(scaleFingeringFinder.patternPositions[0]).fret);

   tone := keyRootTone;
   halfTone := keyRootHalfTone;

   // Check the problem with Cb key if the first position equals the root
   if (key = ksCFlatMajorAFlatMinor)
      and (firstPosHalftone >= Ord(htB))
      and (not minorKey)
      then Dec(halfTone, 12);

   // Adjustement
   if halftone > firstPosHalftone
      then begin
              Dec(halftone, 12);
              Dec(tone, 7);
           end;

   index := 0;
   while halfTone < firstPosHalftone do
      begin
         degreeInterval := scaleFingeringFinder.scale.degreeInterval[index];
         halftone := halftone + degreeInterval; // Next scale degree
         Inc(tone); // One diatonic tone further

         // Skip notes if needed
         if TONE_ALTERATIONS_TABLE[(tone + 7) mod 7, (halfTone + 12) mod 12] = 127
            then Inc(tone);

         // Next scale interval
         Inc(index);
      end;

   // See if easier writing can be found
   FindEasiestWriting(tone, halfTone);

   // Compute the scale notes
   if tone < 0
      then Inc(tone, 7);
   fDegrees.Clear;
   currentPosHalfTone := firstPosHalftone;
   degreeIndex := scaleFingeringFinder.patternStartDegree + scaleFingeringFinder.scaleMode - 1;
   for positionIndex := 0 to scaleFingeringFinder.patternPositions.count - 1 do
      begin
         // Create the note
         staffNote := TStaffNote.Create;
         fDegrees.Add(staffNote);

         patternPosition := TFretBoardPosition(scaleFingeringFinder.patternPositions[positionIndex]);
         staffNote.fAbsHalftone := currentPosHalfTone;
         staffNote.fQuality := patternPosition.quality;
         staffNote.fQualityAccidental := patternPosition.qualityAccidental;
         staffNote.fCharacterTone := patternPosition.characterTone;

         // Skip notes if needed
         if (TONE_ALTERATIONS_TABLE[tone mod 7, halfTone mod 12] = 127)
            then Inc(tone);

         // See if easier writing can be found
         FindEasiestWriting(tone, halfTone);

         // Set the note properties
         if halfTone < 0
            then Assert(false);
         Assert(halfTone >= 0);
         staffNote.fAbsTone := tone;
         degreeAlteration := TONE_ALTERATIONS_TABLE[tone mod 7, halfTone mod 12];
         toneAlteration := fToneAlterations[tone mod 7];

         // Go one diatonic tone further
         Inc(tone);

         // Set the note accidental and alteration
         staffNote.fAccidental := NOTE_ACCIDENTAL_TABLE[degreeAlteration, toneAlteration];
         if staffNote.fAccidental = naNone
            then case fToneAlterations[staffNote.fAbsTone mod 7] of
                    -1: staffNote.fAlteration := naFlat;
                    +1: staffNote.fAlteration := naSharp;
                    else staffNote.fAlteration := naNone;
                 end
            else staffNote.fAlteration := staffNote.fAccidental;

         // Move on to the next scale degree
         degreeInterval := scaleFingeringFinder.scale.degreeInterval[degreeIndex];
         Inc(halftone, degreeInterval); // Next scale degree
         Inc(currentPosHalfTone, degreeInterval);
         Inc(degreeIndex);
      end;
   Invalidate;
end;

procedure TScalePatternChart.Clear;
begin
   fKey := ksCMajorAMinor;
   fDegrees.Clear;
   ItemIndex := -1;
   Invalidate;
end;

procedure TScalePatternChart.DrawNote(var x : integer; note : TStaffNote);
var
   xCopy : integer;
   s : string;
begin
   xCopy := x;
   canvas.font.color := GetQualityColor(note.quality, note.qualityAccidental);
   case note.characterTone of
      ctPrimary:
         canvas.font.style := [fsBold, fsUnderline];
      ctSecondary:
         canvas.font.style := [fsBold, fsItalic];
   end;

   // Draw the degree quality and quality accidental
   s := Chr(Ord(FIRST_QUALITY_CHAR) + Ord(note.quality));
   if not (note.qualityAccidental in [naNone, naNatural])
      then s := QUALITY_ACCIDENTAL_CHARACTERS[note.qualityAccidental] + s;
   xCopy := xCopy - canvas.TextWidth(s) div 2;
   canvas.TextOut(xCopy, GetPositionY(-1) + 2, s);
   inherited DrawNote(x, note);
end;


procedure TScalePatternChart.PlayDegree(index: integer);
begin
   if (index >= 0) and (index < fDegrees.count)
      then begin
              Assert(fDegrees[index] is TStaffNote);
              globalMidi.AllNotesOff;
              globalMidi.NoteOn(36 + TStaffNote(fDegrees[index]).absHalftone);
           end;
end;

end.

