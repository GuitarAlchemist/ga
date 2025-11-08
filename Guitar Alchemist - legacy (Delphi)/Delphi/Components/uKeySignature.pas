unit uKeySignature;

interface
uses
   StdCtrls
   ,Classes
   ,Types
   ,uMusicClasses
   ,uMusicFontRoutines
   ;


const
   DEFAULT_ITEM_HEIGHT = 64;
   MIN_ITEM_HEIGHT = 28;
   HORIZONTAL_KEY_OFFSET = 6;

type
   TKeyMode = (kmMajor, kmMinor, kmBoth);

   TCustomKeyBox = class(TCustomComboBox)
      private
         fNeedToPopulate : boolean;
         fSelectedKey : TKey;
         fMode : TKeyMode;
         procedure PopulateList;
         function GetSelected : TKey;
         procedure SetSelected(const value : TKey);
         procedure SetMode(value : TKeyMode);
      protected
         procedure CreateWnd; override;
         procedure Select; override;
         procedure DrawItem(index : integer; rect : TRect; state : TOwnerDrawState); override;
         procedure SetItemHeight(value : integer); override;
      public
         constructor Create(aOwner : TComponent); override;
         property Selected : TKey read GetSelected write SetSelected default ksCMajorAMinor;
         property ItemHeight default DEFAULT_ITEM_HEIGHT;
         property Mode : TKeyMode read fMode write SetMode;
   end;

   TKeyBox = class(TCustomKeyBox)
      published
         property AutoComplete;
         property AutoDropDown;
         property Selected;
         property Anchors;
         property BevelEdges;
         property BevelInner;
         property BevelKind;
         property BevelOuter;
         property BiDiMode;
         property Color;
         property Constraints;
         property Ctl3D;
         property DropDownCount;
         property Enabled;
         property ItemHeight;
         property ParentBiDiMode;
         property ParentColor;
         property ParentCtl3D;
         property ParentFont;
         property ParentShowHint;
         property PopupMenu;
         property ShowHint;
         property TabOrder;
         property TabStop;
         property Visible;
         property Mode;
         property OnChange;
         property OnCloseUp;
         property OnClick;
         property OnContextPopup;
         property OnDblClick;
         property OnDragDrop;
         property OnDragOver;
         property OnDropDown;
         property OnEndDock;
         property OnEndDrag;
         property OnEnter;
         property OnExit;
         property OnKeyDown;
         property OnKeyPress;
         property OnKeyUp;
         property OnSelect;
         property OnStartDock;
         property OnStartDrag;
   end;

implementation

uses
   Graphics
   ,Windows, Controls
   ;

procedure DrawKeySignatureRect(canvas : TCanvas; rect : TRect; key : TKey);
const
   SHARP_POSITIONS : array[0..6] of integer =
      (8, 5, 9, 6, 3, 7, 4);
   FLAT_POSITIONS : array[0..6] of integer =
      (4, 7, 3, 6, 2, 5, 1);
   KEY_SIGNATURE_ALTERATIONS : array[TKey] of integer =
      (0, 1, 2, 3, 4, 5, 6, 7, -1, -2, -3, -4, -5, -6, -7);
var
   offset : double;
   x, y : integer;
   tm : TTextMetric;
   index : integer;
   height, spaceHeight : integer;

   alterationCount : integer;
   alterationPosition : integer;
   alterationY : integer;
   xIncrement : integer;
   sharp : boolean;
   ch : char;
begin
   offset := (rect.bottom - rect.top) * 0.05;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.height := rect.bottom - rect.top - Round(offset * 2);
   GetTextMetrics(canvas.handle, tm);
   x := canvas.TextWidth(G_KEY_CHARACTER) + Round(6 * offset);

   // Draw staff
   height := Round(canvas.font.height * 0.51);
   spaceHeight := height div 4;
   canvas.pen.width := spaceHeight div 5;
   y := rect.top + Round(offset) + tm.tmAscent - 3 * spaceHeight - 1;
   for index := 0 to 4 do
      begin
         canvas.MoveTo(rect.left, y);
         canvas.LineTo(rect.right, y);
         Inc(y, spaceHeight);
      end;

   // Draw G key
   canvas.TextOut(Round(3 * offset), rect.top + Round(offset), G_KEY_CHARACTER);

   // Draw key alterations
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
              canvas.font.name := STAFF_FONT_NAME;
              canvas.font.height := spaceHeight * 8;
              GetTextMetrics(canvas.handle, tm);
              xIncrement := canvas.TextWidth(SHARP_CHARACTER);

              // Draw the alterations
              for index := 0 to alterationCount - 1 do
                 begin
                    if sharp
                       then alterationPosition := SHARP_POSITIONS[index]
                       else alterationPosition := FLAT_POSITIONS[index];
                    alterationY := y - (alterationPosition + 2) * spaceHeight div 2;
                    canvas.TextOut(x, alterationY - tm.tmAscent, ch);
                    Inc(x, xIncrement);
                 end;
           end;
end;


{ TCustomKeyBox }

constructor TCustomKeyBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   doubleBuffered := true;
   style := csOwnerDrawFixed;
   itemHeight := DEFAULT_ITEM_HEIGHT;
   fSelectedKey := ksCMajorAMinor;
   fMode := kmMajor;
   fNeedToPopulate := true;
end;

procedure TCustomKeyBox.CreateWnd;
begin
   inherited CreateWnd;
   if fNeedToPopulate
      then PopulateList;
end;

procedure TCustomKeyBox.Select;
begin
   Selected := TKey(ItemIndex);
   inherited Select;
end;

procedure TCustomKeyBox.SetItemHeight(value : integer);
begin
   if value >= MIN_ITEM_HEIGHT
      then inherited SetItemHeight(value);
end;

procedure TCustomKeyBox.DrawItem(index : integer; rect : TRect; state : TOwnerDrawState);
var
  LRect: TRect;
  LBackground: TColor;
begin
    canvas.FillRect(Rect);
    LBackground := canvas.Brush.Color;

    LRect := Rect;
    LRect.right := Trunc((lRect.bottom - lRect.top) * 1.8);
    InflateRect(LRect, -1, -1);

    canvas.brush.style := bsClear;
    canvas.pen.width := 1;
    canvas.pen.color := clBlack;
    canvas.pen.style := psSolid;
    canvas.pen.mode := pmCopy;    
    if odSelected in state
       then begin
               canvas.pen.mode := pmMergeNotPen;
               canvas.font.color := clWhite;
            end
       else begin
               canvas.pen.mode := pmBlack;
               canvas.font.color := clBlack;
            end;
    DrawKeySignatureRect(canvas, lRect, TKey(index));

    canvas.font.name := CHORDS_FONT_NAME;
    canvas.font.height := (rect.bottom - rect.top) div 3;
    canvas.font.style := [fsBold];
    canvas.brush.color := LBackground;
    rect.left := LRect.Right + HORIZONTAL_KEY_OFFSET * 2;

    if odSelected in state
       then canvas.font.color := clWhite;
    canvas.TextRect(Rect, Rect.Left,
      Rect.Top + (Rect.Bottom - Rect.Top - canvas.TextHeight(Items[Index])) div 2,
      Items[Index]);
end;

procedure TCustomKeyBox.PopulateList;
var
   index : integer;
begin
   if HandleAllocated
      then begin
              items.BeginUpdate;
              try

                    items.Clear;
                    case fMode of
                       kmBoth:
                          for index := 0 to Ord(High(MAJOR_KEY_NAME)) do
                             items.Add(MAJOR_KEY_NAME[TKey(index)] + ' / ' + MINOR_KEY_NAME[TKey(index)] + MINOR_CHARACTER);
                       kmMinor:
                          for index := 0 to Ord(High(MAJOR_KEY_NAME)) do
                             items.Add(MINOR_KEY_NAME[TKey(index)] + MINOR_CHARACTER);
                       kmMajor:
                          for index := 0 to Ord(High(MAJOR_KEY_NAME)) do
                             items.Add(MAJOR_KEY_NAME[TKey(index)]);
                       else Assert(false, 'Case not handled');
                    end;
                    itemIndex := Ord(fSelectedKey);
                 finally
                    items.EndUpdate;
              end;
              fNeedToPopulate := false;
           end
      else fNeedToPopulate := true;
end;


function TCustomKeyBox.GetSelected : TKey;
begin
   if handleAllocated
      then begin
              if ItemIndex <> -1
                 then result := TKey(itemIndex)
                 else result := TKey(0);
           end
      else result := fSelectedKey;
end;

procedure TCustomKeyBox.SetSelected(const value : TKey);
begin
  if value <> fSelectedKey
     then begin
             if handleAllocated
                then itemIndex := Ord(value);
             fSelectedKey := value;
             Change;
          end;
end;

procedure TCustomKeyBox.SetMode(value : TKeyMode);
begin
   if value <> fMode
      then begin
              fMode := value;
              fSelectedKey := GetSelected;
              PopulateList;
           end;
end;


end.
