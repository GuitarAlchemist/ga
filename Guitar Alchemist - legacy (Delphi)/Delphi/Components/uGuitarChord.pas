unit uGuitarChord;

interface

uses
  Windows, Messages, SysUtils, Classes, Controls, Graphics, Forms,
  uMusicClasses, uLayout, uMusicFontRoutines, Dialogs, uMidi, uChordVoicings,
  uVoicingFingersFinder;


const
  DEFAULT_RATIO = 6;
  DEFAULT_DOT_SIZE = 7;

  CAPTION_PERCENTAGE = 60;
  CAPTION_SPACER_PERCENTAGE = 7;
  FRET0_PERCENTAGE = 20;
  FRET0_SPACER_PERCENTAGE = 7;
  NUT_PERCENTAGE = 6;


type
  TString = 0..12;
  TFret = -1..40;

  RFretBoardPosition = record
     str : TString;
     fret : TFret;
     finger : integer;
     quality : THalfToneQuality;
     useSharp : boolean;
     tone : TTone;
     accidental : TNoteAccidental;
     characterTone : TCharacterTone;
  end;

  RFretBoardBarre = record
     firstStr : TString;
     fret : TFret;
  end;

  TFretBoardPositionFeature = (npSelected, npClicked, npFocused, npGrayed);
  TFretBoardPositionStyle = set of TFretBoardPositionFeature;

  TGuitarFretBoardMode = (nmChord, nmScale);


type
  TGuitarFretBoard = class(TCustomLayout)
     // Painting
     private
        fContentChanged : boolean;
        fFretLabelContentChanged : boolean;
        fStringOrFretCountChanged : boolean;
        fForceAdjustWidth : boolean;
        fRatioChanged : boolean;
        fChanged : boolean;

        fCursorColor : TColor;
        fClickedColor : TColor;
        fFixedLineWidth : boolean;
        fFret0FontHeight : integer;
        fFretSpace : integer;
        fStringSpace : integer;
        fAlignment : TAlignment;
        fRatio : integer;
        fShowCaption : boolean;
        fShowFret0 : boolean;
        fShowNut : boolean;
        fShowFretLabel : boolean;
        fShowFingers : boolean;
        fColorDegrees : boolean;
        fMode : TGuitarFretBoardMode;

        procedure SetRatio(value : integer);
        procedure SetCursorColor(value : TColor);
        procedure SetClickedColor(value : TColor);
        procedure SetShowNut(value : boolean);
        procedure SetShowFret0(value : boolean);
        procedure SetShowCaption(value : boolean);
        procedure SetAlignment(value : TAlignment);
        procedure SetFretLabel(value : integer); virtual;
        procedure SetShowNotes(value : boolean);
        procedure SetShowCharacterTones(value : boolean);
        procedure SetFretLabelPosition(value : integer);
        procedure SetShowFretLabel(value : boolean);
        procedure SetShowFingers(value : boolean);
        procedure SetFixedLineWidth(value : boolean);
        procedure SetColorDegrees(value : boolean);
     protected
        procedure CMTextChanged(var Message: TMessage); message CM_TEXTCHANGED;
        function WidthFromStringSpace(stringSpace : integer) : integer;
        function HeightFromFretSpace(fretSpace : integer) : integer;
        function StringSpaceFromWidth(width : integer) : integer;
        function FretSpaceFromHeight(height : integer) : integer;
        function CanResize(var newWidth, newHeight : integer) : boolean; override;
        procedure Changed; virtual;
        procedure MetricsChanged; override;
        procedure ComputeMetrics; override;
        procedure ComputeContent; virtual;
        procedure ComputeFretLabelContent; virtual;
        procedure ContentChanged; virtual;
        procedure FretLabelContentChanged; virtual;
        procedure Paint; override;
        procedure Loaded; override;

        property Aligment : TAlignment read fAlignment write SetAlignment default taLeftJustify;
        property ShowCaption : boolean read fShowCaption write SetShowCaption;
        property ShowFret0 : boolean read fShowFret0 write SetShowFret0;
        property ShowNut : boolean read fShowNut write SetShowNut;
        property ShowFretLabel : boolean read fShowFretLabel write SetShowFretLabel;
        property ShowFingers : boolean read fShowFingers write SetShowFingers;
        property Ratio : integer read fRatio write SetRatio;
        property CursorColor : TColor read fCursorColor write SetCursorColor;
        property ClickedColor : TColor read fClickedColor write SetClickedColor;
        property FixedLineWidth : boolean read fFixedLineWidth write SetFixedLineWidth;
        property ColorDegrees : boolean read fColorDegrees write SetColorDegrees;


    // Hot track
    private
        fHotTrack : boolean;
        fLastMouseOverControl : boolean;
        fMouseOverControl : boolean;
        fMousePosition : RFretBoardPosition;
        fMouseLastPosition : RFretBoardPosition;
        fMouseDown : boolean;
        fKeyDown : boolean;
    protected
        procedure DrawFocus;
        procedure MouseMove(shift : TShiftState; x, y : integer); override;
        procedure MouseUp(button: TMouseButton; shift : TShiftState; x, y : integer); override;
        procedure MouseDown(button : TMouseButton; shift : TShiftState; x, y : integer); override;
        procedure DblClick; override;
        property HotTrack : boolean read fHotTrack write fHotTrack;


     // Strings and frets
     private
        fStringCount : integer;
        fFretCount : integer;
        procedure DrawStringsAndFrets;
        procedure SetStringCount(value : integer); virtual;
        procedure SetFretCount(value : integer); virtual;

     // Events
     private
        fOnChange : TNotifyEvent;
     protected
        procedure DoChange; virtual;
        property OnChange : TNotifyEvent read fOnChange write fOnChange;

     // Positions
     private
        fSelectedPositions : array of RFretBoardPosition;
        fDotSize : integer;
        fLeftHanded : boolean;
        fPositionDotSize : integer;
        fFretsAndStringsPenSize : integer;
        fCursorDotSize : integer;
        fShowFret0Label : boolean;
        fShowNotes : boolean;
        fShowCharacterTones : boolean;
        fFretLabel : integer;
        fFretLabelPosition : integer;
        fSelectedDegreeIndex : integer;
        procedure SetDotSize(const value : integer);
        procedure SetLeftHanded(value : boolean);
        procedure SetSelectedDegreeIndex(value : integer);
        procedure DrawDot(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle = []);
        procedure DrawFret0Position(position : RFretBoardPosition; muted : boolean; style : TFretBoardPositionStyle);
        function GetSelectedPosition(index : integer) : RFretBoardPosition;
     protected
        procedure DrawPosition(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle = [npSelected]); virtual;
        procedure DrawAvailablePositions; virtual;
        procedure DrawSelectedPositions; virtual;
        function GetNoteName(position : RFretBoardPosition) : string;

        property FretLabel : integer read fFretLabel write SetFretLabel;
        property FretLabelPosition : integer read fFretLabelPosition write SetFretLabelPosition;
        property ShowNotes : boolean read fShowNotes write SetShowNotes;
        property ShowCharacterTones : boolean read fShowCharacterTones write SetShowCharacterTones;
     public
        function CheckString(str : TString) : boolean;
        function CheckFret(fret : TFret) : boolean;
        function CheckPosition(position : RFretBoardPosition) : boolean;
        function PositionIsSelected(var position : RFretBoardPosition) : boolean; virtual;
        function SelectedPositionCount : integer;
        function OpenStringIsSelected(position : RFretBoardPosition) : boolean;
        function MutedStringIsSelected(position : RFretBoardPosition) : boolean;
        procedure GetHalfTones(var halfTones : THalfToneArray);
        procedure SelectBarre(position : RFretBoardPosition);
        property SelectedPosition[index : integer] : RFretBoardPosition read GetSelectedPosition;
     public
        constructor Create(aOwner : TComponent); override;
        destructor Destroy; override;
        procedure Clear; virtual;
        procedure SetChord(frets : array of integer);
        function StringToX(str : TString) : integer; overload;
        function StringToX(position : RFretBoardPosition) : integer; overload;
        function StringFromX(x : integer; out str : TString) : boolean;
        function GetFretBoardPositionFromPoint(p : TPoint; out position : RFretBoardPosition) : boolean;
        function FretFromY(y : integer; out fret : TFret) : boolean;
        function FretToY(fret : TFret) : integer; overload;
        function FretToY(position : RFretBoardPosition) : integer; overload;
        function Fret0Y : integer;
        function NutY : integer;
        property StringCount : integer read fStringCount write SetStringCount;
        property FretCount : integer read fFretCount write SetFretCount;
        property DotSize : integer read fDotSize write SetDotSize default DEFAULT_DOT_SIZE;
        property LeftHanded : boolean read fLeftHanded write SetLeftHanded;
        property ItemIndex : integer read fSelectedDegreeIndex write SetSelectedDegreeIndex;
  end;

  TCustomGuitarScale = class(TGuitarFretBoard)
     private
        fPlaying : boolean;
        fShowPatterns : boolean;
        fExtended : boolean;
        fShiftToTopFret : boolean;        
        fScale : TScale;
        fScaleName : string;
        fScaleMode : integer;
        fPattern : integer;
        fMinPatternFret : integer;
        fMaxPatternFret : integer;
        fClicksDisabled : boolean;
        fActive : boolean;
        fLastTickCount : cardinal;
        fLastPlayedDegree : integer;
        fPlayMinDuration : cardinal;
        fKey : TKey;
        fFirstRootPosition : RFretBoardPosition;
        fMinFretBoardFret : integer;
        fKeyRoot : RHalfTone;
        fRootFret : TFret;
        procedure SetExtended(value : boolean); virtual;
        procedure SetScaleName(value : string);
        procedure SetScaleMode(value : integer);
        procedure SetPattern(value : integer);
        procedure SetShowPatterns(value : boolean);
        procedure SetKey(value : TKey);
        function GetSelectedDegreeQuality : THalfToneQuality;
        procedure SetPlayMinDuration(value : cardinal);
        function SelectDegreeIndex(position : RFretBoardPosition) : boolean;
     protected
        procedure CreateWnd; override;
        procedure WndProc(var message : TMessage); override;
        procedure CMFocusChanged(var Message: TCMFocusChanged); message CM_FOCUSCHANGED;
        procedure ComputeContent; override;
        procedure ComputeFretLabelContent; override;
        procedure MouseDown(button : TMouseButton; shift: TShiftState; x, y: integer); override;
        function DoMouseWheelDown(shift : TShiftState; mousePos : TPoint) : boolean; override;
        function DoMouseWheelUp(shift : TShiftState; mousePos : TPoint): boolean; override;
        procedure KeyDown(var key : word; shift : TShiftState); override;
        procedure KeyUp(var key : word; shift : TShiftState); override;
        procedure PlayDegree(index : integer); virtual;
        procedure DrawPosition(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle = [npSelected]); override;
        property Extended : boolean read fExtended write SetExtended;
        property ScaleName : string read fScaleName write SetScaleName;
        property ScaleMode : integer read fScaleMode write SetScaleMode;
        property Pattern : integer read fPattern write SetPattern;
        property ShowPatterns : boolean read fShowPatterns write SetShowPatterns;
        property Key : TKey read fKey write SetKey;
     public
        constructor Create(aOwner : TComponent); override;
        destructor Destroy; override;
        procedure SetScaleAndMode(scaleName : string; scaleMode : integer);        
        function PositionIsSelected(var position : RFretBoardPosition) : boolean; override;
        procedure SelectScale(scaleName : string; mode, pattern : integer);
        function FirstItemSelected : boolean;
        function LastItemSelected : boolean;
        function ScaleIsMinor : boolean;
        procedure FirstItem;
        procedure LastItem;
        procedure PrevItem(wrapFretBoard : boolean = false);
        procedure NextItem(wrapFretBoard : boolean = false);
        property ItemIndex;
        property DegreeQuality : THalfToneQuality read GetSelectedDegreeQuality;
        property PlayMinDuration : cardinal read fPlayMinDuration write SetPlayMinDuration;
  end;

  TGuitarScalePattern = class(TCustomGuitarScale)
     protected
        procedure PlayDegree(index : integer); override;
     public
        constructor Create(aOwner : TComponent); override;
     published
        property Align;
        property DotSize;
        property LeftHanded;
        property ColorDegrees;
        property HotTrack;
        property ScaleName;
        property ScaleMode;
        property Extended;
        property Pattern;
        property ItemIndex;
        property PlayMinDuration;
        property Font;
        property Visible;
        property Key;
        property Ratio;
        property ShowNotes;
        property ShowCaption;
        property ShowCharacterTones;        

        property OnChange;
  end;


  TGuitarScaleFretBoard = class(TCustomGuitarScale)
     private
        procedure SetExtended(value : boolean); override;
     protected
        procedure DrawAvailablePositions; override;
     public
        constructor Create(aOwner : TComponent); override;
     published
        property DotSize;
        property LeftHanded;
        property ColorDegrees;
        property HotTrack;
        property ScaleName;
        property ScaleMode;
        property Extended;
        property Pattern;
        property ShowPatterns;
        property ItemIndex;
        property PlayMinDuration;
        property Font;
        property OnChange;
        property Visible;
        property Key;
        property Ratio;
        property ShowNotes;                
  end;


  TGuitarChord = class(TGuitarFretBoard)
     private
        fShowOpenStrings : boolean;
        fShowChordNotes : boolean;
        fMinFret : integer;
        fMaxFret : integer;
        fFirstFingerBarreExtent : integer;
        fFirstFingerBarreStartingStr : integer;
        function GetPosition(str : TString) : RFretBoardPosition;
        procedure SetPosition(str : TString; const value : RFretBoardPosition);
        procedure SetShowOpenStrings(value : boolean);
        procedure SetShowChordNotes(value : boolean);
        procedure SetStringCount(value : integer); override;
        procedure SetFretLabel(value : integer); override;
     protected
        procedure SelectPosition(position : RFretBoardPosition);
        procedure DrawPosition(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle = [npSelected]); override;
        procedure Paint; override;
        procedure DrawChordNotes;
        procedure MouseDown(button : TMouseButton; shift: TShiftState; x, y: integer); override;
     public
        constructor Create(aOwnder : TComponent); override;
        destructor Destroy; override;
        procedure Clear; override;        
        procedure SetVoicing(voicing : TChordVoicing);
        property selectedPositions[str : TString] : RFretBoardPosition read GetPosition write SetPosition;
     published
        property ShowCaption;
        property Caption;
        property Aligment;
        property Align;
        property DotSize;
        property Ratio;
        property CursorColor;
        property ClickedColor;
        property Font;
        property FixedLineWidth;
        property HotTrack;                
        property ShowOpenStrings : boolean read fShowOpenStrings write SetShowOpenStrings;
        property ShowChordNotes : boolean read fShowChordNotes write SetShowChordNotes;
        property Visible;
        property ShowFret0;
        property ShowNut;
        property ShowFretLabel;
        property ShowFingers;
        property FretLabel;
        property FretLabelPosition;
        property LeftHanded;
        property OnChange;
  end;

  procedure GetNoteFret(halfTone : THalfTone; str : TString; out rootFret : TFret);

implementation

const
   DEFAULT_STRING_COUNT = 6;
   DEFAULT_FRET_COUNT = 5;


function SamePosition(pos1, pos2 : RFretBoardPosition) : boolean;
begin
   result := (pos1.str = pos2.str) and (pos1.fret = pos2.fret);
end;

procedure GetNoteFret(halfTone : THalfTone; str : TString; out rootFret : TFret);
var
   ht : THalfTone;
begin
   ht := STANDARD_GUITAR_TUNING[str].halfTone;
   rootFret := 0;
   while ht <> halfTone do
      begin
         if ht = htB
            then ht := htC
            else Inc(ht);
         Inc(rootFret);
      end;
end;


{ TGuitarFretBoard }

constructor TGuitarFretBoard.Create(aOwner : TComponent);
begin
   inherited Create(aOwner);
   controlStyle := [csCaptureMouse, csDesignInteractive, csOpaque, csReplicatable, csClickEvents, csDoubleClicks, csSetCaption];
   SetBounds(0, 0, 175, 250);

   fStringCount := DEFAULT_STRING_COUNT;
   fFretCount := DEFAULT_FRET_COUNT;
   fDotSize := DEFAULT_DOT_SIZE;
   fRatio := DEFAULT_RATIO;
   fLeftHanded := false;
   SetLength(fSelectedPositions, fStringCount);
   fCursorColor := $00B35900;
   fClickedColor := $004080FF;
   fColorDegrees := false;   

   fShowCaption := true;
   fShowFret0 := true;
   fShowNut := true;
   fShowFretLabel := true;
   fShowFingers := false;
   fFretLabel := 0;
   fFretLabelPosition := 0;
   fShowFret0Label := true;
   fShowNotes := false;
   fShowCharacterTones := true;      
   fFixedLineWidth := true;
   fMode := nmChord;
   fSelectedDegreeIndex := -1;

   fOnChange := nil;

   topPropOffsetPercentage := 30;
   rightPropOffsetPercentage := 40;

   Clear;

   fContentChanged := true;
   fFretLabelContentChanged := true;
   fStringOrFretCountChanged := false;

   fForceAdjustWidth := false;
   fRatioChanged := false;

   DoubleBuffered := true;
end;

destructor TGuitarFretBoard.Destroy;
begin
   inherited Destroy;
end;

procedure TGuitarFretBoard.DoChange;
begin
   if Assigned(fOnChange)
      then OnChange(self);
end;

function TGuitarFretBoard.StringToX(str : TString) : integer;
begin
   result := Trunc(layoutLeft + fStringSpace * (str - 0.5));
end;

function TGuitarFretBoard.StringToX(position : RFretBoardPosition) : integer;
begin
   result := StringToX(position.str);
end;

function TGuitarFretBoard.StringFromX(x : integer; out str : TString) : boolean;
begin
   // Retrieve the string
   result := (x > layoutLeft) and (x < layoutRight - 1);
   if result
      then begin
              Assert(fStringSpace <> 0);
              str := Round((x - layoutLeft) / fStringSpace + 0.5);
           end;
   if str > fStringCount
      then result := false;

   // Invert string if left-handed
   if fLeftHanded
      then str := fStringCount - str + 1;
end;

function TGuitarFretBoard.FretToY(fret : TFret) : integer;
begin
   if fret < 0
      then fret := 0;
   Assert(FretCount <> 0);
   result := Trunc(layoutTopOffset + ((fret - 1) * layoutHeight) / FretCount);
end;

function TGuitarFretBoard.FretToY(position : RFretBoardPosition) : integer;
begin
   result := FretToY(position.fret);
end;

function TGuitarFretBoard.Fret0Y : integer;
begin
   result := Trunc(topPropOffset * (CAPTION_PERCENTAGE + CAPTION_SPACER_PERCENTAGE) * Ord(fShowCaption) / 100);
end;

function TGuitarFretBoard.NutY : integer;
begin
  result := Fret0Y + Trunc(topPropOffset * (FRET0_PERCENTAGE + FRET0_SPACER_PERCENTAGE) * Ord(fShowNut) / 100);
end;

function TGuitarFretBoard.GetFretBoardPositionFromPoint(p : TPoint; out position : RFretBoardPosition) : boolean;
begin
   result := StringFromX(p.X, position.str) and FretFromY(p.Y, position.fret);
end;

function TGuitarFretBoard.FretFromY(y : integer; out fret : TFret) : boolean;
begin
   result := (y > Fret0Y) and (y < layoutBottom);
   if result
      then begin
                   if y < layoutTop
                      then begin
                              fret := 0;
                              result := fShowFret0;
                           end
              else if y > layoutBottom
                      then fret := fretCount - 1
              else         fret := ((y - layoutTopOffset + 1) * fretCount) div layoutHeight + 1;
           end;
end;

procedure TGuitarFretBoard.DrawStringsAndFrets;
var
   index : integer;
   x, y : integer;
   x1, x2 : integer;
begin
   // Draw strings
   canvas.pen.color := clBlack;
   if fFixedLineWidth
      then canvas.pen.width := 1
      else canvas.pen.width := fFretsAndStringsPenSize;
   canvas.pen.mode := pmCopy;
   canvas.brush.color := clBlack;
   for index := 1 to fStringCount do
      begin
         x := StringToX(index);
         canvas.MoveTo(x, FretToY(1));
         canvas.LineTo(x, FretToY(fFretCount + 1));
      end;

   // Draw nut and frets
   x1 := layoutLeft + StringToX(1);
   x2 := layoutLeft + StringToX(fStringCount);

   // Nut
   if fShowNut and (fFretLabel = 0)
      then begin
              y := FretToY(1);
              canvas.Rectangle(x1, NutY, x2 + 1, y);
           end;

   // Other frets
   for index := 1 to fFretCount + 1 do
      begin
         y := FretToY(index);
         canvas.MoveTo(x1, y);
         canvas.LineTo(x2, y);
      end;
end;

procedure TGuitarFretBoard.Paint;
const
  Alignments : array[TAlignment] of Longint = (DT_LEFT, DT_RIGHT, DT_CENTER);
var
   r : TRect;
   flags : Longint;
   ch : char;
   tm : TTextMetric;
   x, y : integer;
begin
   inherited Paint;

   // Maybe compute the content
   if fContentChanged
      then try
                 ComputeContent;
                 fChanged := true;
              finally
                 fContentChanged := false;
           end;

   // Maybe compute the fret label
   if fFretLabelContentChanged
      then try
                 ComputeFretLabelContent;
                 fChanged := true;
              finally
                 fFretLabelContentChanged := false;
           end;

   if fChanged
      then try
                 DoChange;
              finally
                 fChanged := false;   
           end;

   // Clear background
   canvas.pen.mode := pmCopy;
   canvas.pen.color := clWhite;
   canvas.brush.color := clWhite;
   canvas.brush.style := bsSolid;    
   canvas.Rectangle(0, 0, width, height);

   // Draw the caption
   if fShowCaption
      then begin
              canvas.font := self.font;
              canvas.font.height := - Round(topPropOffset * CAPTION_PERCENTAGE / 100);
              flags := DT_EXPANDTABS or DT_VCENTER or Alignments[fAlignment];
              flags := DrawTextBiDiModeFlags(flags);
              r := GetClientRect;
              DrawText(canvas.handle, PChar(caption), -1, r, flags);
           end;

   // Draw the fret label
   if fShowFretLabel and ((fFretLabel + fFretLabelPosition > 0) or (fFretLabel + fFretLabelPosition = 0) and fShowFret0Label)
      then begin
              canvas.font.name := CHORDS_FONT_NAME;
              canvas.font.color := clBlack;
              GetTextMetrics(canvas.handle, tm);

              canvas.font.height := Round(fFretSpace * 0.75);
              canvas.font.style := [];              
              ch := GetFretLabelChar(fFretLabel + fFretLabelPosition);
              x := layoutRight;
              y := FretToY(1 + fFretLabelPosition) + fFretSpace div 2;
              canvas.TextOut(x + 5, y - canvas.TextHeight(ch) div 2, ch);
           end;

   // Draw the FretBoard and selected positions
   DrawStringsAndFrets;
   DrawAvailablePositions;
   DrawSelectedPositions;

   // Draw the mouse position
   if fHotTrack and not (csDesigning in ComponentState)
      then DrawFocus;
end;

procedure TGuitarFretBoard.Loaded;
var
   w, h : integer;
begin
   inherited Loaded;
   if csDesigning in ComponentState
      then begin
              w := width;
              h := height;
              offsetsHaveChanged := false;
              fForceAdjustWidth := true;
              try
                    if CanResize(w, h)
                       then SetBounds(left, top, w, h);
                    offsetsHaveChanged := true;
                 finally
                    fForceAdjustWidth := false;
              end;
           end;
end;

procedure TGuitarFretBoard.SetFretCount(value : integer);
var
   delta : integer;
begin
   if value < 1
      then value := 1;
   if value <> fFretCount
      then begin
              delta := value - fFretCount;
              fFretCount := value;
              fStringOrFretCountChanged := true;
              height := height + delta * fFretSpace;
              MetricsChanged;
           end;
end;

procedure TGuitarFretBoard.SetStringCount(value : integer);
begin
        if value < 4
           then value := 4
   else if value > 7
           then value := 7;
   if value <> fStringCount
      then begin
              fStringOrFretCountChanged := true;
              fStringCount := value;
              MetricsChanged;
           end;
end;

procedure TGuitarFretBoard.DrawAvailablePositions;
begin
   // To be implemented by descendent
end;

procedure TGuitarFretBoard.DrawSelectedPositions;
var
   index : integer;
begin
   for index := 0 to Length(fSelectedPositions) - 1 do
      DrawPosition(fSelectedPositions[index], index);
end;

procedure TGuitarFretBoard.DrawPosition(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle);
begin
   if position.fret <> -1
      then DrawDot(position, index, style);
end;

function TGuitarFretBoard.CheckString(str : TString) : boolean;
begin
   result := (str >= 1) and (str <= fStringCount);
end;

function TGuitarFretBoard.CheckFret(fret : TFret) : boolean;
begin
   result := (fret >= -1) and (fret <= fFretCount);
end;

function TGuitarFretBoard.CheckPosition(position : RFretBoardPosition) : boolean;
begin
   result := CheckString(position.str) and CheckFret(position.fret);
end;

function TGuitarFretBoard.PositionIsSelected(var position : RFretBoardPosition) : boolean;
begin
    if CheckPosition(position)
       then result := (fSelectedPositions[position.str - 1].fret = position.fret)
       else result := false;
end;

function TGuitarFretBoard.SelectedPositionCount : integer;
begin
   result := Length(fSelectedPositions) - 1;
end;

function TGuitarFretBoard.OpenStringIsSelected(position : RFretBoardPosition) : boolean;
begin
    if CheckPosition(position)
       then result := (fSelectedPositions[position.str - 1].fret = 0)
       else result := false;
end;

function TGuitarFretBoard.MutedStringIsSelected(position : RFretBoardPosition) : boolean;
begin
    if CheckPosition(position)
       then result := (fSelectedPositions[position.str - 1].fret = -1)
       else result := false;
end;

procedure TGuitarFretBoard.SetDotSize(const value : integer);
begin
   if (value <> fDotSize)
      and (value >= 1) and (value <= 10)
      then begin
              fDotSize := value;
              MetricsChanged;
           end;
end;

procedure TGuitarFretBoard.SetLeftHanded(value : boolean);
begin
   if value <> fLeftHanded
      then begin
              fLeftHanded := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetSelectedDegreeIndex(value : integer);
begin
   if (value < 0) or (value > Length(fSelectedPositions) - 1)
      then value := -1;

   if value <> fSelectedDegreeIndex
      then begin
              fSelectedDegreeIndex := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.Clear;
var
   str : integer;
   position : RFretBoardPosition;
begin
   position.fret := 0;
   for str := 0 to fStringCount - 1 do
      begin
         position.str := str + 1;
         fSelectedPositions[str] := position;
      end;
   Changed;
end;

procedure TGuitarFretBoard.SetChord(frets : array of integer);
var
   positionCount : integer;
   index : integer;
begin
   positionCount := Length(frets);
   if Length(fSelectedPositions) < positionCount
      then positionCount := Length(fSelectedPositions);
   for index := 0 to positionCount - 1 do
      begin
         fSelectedPositions[index].str := index;
         fSelectedPositions[index].fret := frets[index];
         fSelectedPositions[index].quality := htqUnison;
      end;
   Changed;
end;

function TGuitarFretBoard.WidthFromStringSpace(stringSpace : integer) : integer;
begin
   result := Round(
                (fStringCount * stringSpace) * ((1 + (actualLeftPropOffsetPercentage + actualRightPropOffsetPercentage) / 100))
                + actualLeftOffset + actualRightOffset
             );
end;

function TGuitarFretBoard.HeightFromFretSpace(fretSpace : integer) : integer;
begin
   result := Round(
                (fFretCount * fretSpace) * ((1 + (actualTopPropOffsetPercentage + actualBottomPropOffsetPercentage) / 100))
                + actualTopOffset + actualBottomOffset
             );
end;

function TGuitarFretBoard.StringSpaceFromWidth(width : integer) : integer;
begin
   Assert(fStringCount * 2 * (1 + (actualLeftPropOffsetPercentage + actualRightPropOffsetPercentage) / 100) <> 0);
   result := 2 * Round(
                (width - actualLeftOffset - actualRightOffset - 1) /
                (fStringCount * 2 * (1 + (actualLeftPropOffsetPercentage + actualRightPropOffsetPercentage) / 100))
             );
end;

function TGuitarFretBoard.FretSpaceFromHeight(height : integer) : integer;
begin
   Assert(fFretCount * (1 + (actualTopPropOffsetPercentage + actualBottomPropOffsetPercentage) / 100) <> 0);
   result := Round(
                (height - actualTopOffset - actualBottomOffset - 1) /
                (fFretCount * (1 + (actualTopPropOffsetPercentage + actualBottomPropOffsetPercentage) / 100))
             );
end;

function TGuitarFretBoard.CanResize(var newWidth, newHeight : integer) : boolean;
var
   newStringSpace : integer;
   newFretSpace : integer;
   sizeIncrease : boolean;

   procedure AdjustWidth;
   begin
      if newStringSpace = fStringSpace
         then begin
                 if sizeIncrease
                    then newStringSpace := newStringSpace + 2
                    else newStringSpace := newStringSpace - 2;
              end;

      if (newStringSpace > 5)
         then begin
                 newWidth := WidthFromStringSpace(newStringSpace);
                 newFretSpace := Round(newStringSpace * (0.5 + fRatio * 0.1));
                 newHeight := HeightFromFretSpace(newFretSpace);
                 result := true;
                 fMetricsChanged := true;
              end
         else result := false;
   end;

   procedure AdjustHeight;
   begin
      newFretSpace := FretSpaceFromHeight(newHeight);
      newStringSpace := Round(newFretSpace / (0.5 + fRatio * 0.1));
      AdjustWidth;
   end;

begin
   if fComputingMetrics
      then Exit;

   // Adjust new size
        if offsetsHaveChanged or fStringOrFretCountChanged
           then begin
                   offsetsHaveChanged := false;
                   fStringOrFretCountChanged := false;
                   result := true;
                end
   else if fRatioChanged
           then begin
                   fRatioChanged := false;
                   result := true
                end
   else         begin
                        if (newWidth <> width) or fForceAdjustWidth
                           then begin
                                   sizeIncrease := newWidth > width;
                                   newStringSpace := StringSpaceFromWidth(newWidth);
                                   AdjustWidth;
                                end
                   else if (newHeight = height) and (newWidth = width)
                           then result := true
                   else         begin
                                   sizeIncrease := newHeight > height;
                                   AdjustHeight;
                                end;
                end;
end;

procedure TGuitarFretBoard.CMTextChanged(var Message: TMessage);
begin
   if fShowCaption
      then Changed;
end;

procedure TGuitarFretBoard.ContentChanged;
begin
   fContentChanged := true;
   Changed;
end;

procedure TGuitarFretBoard.FretLabelContentChanged;
begin
   fFretLabelContentChanged := true;
   Changed;
end;

procedure TGuitarFretBoard.Changed;
begin
   fChanged := true;
   Invalidate;
end;

procedure TGuitarFretBoard.MetricsChanged;
begin
   fMetricsChanged := true;
   Changed;
end;

procedure TGuitarFretBoard.ComputeMetrics;
begin
   inherited ComputeMetrics;

   Assert(fStringCount <> 0);
   Assert(fretCount <> 0);
   fStringSpace := Trunc(layoutWidth / ((fStringCount) * 2)) * 2;
   fFretSpace := Trunc(layoutHeight / ((fretCount) * 2)) * 2;

   if fFretSpace < fStringSpace
      then begin
              fPositionDotSize := Round(fFretSpace * fDotSize / 10);
              fFretsAndStringsPenSize := Round(fFretSpace * 0.05);
              if fDotSize >= 4
                 then fCursorDotSize := Round(fFretSpace * (fDotSize - 2) / 10)
                 else fCursorDotSize := Round(fFretSpace * 4 / 10);
              fFret0FontHeight := fStringSpace;
           end
      else begin
              fPositionDotSize := Round(fStringSpace * fDotSize / 10);
              fFretsAndStringsPenSize := Round(fStringSpace * 0.05);
              if fDotSize >= 4
                 then fCursorDotSize := Round(fStringSpace * (fDotSize - 2) / 10)
                 else fCursorDotSize := Round(fStringSpace * 4 / 10);
              fFret0FontHeight := fFretSpace;
           end;

   // Ajust right offset
   useRightPropOffset := fShowFretLabel;

   // Adjust bottom offset
   topPropOffsetCoeff := (CAPTION_PERCENTAGE + CAPTION_SPACER_PERCENTAGE) * Ord(fShowCaption)
                         + (FRET0_PERCENTAGE + FRET0_SPACER_PERCENTAGE) * Ord(fShowFret0)
                         + NUT_PERCENTAGE * Ord(fShowNut);
end;

procedure TGuitarFretBoard.ComputeContent;
begin
   // To be implemented by decendent
end;

procedure TGuitarFretBoard.ComputeFretLabelContent;
begin
   // To be implemented by decendent
end;

procedure TGuitarFretBoard.GetHalfTones(var halfTones : THalfToneArray);
var
   str : integer;
   halfTone : RHalfTone;
   position : RFretBoardPosition;
begin
   SetLength(halfTones, 0);
   for str := 0 to fStringCount - 1 do
      begin
         position := fSelectedPositions[str];
         if position.fret <> -1
            then begin
                    SetLength(halfTones, Length(halfTones) + 1);
                    halfTone := STANDARD_GUITAR_TUNING[str + 1];
                    ShiftHalfTone(halfTone, position.fret);
                    halfTones[High(halfTones)] := halfTone;
                 end;
      end;
end;

function TGuitarFretBoard.GetNoteName(position: RFretBoardPosition): string;
var
   halfTone : RHalfTone;
begin
   if position.fret <> -1
      then begin
              halfTone := STANDARD_GUITAR_TUNING[position.str];
              ShiftHalfTone(halfTone, position.fret);
              result := HALFTONE_NAME_SMART[halfTone.halfTone];
           end
      else result := '';
end;

procedure TGuitarFretBoard.SelectBarre(position : RFretBoardPosition);
var
   str : TString;
begin
   for str := position.str to fStringCount do
      begin
         position.str := str;
         fSelectedPositions[str - 1] := position;
      end;
end;

procedure TGuitarFretBoard.DrawFret0Position(position : RFretBoardPosition; muted : boolean; style : TFretBoardPositionStyle);
var
   x, y : integer;
   tm : TTextMetric;
   ch : char;
begin
   if fShowFret0
      then begin
              if fLeftHanded
                 then position.str := fStringCount + 1 - position.str;

              x := StringToX(position);
              y := Fret0Y;

              canvas.font.charset := SYMBOL_CHARSET;
              canvas.font.name := STAFF_FONT_NAME;
              canvas.font.height := - Round(topPropOffset * FRET0_PERCENTAGE / 100);
              canvas.font.style := [fsBold];
              if npClicked in style
                 then canvas.font.style := canvas.font.style + [fsUnderline];
              canvas.brush.style := bsClear;
              if muted
                 then ch := MUTED_POSITION
                 else ch := OPEN_POSITION;
              if npFocused in style
                 then begin
                         if npClicked in style
                            then canvas.font.color := fClickedColor
                            else canvas.font.color := fCursorColor;
                      end
                 else canvas.font.color := clBlack;
              GetTextMetrics(canvas.handle, tm);
              canvas.TextOut(x - Round(canvas.TextWidth(ch) /2), y - Round(canvas.TextHeight(ch) / 2) + 4, ch);
           end;   
end;

function TGuitarFretBoard.GetSelectedPosition(index : integer) : RFretBoardPosition;
begin
   if (index > 0) and (index <= SelectedPositionCount)
      then result := fSelectedPositions[index]
      else begin
              result.str := 0;
              result.fret := 0;
              result.quality := htqUnison;
              result.useSharp := false;
           end;
end;

procedure TGuitarFretBoard.DrawDot(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle);
var
   x, y, s : integer;
   tm : TTextMetric;
   ch : char;
   qualityColor : TColor;

   function GetPositionChar(position : RFretBoardPosition) : char;
   begin
      if fShowNotes
         then result := GetToneChar(position.tone, position.accidental)
         else result := GetHalfToneQualityChar(position.quality, position.useSharp);
   end;

   procedure DrawFrame(thick : boolean = false);
   const
      ROUND_COEFF = 0.4;
   var
      offset : integer;
      roundX, roundY : integer;
   begin
      if thick
         then offset := 1
         else offset := 0;
      if fStringSpace < fFretSpace
         then begin
                 roundX := Round(fStringSpace * ROUND_COEFF);
                 roundY := roundX;
                 canvas.RoundRect(x - fStringSpace div 2 + offset, y - fStringSpace div 2 + offset,
                                  x +  fStringSpace div 2 - offset, y + fStringSpace div 2 - offset, roundX, roundY)
              end
         else begin
                 roundX := Round(fFretSpace * ROUND_COEFF);
                 roundY := roundX;
                 canvas.RoundRect(x - fFretSpace div 2 + offset, y - fFretSpace div 2 + offset,
                                  x +  fFretSpace div 2 - offset, y + fFretSpace div 2 - offset, roundX, roundY);
              end;
   end;

   procedure DrawSelectedDegreeIndex;
   begin
      if position.quality = htqUnison
         then // Root position
              begin
                 canvas.font.color := $808080;
                 canvas.brush.style := bsClear;
                 ch := NORMAL_POSITION_SMALL;
                 canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);

                 if fMouseDown or fKeyDown
                    then canvas.pen.color := fClickedColor
                    else begin
                            if fColorDegrees
                               then canvas.pen.color := $808080
                               else canvas.pen.color := fCursorColor;
                         end;      
                 canvas.pen.style := psSolid;
                 canvas.pen.width := 3;                 
                 DrawFrame(false);
              end
         else
              begin
                 // Draw the background
                 if fColorDegrees
                    then canvas.font.color := GetQualityColor(position.quality)
                    else canvas.font.color := clBlack;
                 canvas.brush.style := bsClear;
                 ch := NORMAL_POSITION;
                 canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);

                 // Draw the foreground in white
                 canvas.font.color := clWhite;
                 ch := GetPositionChar(position);
                 canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);
                 if fMouseDown or fKeyDown
                    then canvas.pen.color := fClickedColor
                    else begin
                            if fColorDegrees
                               then canvas.pen.color := GetQualityColor(position.quality)
                               else canvas.pen.color := fCursorColor;
                         end;
                 canvas.pen.style := psSolid;
                 canvas.pen.width := 3;                 
                 DrawFrame(false);
              end;
   end;

begin
   if fLeftHanded
      then position.str := fStringCount + 1 - position.str;

   s := fPositionDotSize;

   canvas.font.charset := SYMBOL_CHARSET;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := Round(s * 2.38);
   canvas.font.style := [fsBold];
   canvas.pen.mode := pmCopy;
   canvas.brush.style := bsClear;
   GetTextMetrics(canvas.handle, tm);

   x := StringToX(position.str);
   y := FretToY(position.fret) + Round(fFretSpace / 2);
   qualityColor := GetQualityColor(position.quality);

   if npFocused in style
      then // Focused position
           begin
              if fMode = nmChord
                 then // Chord mode
                      begin
                         if not (npSelected in style)
                            then begin
                                    canvas.font.color := fCursorColor;
                                    ch := NORMAL_POSITION_TINY;
                                    canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);
                                 end;

                         if position.quality = htqUnison
                            then begin
                                    ch := NORMAL_POSITION_TINY;
                                    if npClicked in style
                                       then canvas.font.color := fClickedColor
                                       else canvas.font.color := fCursorColor;
                                    canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);
                                 end;
                      end
                 else // Scale mode
                      begin
                         // Draw a rounded frame
                         if npClicked in style
                            then canvas.pen.color := fClickedColor
                            else canvas.pen.color := fCursorColor;
                         canvas.pen.width := 1;
                         DrawFrame;
                      end;
           end
      else // Normal position
           begin
              if (fMode = nmScale) and (index = fSelectedDegreeIndex) and (fSelectedDegreeIndex <> -1)
                 then DrawSelectedDegreeIndex
                 else begin
                         canvas.font.color := clWhite;
                         canvas.brush.style := bsClear;
                         ch := NORMAL_POSITION;
                         canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);

                              if npGrayed in style
                                 then canvas.font.color := $D4D4D4
                         else if fColorDegrees
                                 then canvas.font.color := qualityColor
                         else         canvas.font.color := clBlack;

                         if fShowFingers and (position.finger <> -1) 
                            then ch := GetFingerChar(position.finger)
                            else ch := GetPositionChar(position);
                         canvas.TextOut(x - Trunc(canvas.TextWidth(ch) /2), y - tm.tmAscent, ch);
                      end;
           end;

   // Maybe draw character tone mark
   if fShowCharacterTones and (position.characterTone <> ctNone)
      then begin
                   if npGrayed in style
                      then canvas.pen.color := $D4D4D4
              else if fColorDegrees
                      then canvas.pen.color := qualityColor
              else         canvas.pen.color := clBlack;

              if position.characterTone = ctPrimary
                 then canvas.pen.width := 3
                 else canvas.pen.width := 1;
              canvas.MoveTo(x - fPositionDotSize div 2, y + fPositionDotSize div 2);
              canvas.LineTo(x + fPositionDotSize div 2, y + fPositionDotSize div 2);
           end;
end;

procedure TGuitarFretBoard.DrawFocus;
var
   positionStyle : TFretBoardPositionStyle;
begin
   // Maybe draw the selected position
   if fMouseOverControl
      then begin
              if (fMousePosition.fret = 0) and OpenStringIsSelected(fMousePosition)
                 then fMousePosition.fret := -1;
              positionStyle := [npFocused];
              if PositionIsSelected(fMousePosition)
                 then Include(positionStyle, npSelected);
              if fMouseDown
                 then Include(positionStyle, npClicked);
              DrawPosition(fMousePosition, -1, positionStyle);
           end;
end;

procedure TGuitarFretBoard.MouseMove(shift : TShiftState; x, y : integer);
   function MousePositionChanged : boolean;
   begin
      result := false;
           if fMouseLastPosition.str <> fMousePosition.str
              then result := true
      else if (fMouseLastPosition.fret <> fMousePosition.fret)
               and not ((fMouseLastPosition.fret = 0) and (fMousePosition.fret = -1)
                       or (fMouseLastPosition.fret = -1) and (fMousePosition.fret = 0))
              then result := true;
   end;

begin
   if fHotTrack and not (fMetricsChanged) and not (csDesigning in ComponentState)
      then begin
              fMouseOverControl := GetFretBoardPositionFromPoint(point(x,y), fMousePosition);
                   if fMouseOverControl
                      then begin
                              if (not SamePosition(fMousePosition, fMouseLastPosition)) and MousePositionChanged
                                 then begin
                                         Invalidate;
                                         if fMouseOverControl and ((ssLeft in shift) or (ssRight in shift))
                                            then MouseDown(mbLeft, shift, x, y);
                                      end;
                              fMouseLastPosition.str := fMousePosition.str;
                              fMouseLastPosition.fret := fMousePosition.fret;
                              if fMouseLastPosition.fret = 0
                                 then fMouseLastPosition.fret := -1;
                              screen.cursor := crHandPoint;
                           end
              else if fLastMouseOverControl
                   then begin
                           screen.cursor := crDefault;
                           fMouseLastPosition.str := 12;
                           fMouseLastPosition.fret := 40;
                           Invalidate;
                        end;
              mouseCapture := fMouseOverControl;
              fLastMouseOverControl := fMouseOverControl;
           end;

   fMouseDown := (ssLeft in shift) or (ssRight in shift);

   inherited MouseMove(Shift, X, Y);
end;

procedure TGuitarFretBoard.MouseUp(button: TMouseButton; shift : TShiftState; x, y : integer);
begin
   fMouseDown := false;
   Invalidate;
   inherited MouseUp(button, shift, x, y);
end;

procedure TGuitarFretBoard.MouseDown(button : TMouseButton; shift : TShiftState; x, y : integer);
begin
   inherited MouseDown(button, shift, x, y);
   if not (ssDouble in shift)
      then fMouseDown := true;
end;

procedure TGuitarFretBoard.DblClick;
begin
   fMouseDown := true;
   inherited DblClick;
end;

procedure TGuitarChord.SetShowOpenStrings(value : boolean);
begin
   if value <> fShowOpenStrings
      then begin
              fShowOpenStrings := value;
              Changed;
           end;
end;

procedure TGuitarChord.SetShowChordNotes(value : boolean);
begin
{
   if value <> fShowChordNotes
      then begin
              fShowChordNotes := value;
              if value
                 then begin
                         fBottomOffset := fFret0Offset;
                         //MetricsChanged;

                         height := height + fFret0Offset;
                      end
                 else begin
                         fBottomOffset := 0;

                         height := height - fFret0Offset;

                      end;
           end;
}
end;

constructor TGuitarChord.Create(aOwnder: TComponent);
begin
   inherited Create(aOwnder);
   fShowOpenStrings := false;
   fShowChordNotes := true;
   fShowFret0Label := false;
   fHotTrack := true;
   fMinFret := -1;
   fMaxFret := -1;   
   fFirstFingerBarreExtent := -1;
   fFirstFingerBarreStartingStr := -1;
end;

destructor TGuitarChord.Destroy;
begin
   SetLength(fSelectedPositions, 0);
   inherited Destroy;
end;

procedure TGuitarChord.SetVoicing(voicing : TChordVoicing);
var
   voicingFingersFinder : TVoicingFingersFinder;
   firstFingering : TFingeringItem;
   position : RFretBoardPosition;
   positionCount : integer;
   str, fret : integer;
   finger : integer;
   barreFinger : integer;
   fingerIndex : integer;
begin
   Clear;
   fMinFret := voicing.minFret;
   fMaxFret := voicing.maxFret;
   fFirstFingerBarreExtent := voicing.firstFingerBarreExtent;
   fFirstFingerBarreStartingStr := voicing.firstFingerBarreStartingStr;

   voicingFingersFinder := TVoicingFingersFinder.Create;
   try
         voicingFingersFinder.voicing := voicing;
         if voicingFingersFinder.fingerOrders.count > 0
            then begin
                    Assert(voicingFingersFinder.fingerOrders.objects[0] is TFingeringItem);
                    firstFingering := TFingeringItem(voicingFingersFinder.fingerOrders.objects[0]);
                 end;
         ShowOpenStrings := true;
         if voicing.maxFret > 5
            then begin
                    FretLabel := voicing.minFret;
                    ShowFretLabel := false;
                 end
            else ShowFretLabel := false;
         fingerIndex := 0;
         for str := 0 to 5 do
            begin
               fret := voicing.formula[str];
               if (voicing.maxFret > 5) and (fret > 0)
                  then Dec(fret, voicing.minFret - 1);               
               if fret > 0
                  then begin
                               if (fret = voicing.minFret)
                                  and (fFirstFingerBarreExtent > 1)
                                  and (str = fFirstFingerBarreStartingStr - fFirstFingerBarreExtent + 1)
                                  then begin
                                          barreFinger := firstFingering.order[fingerIndex];
                                          finger := barreFinger;
                                          Inc(fingerIndex);
                                       end
                          else if (fret = voicing.minFret)
                               and (fFirstFingerBarreExtent > 1)
                               and (str >= fFirstFingerBarreStartingStr - fFirstFingerBarreExtent + 1)
                                  then finger := barreFinger
                          else         begin
                                          finger := firstFingering.order[fingerIndex];
                                          Inc(fingerIndex);
                                       end
                       end;

               position.str := str + 1;
               position.fret := fret;
               position.quality := htqUnison;
               position.characterTone := ctNone;
               position.finger := finger;

               selectedPositions[position.str] := position;
            end;
         Changed;

      finally
         voicingFingersFinder.Free;
   end;
end;

procedure TGuitarChord.DrawPosition(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle);
begin
        if position.fret = -1
           then begin
                   if fShowNut
                      then DrawFret0Position(position, true, style)
                end
   else if position.fret = 0
           then begin
                   if (fShowOpenStrings or (npFocused in style)) and fShowNut
                      then DrawFret0Position(position, false, style)
                end
   else         inherited DrawPosition(position, index, style);
end;

procedure TGuitarChord.Paint;

   procedure DrawBarre;
   var
      fret : integer;
      xRight, xLeft : integer;
      x, y, w, h : integer;
      ch : char;
   begin
      fret := fMinFret;
      if fMaxFret > 5
         then fret := 1;
      xRight := StringToX(fFirstFingerBarreStartingStr + 1);
      xLeft := StringToX(fFirstFingerBarreStartingStr - fFirstFingerBarreExtent + 2);
      ch := GetBarreChar(fFirstFingerBarreExtent - 1);
      x := xLeft;
      w := xRight - xLeft;
      y := FretToY(fret) - 2;
      h := FretToY(1);
      canvas.font.name := STAFF_FONT_NAME;
      canvas.font.height := Round(h * 10);
      canvas.TextOut(x, y - Round(canvas.TextHeight(ch) / 2 + (h / 1.5)), ch);
{
      canvas.pen.width := fPositionDotSize div 3;
      canvas.Arc(x, y, x + w, y + h,
                 x + w, y + h div 2, x, y + h div 2);
}
   end;

begin
   inherited Paint;
   if fFirstFingerBarreExtent > 0
      then DrawBarre;
   DrawChordNotes;
end;

procedure TGuitarChord.DrawChordNotes;
var
   str : integer;
   position : RFretBoardPosition;
   noteName : string;
   x, y : integer;
begin
   if fShowChordNotes
      then begin
              canvas.brush.style := bsClear;
              canvas.font.name := 'Verdana';
              canvas.font.height := -bottomPropOffset;
              canvas.font.color := $005B3302;
              canvas.font.style := [fsBold];
              y := layoutBottom; //height - (integer(fBottomOffset)); //- canvas.font.height) div 2;
              for str := 1 to fStringCount do
                 begin
                    position := fSelectedPositions[str - 1];
                    noteName := GetNoteName(position);
                    if noteName <> ''
                       then begin
                               if position.fret = 0
                                  then canvas.font.style := []
                                  else canvas.font.style := [fsBold];
                               x := StringToX(str) - canvas.TextWidth(noteName) div 2;
                               canvas.TextOut(x, y, noteName);
                            end;
                 end;
           end;
end;

procedure TGuitarChord.MouseDown(button : TMouseButton; shift: TShiftState; x, y: integer);
begin
   inherited;
   if fMouseOverControl
      then SelectPosition(fMousePosition);
end;

procedure TGuitarChord.SetStringCount(value : integer);
begin
   if value <> fStringCount
      then begin
              fStringCount := value;
              SetLength(fSelectedPositions, value);
              Changed;
           end;
end;

procedure TGuitarChord.SetFretLabel(value : integer);
begin
   if value < 0
      then value := 0;
   inherited SetFretLabel(value);   
end;

function TGuitarChord.GetPosition(str : TString) : RFretBoardPosition;
begin
   if CheckString(str)
      then result := fSelectedPositions[str - 1]
      else begin
              result.str := 0;
              result.fret := 0;
           end;
end;

procedure TGuitarChord.SetPosition(str : TString; const value : RFretBoardPosition);
begin
   if (value.str = str) and CheckPosition(value)
      then begin
              fSelectedPositions[str - 1] := value;
              Changed;
           end;
end;

procedure TGuitarChord.SelectPosition(position : RFretBoardPosition);
begin
   if fLeftHanded
      then position.str := fStringCount + 1 - position.str;

        if MutedStringIsSelected(position) and (position.fret = 0)
           then begin
                   position.fret := 0;
                   selectedPositions[position.str] := position;
                end
   else if OpenStringIsSelected(position) and (position.fret = 0)
           then begin
                   position.fret := -1;
                   selectedPositions[position.str] := position;
                end
   else if PositionIsSelected(position)
           then begin
                   position.fret := 0;
                   selectedPositions[position.str] := position;
                end
   else         selectedPositions[position.str] := position;
end;

procedure TGuitarFretBoard.SetRatio(value : integer);
var
   newFretSpace : integer;
   newHeight : integer;
begin
        if value < 1
           then value := 1
   else if value > 15
           then value := 15;
   if value <> fRatio
      then begin
              fRatio := value;
              if not (csLoading in componentState)
                 then begin
                         fRatioChanged := true;
                         fMetricsChanged := true;
                         newFretSpace := Round(fStringSpace * (0.5 + fRatio * 0.1));
                         newHeight := HeightFromFretSpace(newFretSpace);
                         height := newHeight;
                      end;
           end;
end;

procedure TGuitarFretBoard.SetCursorColor(value : TColor);
begin
   if value <> fCursorColor
      then begin
              fCursorColor := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetClickedColor(value : TColor);
begin
   if value <> fClickedColor
      then begin
              fClickedColor := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetShowNut(value : boolean);
begin
   if value <> fShowNut
      then begin
              fShowNut := value;
              OffsetsChanged;
           end;
end;

procedure TGuitarFretBoard.SetFretLabel(value : integer);
const
   MAX_POSITION = 20;
begin
        if value < -1
           then value := -1
   else if value > MAX_POSITION
           then value := MAX_POSITION;
   if value <> fFretLabel
      then begin
              fFretLabel := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetShowNotes(value : boolean);
begin
   if value <> fShowNotes
      then begin
              fShowNotes := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetShowCharacterTones(value : boolean);
begin
   if value <> fShowCharacterTones
      then begin
              fShowCharacterTones := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetFretLabelPosition(value : integer);
begin
        if value < 0
           then value := 0
   else if value > fFretCount
           then value := fFretCount;
   if value <> fFretLabelPosition
      then begin
              fFretLabelPosition := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetShowFret0(value : boolean);
begin
   value := fShowNut and value; // Nut must be visible for showing up the fret 0
   if value <> fShowFret0
      then begin
              fShowFret0 := value;
              OffsetsChanged;
           end;
end;

procedure TGuitarFretBoard.SetShowCaption(value : boolean);
begin
   if value <> fShowCaption
      then begin
              fShowCaption := value;
              OffsetsChanged;
           end;
end;

procedure TGuitarFretBoard.SetShowFretLabel(value : boolean);
begin
   if value <> fShowFretLabel
      then begin
              fShowFretLabel := value;
              OffsetsChanged;
           end;
end;

procedure TGuitarFretBoard.SetShowFingers(value : boolean);
begin
   if value <> fShowFingers
      then begin
              fShowFingers := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetFixedLineWidth(value : boolean);
begin
   if value <> fFixedLineWidth
      then begin
              fFixedLineWidth := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetColorDegrees(value : boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              Changed;
           end;
end;

procedure TGuitarFretBoard.SetAlignment(value : TAlignment);
begin
   if value <> fAlignment
      then begin
              fAlignment := value;
              Changed;
           end;
end;

{ TCustomGuitarScale }

constructor TCustomGuitarScale.Create(aOwner: TComponent);
begin
   inherited Create(aOwner);
   fScale := TScale.Create;

   fScaleName := 'Major';
   fScaleMode := 1;
   SetLength(fSelectedPositions, 0);

   fShowCaption := true;
   fShowNut := false;
   fShowFret0 := false;
   fShowFretLabel := false;
   fRatio := 10;
   fPattern := 1;
   fShowPatterns := true;

   topPropOffsetPercentage := 7;
   rightPropOffsetPercentage := 0;

   fExtended := false;
   fShiftToTopFret := true;
   fFretCount := 5;

   fMinPatternFret := 0;
   fMaxPatternFret := 0;
   fPlayMinDuration := 150;

   fMode := nmScale;
   fLastPlayedDegree := -1;

   fPlaying := false;
   fKey := ksCMajorAMinor;

   TabStop := true;
   aligment := taCenter;
end;

destructor TCustomGuitarScale.Destroy;
begin
   fScale.Free;
   inherited Destroy;
end;

procedure TCustomGuitarScale.SelectScale(scaleName : string; mode, pattern : integer);
begin
   fScaleName := scaleName;
   fScaleMode := mode;
   fPattern := pattern;
   ContentChanged;
end;

function TCustomGuitarScale.FirstItemSelected : boolean;
begin
   result := (SelectedPositionCount > 0) and (ItemIndex = 0);
end;

function TCustomGuitarScale.LastItemSelected : boolean;
begin
   result := (SelectedPositionCount > 0) and (ItemIndex = SelectedPositionCount);
end;

function TCustomGuitarScale.ScaleIsMinor : boolean;
begin
   result := fScale.isMinor;
end;

procedure TCustomGuitarScale.FirstItem;
begin
   ItemIndex := 0;
end;

procedure TCustomGuitarScale.LastItem;
begin
   ItemIndex := SelectedPositionCount;
end;

procedure TCustomGuitarScale.PrevItem(wrapFretBoard : boolean);
begin
   if FirstItemSelected
      then begin
              if wrapFretBoard
                 then LastItem;
           end
      else ItemIndex := ItemIndex - 1;
end;

procedure TCustomGuitarScale.NextItem(wrapFretBoard : boolean);
begin
   if LastItemSelected
      then begin
              if wrapFretBoard
                 then FirstItem;
           end
      else ItemIndex := ItemIndex + 1;
end;

function TCustomGuitarScale.PositionIsSelected(var position : RFretBoardPosition) : boolean;
var
   index : integer;
begin
   result := false;
   for index := 0 to Length(fSelectedPositions) - 1 do
      if SamePosition(fSelectedPositions[index], position)
         then begin
                 result := true;
                 position.quality := fSelectedPositions[index].quality;
                 position.useSharp := fSelectedPositions[index].useSharp;
                 Break;
              end;
end;

procedure TCustomGuitarScale.SetExtended(value : boolean);
begin
   if value <> fExtended
      then begin
              fContentChanged := true;
              fExtended := value;
              if value
                 then FretCount := 6
                 else FretCount := 5;
           end;
end;

procedure TCustomGuitarScale.SetScaleName(value : string);
begin
   if globalScaleRepository.GetScale(fScale, value)
      then begin
              if not (csReading in componentState)
                 then begin
                         fScaleName := value;
                         fScaleMode := 1;
                      end;
              ContentChanged;
           end;
end;

procedure TCustomGuitarScale.SetScaleMode(value : integer);
begin
   if value <> fScaleMode
      then begin
              if not (csReading in componentState)
                 then begin
                              if value > fScale.count + 1
                                 then value := fScale.count + 1
                         else if value < 1
                                 then value := 1;
                      end;

              if globalScaleRepository.GetScale(fScale, fScaleName, value)
                 then begin
                         fScaleMode := value;
                         fFretLabelContentChanged := true;
                         ContentChanged;
                      end;
           end;
end;

procedure  TCustomGuitarScale.SetPattern(value : integer);
begin
        if value < 1
           then value := 1
   else if value > 5
           then value := 5;

   if value <> fPattern
      then begin
              fPattern := value;
              fFretLabelContentChanged := true;
              fSelectedDegreeIndex := -1;
              ContentChanged;
           end;
end;

procedure TCustomGuitarScale.SetShowPatterns(value : boolean);
begin
   if value <> fShowPatterns
      then begin
              fShowPatterns := value;
              ContentChanged;
           end;
end;

procedure TCustomGuitarScale.SetKey(value : TKey);
begin
   if value <> fKey
      then begin
              fKey := value;
              FretLabelContentChanged;
           end;
end;

function TCustomGuitarScale.GetSelectedDegreeQuality : THalfToneQuality;
begin
   if ItemIndex = -1
      then result := htqUnison
      else result := fSelectedPositions[ItemIndex].quality;
end;

procedure TCustomGuitarScale.SetPlayMinDuration(value : cardinal);
begin
        if value < 50
           then value := 50
   else if value > 1000
           then value := 1000;
   fPlayMinDuration := value;           
end;

procedure TCustomGuitarScale.WndProc(var message : TMessage);
begin
  case message.msg of
    WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_LBUTTONDBLCLK:
      if not (csDesigning in ComponentState) and not focused
         then begin
                 fClicksDisabled := true;
                 Windows.SetFocus(Handle);
                 fClicksDisabled := false;
                 if not focused
                    then Exit;
              end;
    CN_COMMAND:
       begin
          ItemIndex := 0;
          if fClicksDisabled
             then Exit;
       end;
  end;
  inherited WndProc(Message);
end;

procedure TCustomGuitarScale.CreateWnd;
begin
   inherited CreateWnd;
   font.style := [fsBold];
   font.name := 'verdana';
end;

procedure TCustomGuitarScale.CMFocusChanged(var Message: TCMFocusChanged);
begin
   with Message do
      if Sender is TCustomGuitarScale
         then fActive := sender = self
         else fActive := false;
   if fActive
      then begin
              if ItemIndex = -1
                 then ItemIndex := 0;
           end
      else ItemIndex := -1;
  inherited;
end;


procedure TCustomGuitarScale.ComputeContent;
const
   MAX_STRING_EXTENT = 5;
var
   posQuality, degreeQuality : THalfToneQuality;
   degreeIndex : integer;
   fret : integer;
   str : integer;
   octave : integer;
   maxFretBoardExtent : integer;
   minStringFret : integer;
   firstPosFret : integer;
   maxFretBoardFret : integer;
   stringToneJumps : integer;
   firstDegreeIndex : integer;
   lastStringFret : integer;
   toneJump : boolean;
   biggerJump : boolean;
   position : RFretBoardPosition;
   stop : boolean;
   firstRootFound : boolean;

   function NextString : boolean;
   begin
      fret := firstPosFret - 2;
      lastStringFret := MaxInt;
      Inc(str);
      minStringFret := MaxInt;
      stringToneJumps := 0;
      result := str < fStringCount;
   end;

   function NextFret : boolean;
   var
      stringExtent : integer;
      FretBoardExtent : integer;
   begin
      // Next fret
     Inc(fret);

      // compute extents
      if minStringFret = MaxInt
         then begin
                 stringExtent := 0;
                 FretBoardExtent := 0;
                 toneJump := false;
                 biggerJump := false;
              end
         else begin
                 stringExtent := fret - minStringFret;
                 FretBoardExtent := fret - fMinFretBoardFret;
                 toneJump := (fret - lastStringFret) = 2;
                 biggerJump := (fret - lastStringFret) >= 3;
              end;

      // Maybe go to next string
      if (stringExtent >= MAX_STRING_EXTENT)
                        or (FretBoardExtent > maxFretBoardExtent)
                        or (toneJump and (stringToneJumps > 0) and not(fExtended))
                        or biggerJump
         then result := NextString
         else result := true;
   end;

   function ComputeFirstDegreeIndex : integer;
   var
      pattern : integer;
      interval, next_interval : integer;
      intervalSum : integer;
      skip : boolean;
   begin
      result := - fScaleMode + 1;
      intervalSum := 0;

      pattern := fPattern + fScaleMode - 2;

      while pattern > 0 do
         begin
            interval := fScale.degreeInterval[result];
            next_interval := fScale.degreeInterval[result + 1];

            Inc(intervalSum, interval);
            skip := (
                       ((interval = 1) and (intervalSum >= 5)) or
                       ((next_interval = 1) and (intervalSum > 5))
                       and (result > -1)
                    );

            if skip
               then begin
                       intervalSum := 0;
                       if result < 0
                          then Dec(pattern);
                    end
               else Dec(pattern);

            Inc(result);
         end;
   end;

   procedure AddPosition(str, fret : integer; quality : THalfToneQuality; var octave : integer;
                         prepend : boolean = false; reverse : boolean = false);
   var
      index : integer;
      positionIndex : integer;
      note : TNote;
      staffTone : RStaffTone;
      accidental : TNoteAccidental;
   begin
      // Maybe remember first root position
      if (quality = htqUnison) and (not firstRootFound)
         then begin
                 fFirstRootPosition.str := str;
                 fFirstRootPosition.fret := fret;
                 firstRootFound := true;
              end;

      // Add position
      SetLength(fSelectedPositions, Length(fSelectedPositions) + 1);
      if prepend
         then begin
                 for index := Length(fSelectedPositions) - 1 downto 0 do
                    fSelectedPositions[index] := fSelectedPositions[index - 1];
                 positionIndex := 0;
              end
         else positionIndex := Length(fSelectedPositions) - 1;

      if (posQuality = htqUnison) and (positionIndex > 0)
         then begin
                 if reverse
                    then Dec(octave)
                    else Inc(octave);
              end;

      fSelectedPositions[positionIndex].str := str;
      fSelectedPositions[positionIndex].fret := fret;
      fSelectedPositions[positionIndex].quality := quality;
           if quality = htqDiminishedFifth
              then fSelectedPositions[positionIndex].useSharp := not fScale.useFlatFifth
      else if quality = htqAugmentedFifth
              then fSelectedPositions[positionIndex].useSharp := not fScale.useFlatSixth
      else         fSelectedPositions[positionIndex].useSharp := false;


      note := TNote.Create;
      try
            note.SetFromScale(fKey, fScale, degreeIndex, 0, fScale.isMinor);
            staffTone := note.GetStaffTone(fKey);
            accidental := staffTone.accidental;
            if (accidental = naNone) and (staffTone.accidentedInKey)
               then begin
                       if fKey < ksFMajorDMinor
                          then // Sharp key
                               accidental := naSharp
                          else // Flat key
                               accidental := naFlat
                    end;

            fSelectedPositions[positionIndex].tone := staffTone.tone;
            fSelectedPositions[positionIndex].accidental := accidental;
            fSelectedPositions[positionIndex].characterTone := fScale.degreeCharacterTone[degreeIndex];

         finally
            note.Free;
      end;
   end;


   function ComputePattern : boolean;
   var
      index : integer;
   begin
      octave := 1;

      SetLength(fSelectedPositions, 0);
      // Compute pattern
              begin
                 // Inits
                 stop := false;
                 str := 0;
                 fret := -2;
                 stringToneJumps := 0;
                 firstPosFret := MaxInt;
                 lastStringFret := MaxInt;
                 minStringFret := MaxInt;
                 fMinFretBoardFret := MaxInt;
                 maxFretBoardFret := -MaxInt;
                 if fExtended
                    then maxFretBoardExtent := 6
                    else maxFretBoardExtent := 4;
                 firstRootFound := false;

                 firstDegreeIndex := ComputeFirstDegreeIndex;

                 while str < fStringCount do
                    begin
                       for index := 0 to fScale.count do
                          begin
                             // Retrieve the scale degree
                             degreeIndex := firstDegreeIndex + index;
                             degreeQuality := THalfToneQuality(
                                                 (Ord(fScale.degreeQuality[degreeIndex])) mod 12
                                              );

                             // Climb up frets and strings until the scale degree is found
                             repeat
                                if not NextFret
                                   then stop := true;
                                posQuality := PositionQuality(str, fret);
                             until posQuality = degreeQuality;

                             if stop
                                  then Break
                                  else // Save the position
                                       begin
                                          // Remember minimum and maximums
                                          if fret < minStringFret
                                             then minStringFret := fret;
                                          if fret < fMinFretBoardFret
                                             then fMinFretBoardFret := fret;
                                          if fret > maxFretBoardFret
                                             then maxFretBoardFret := fret;
                                          if firstPosFret = MaxInt
                                             then firstPosFret := fret;

                                          // Count tone jumps on the same string
                                          if toneJump and (lastStringFret <> MaxInt)
                                             then Inc(stringToneJumps);

                                          // Add the position to the list
                                          AddPosition(str, fret, degreeQuality, octave);
                                          lastStringFret := fret;
                                       end;
                          end;
                    end;

                 // Move positions that break the "max FretBoard extent" rule
                 if (maxFretBoardFret - fMinFretBoardFret > maxFretBoardExtent)
                    then begin
                            for index := 0 to Length(fSelectedPositions) - 1 do
                               begin
                                  position := fSelectedPositions[index];
                                  if position.fret = maxFretBoardFret
                                     then begin
                                             str := position.str;
                                             fret := position.fret;
                                             degreeQuality := position.quality;
                                             NextString;
                                             while NextFret and (PositionQuality(str, fret) <> degreeQuality) do
                                                NextFret;
                                             fSelectedPositions[index].str := str;
                                             fSelectedPositions[index].fret := fret;
                                          end;
                               end;
                         end;

                 // Compute missing position(s) on first string
                 Assert(fSelectedPositions[0].str = 0);
                 if fSelectedPositions[0].quality = htqUnison
                    then Dec(octave);
                 for index := fSelectedPositions[0].fret - 1 downto fMinFretBoardFret do
                    begin
                       posQuality := PositionQuality(0, index);
                       if fScale.IsQualityInScale(posQuality)
                          then AddPosition(0, index, posQuality, octave, true, false);
                    end;

                 // Shift frets if needed
                 if fShiftToTopFret
                    then begin
                            if (fMinFretBoardFret >= 0) or (fMinFretBoardFret <= -2)
                               then begin
                                       for index := 0 to Length(fSelectedPositions) - 1 do
                                          Dec(fSelectedPositions[index].fret, fMinFretBoardFret + 1)
                                    end;
                         end;
                fMinPatternFret := fMinFretBoardFret;
                fMaxPatternFret := maxFretBoardFret;

                // Apply string and fret corrections
                for index := 0 to Length(fSelectedPositions) - 1 do
                   begin
                      fSelectedPositions[index].str := fSelectedPositions[index].str + 1;
                      fSelectedPositions[index].fret := fSelectedPositions[index].fret + 2;
                   end;
              end
      end;

begin
   // Retrieve the scale
   globalScaleRepository.GetScale(fScale, fScaleName, fScaleMode);

   // Set caption
   if fShowPatterns
      then begin
              if fScale.GetModeName <> ''
                 then caption := fScale.GetModeName + ' #' + IntToStr(fPattern)
                 else caption := fScale.GetScaleName + ' mode ' + IntToStr(fScale.mode) + ' #' + IntToStr(fPattern);
           end
      else begin
              if fScale.GetModeName <> ''
                 then caption := fScale.GetModeName + ' mode'
                 else caption := fScale.GetScaleName + ' mode ' + IntToStr(fScale.mode);
           end;


   // Compute the scale positions
   if fShowPatterns
      then // Compute pattern
           begin
              ComputePattern;
           end
      else // Select the whole FretBoard
           begin
                 for str := 0 to fStringCount - 1 do
                    for fret := -1 to fFretCount - 2 do
                       begin
                          posQuality := PositionQuality(str, fret);
                          if fScale.IsQualityInScale(posQuality)
                             then AddPosition(str + 1, fret + 2, posQuality, octave);
                    end;
           end;
end;

procedure TCustomGuitarScale.ComputeFretLabelContent;
begin
   // Compute fret label
   fKeyRoot.halfTone := GetKeyRoot(fKey, fScale.isMinor);
   GetNoteFret(fKeyRoot.halfTone, fFirstRootPosition.str, fRootFret);

   fFretLabel := fRootFret - (fFirstRootPosition.fret - fMinFretBoardFret);
   fFretLabelPosition := fFirstRootPosition.fret - fMinFretBoardFret;
end;

procedure TCustomGuitarScale.MouseDown(button : TMouseButton; shift: TShiftState; x, y: integer);
begin
   inherited;
   if fMouseOverControl
      then begin
              if SelectDegreeIndex(fMousePosition)
                 then begin
                         Invalidate;
                         if (ItemIndex <> -1) and fHotTrack and not fPlaying
                         and ((ssShift in shift) or (ssLeft in shift))
                            then PlayDegree(ItemIndex);
                      end
                 else Invalidate;
           end;
end;

function TCustomGuitarScale.DoMouseWheelDown(shift : TShiftState; mousePos : TPoint) : boolean;
var
   wrapFretBoard : boolean;
begin
   wrapFretBoard := ssCtrl in shift;
   NextItem(wrapFretBoard);
   if (ItemIndex <> -1) and ((ssShift in shift))
      then begin
              fKeyDown := true;
              Invalidate;
              fLastTickCount := GetTickCount;
              fLastPlayedDegree := ItemIndex;
              PlayDegree(ItemIndex);
           end;
   result := true;
end;

function TCustomGuitarScale.DoMouseWheelUp(shift : TShiftState; mousePos : TPoint): boolean;
var
   wrapFretBoard : boolean;
begin
   wrapFretBoard := ssCtrl in shift;
   PrevItem(wrapFretBoard);
   if (ItemIndex <> -1) and ((ssShift in shift))
      then begin
              fKeyDown := true;
              Invalidate;
              fLastTickCount := GetTickCount;
              fLastPlayedDegree := ItemIndex;
              PlayDegree(ItemIndex);
           end;
   result := true;           
end;

procedure TCustomGuitarScale.PlayDegree(index : integer);
begin
   // To be implemented by descendent
end;

procedure TCustomGuitarScale.DrawPosition(position : RFretBoardPosition; index : integer; style : TFretBoardPositionStyle = [npSelected]);
var
   actualFret : TFret;
begin
   actualFret := position.fret + fFretLabel - 1;
   if actualFret <= -1
      then style := style + [npGrayed];
   inherited DrawPosition(position, index, style);
end;

procedure TCustomGuitarScale.SetScaleAndMode(scaleName : string; scaleMode : integer);
begin
   if globalScaleRepository.GetScale(fScale, fScaleName)
      then begin
              fScaleName := scaleName;

                   if scaleMode > fScale.count + 1
                      then scaleMode := fScale.count + 1
              else if scaleMode < 1
                      then scaleMode := 1;

              fScaleMode := scaleMode;
              fFretLabelContentChanged := true;
              ContentChanged;
           end;
end;

procedure TCustomGuitarScale.KeyDown(var key : word; shift : TShiftState);
var
   wrapFretBoard : boolean;
begin
   inherited;
   if GetTickCount > fPlayMinDuration + fLastTickCount
      then begin
              wrapFretBoard := ssCtrl in shift;
                   if key = VK_ADD
                      then NextItem(wrapFretBoard)
              else if key = VK_SUBTRACT
                      then PrevItem(wrapFretBoard)
              else if (key = VK_SPACE)
                      then begin
                              if fLastPlayedDegree = ItemIndex
                                 then key := 0;
                           end
              else if fLastPlayedDegree <> ItemIndex
                      then key := 0
              else if (key = VK_SHIFT) and (fLastPlayedDegree = ItemIndex)
                      then key := 0;
           end
      else key := 0;

   // Maybe play the scale degree
   if key <> 0
      then begin
              if (ItemIndex <> -1) and ((ssShift in shift) or (key = VK_SPACE))
                 then begin
                         fKeyDown := true;
                         Invalidate;
                         fLastTickCount := GetTickCount;
                         fLastPlayedDegree := ItemIndex;
                         PlayDegree(ItemIndex);
                      end;
           end;
end;

procedure TCustomGuitarScale.KeyUp(var key : word; shift : TShiftState);
begin
   fLastPlayedDegree := -1;
   fLastTickCount := 0;
   fKeyDown := false;
   Changed;
end;

function TCustomGuitarScale.SelectDegreeIndex(position : RFretBoardPosition) : boolean;
var
   degreeIndex : integer;
   index : integer;   
begin
   degreeIndex := -1;
   for index := 0 to Length(fSelectedPositions) - 1 do
      if SamePosition(fSelectedPositions[index], position)
         then begin
                 degreeIndex := index;
                 Break;
              end;
   result := degreeIndex <> -1;
   if result
      then ItemIndex := degreeIndex;
end;

procedure SetFretLabel(value : integer);
begin
end;

{ TGuitarScaleFretBoard }

constructor TGuitarScaleFretBoard.Create(aOwner: TComponent);
begin
   inherited Create(aOwner);
   fFretCount := 22;
   fShiftToTopFret := false;

   rightPropOffset := 0;
   fShowFretLabel := true;
end;

procedure TGuitarScaleFretBoard.DrawAvailablePositions;
var
   index : integer;

   procedure DrawFretPositions(fret : integer);
   var
      str : integer;
      quality : THalfToneQuality;
      position : RFretBoardPosition;
   begin
      for str := 0 to fStringCount - 1 do
         begin
            quality := PositionQuality(str, fret);
            if fScale.IsQualityInScale(quality)
               then begin
                       position.str := str + 1;
                       position.fret := fret + 2;
                       position.quality := quality;
                            if quality = htqDiminishedFifth
                               then position.useSharp := not fScale.useFlatFifth
                       else if quality = htqAugmentedFifth
                               then position.useSharp := not fScale.useFlatSixth
                       else         position.useSharp := false;
                       DrawDot(position, -1, [npGrayed]);
                    end;
         end;
   end;

begin
   if fShowPatterns
      then begin
              for index := -1 to fFretCount - 2 do
                 DrawFretPositions(index);
           end;
end;

procedure TGuitarScaleFretBoard.SetExtended(value : boolean);
begin
   if value <> fExtended
      then begin
              fExtended := value;
              ContentChanged;
           end;
end;

{ TGuitarScalePattern }

constructor TGuitarScalePattern.Create(aOwner : TComponent);
begin
   inherited Create(aOwner);
   fShowFretLabel := true;
   fFretLabel := 5;
   rightPropOffsetPercentage := 40;
end;

procedure TGuitarScalePattern.PlayDegree(index : integer);
var
   position : RFretBoardPosition;
   actualFret : integer;   
   noteVal : integer;
begin
   if csDesigning in ComponentState
      then Exit;
   if (index >= Low(fSelectedPositions)) and (index <= High(fSelectedPositions))
      then begin
              position := fSelectedPositions[index];
              actualFret := position.fret + fFretLabel - 1;
              if actualFret >= 0
                 then begin
                         noteVal := 36 + Ord(STANDARD_GUITAR_TUNING[position.str - 1].halfTone)
                                       + STANDARD_GUITAR_TUNING[position.str - 1].octave * 12
                                       + actualFret;
                         globalMidiGen.PlayNote(noteVal);
                      end;   
           end;
end;

procedure TGuitarChord.Clear;
begin
   inherited;
   fMinFret := -1;
   fMaxFret := -1;
   fFirstFingerBarreExtent := -1;
   fFirstFingerBarreStartingStr := -1;
end;

initialization
begin
   Assert(CAPTION_PERCENTAGE + CAPTION_SPACER_PERCENTAGE + FRET0_PERCENTAGE + FRET0_SPACER_PERCENTAGE + NUT_PERCENTAGE = 100);
end;

end.
