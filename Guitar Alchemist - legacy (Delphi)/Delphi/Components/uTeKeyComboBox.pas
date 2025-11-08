unit uTeKeyComboBox;

interface
uses
   StdCtrls
   ,Classes
   ,Types
   ,Graphics
   ,Controls
   ,te_controls
   ,uMusicClasses
   ,uMusicFontRoutines
   ;


const
   DEFAULT_ITEM_HEIGHT = 64;
   MIN_ITEM_HEIGHT = 28;
   HORIZONTAL_KEY_OFFSET = 6;

type
   TKeyMode = (kmMajor, kmMinor, kmBoth);


   TTeKeyBox = class(TTeComboBox)
      private
         fNeedToPopulate : boolean;
         fSelectedKey : TKey;
         fMode : TKeyMode;
         procedure PopulateList;
         function GetSelectedKey : TKey;
         procedure SetSelectedKey(const value : TKey);
         procedure SetMode(value : TKeyMode);
      protected
         procedure CreateWnd; override;
         procedure DrawItem(Control: TWinControl; Canvas: TCanvas; Index: Integer; Rect: TRect; State: TOwnerDrawState);
      public
         constructor Create(aOwner : TComponent); override;
      published
         property SelectedKey : TKey read GetSelectedKey write SetSelectedKey default ksCMajorAMinor;
         property Mode : TKeyMode read fMode write SetMode;
   end;


implementation

uses
   Windows
   ,SysUtils
   ,te_utils
   ,te_theme
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
   r : TRect;
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
                    r.Left := rect.Left;
                    r.Top := rect.top;
                    r.Right := rect.Left + 20;
                    r.Bottom := 1000;
                    canvas.TextOut(x, alterationY - tm.tmAscent, ch);
                    Inc(x, xIncrement);
                 end;
           end;
end;


{ TTeKeyBox }

constructor TTeKeyBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   ControlStyle := ControlStyle + [csOpaque];
   AutoSize := false;
   OnDrawItem := DrawItem;
   DropDownCount := 6;
   ReadOnly := true;   

   fSelectedKey := ksCMajorAMinor;
   fMode := kmMajor;
   fNeedToPopulate := true;
end;

procedure TTeKeyBox.CreateWnd;
begin
   inherited CreateWnd;
   ComboStyle := kcsDropDownList;
   ListStyle := lbOwnerDrawFixed;
   itemHeight := DEFAULT_ITEM_HEIGHT;
   SetBounds(left, top, width, DEFAULT_ITEM_HEIGHT + BorderWidth * 2);
   font.size := -height;

   if fNeedToPopulate
      then PopulateList;
end;

procedure TTeKeyBox.PopulateList;
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


function TTeKeyBox.GetSelectedKey : TKey;
begin
   if handleAllocated
      then begin
              if ItemIndex <> -1
                 then result := TKey(itemIndex)
                 else result := TKey(0);
           end
      else result := fSelectedKey;
end;

procedure TTeKeyBox.SetSelectedKey(const value : TKey);
begin
   if handleAllocated
      then itemIndex := Ord(value);
   fSelectedKey := value;
end;

procedure TTeKeyBox.SetMode(value : TKeyMode);
begin
   if value <> fMode
      then begin
              fMode := value;
              fSelectedKey := GetSelectedKey;
              PopulateList;
           end;
end;

procedure TTeKeyBox.DrawItem(Control: TWinControl; Canvas: TCanvas; Index: Integer; Rect: TRect; State: TOwnerDrawState);
var
   LRect: TRect;
   DrawState: TTeListItemDrawState;
begin
   // Theme engine
   canvas.font := font;
   if IsObjectDefined(klscListBox, ThemeObject)
      then begin
                   if not Enabled
                      then DrawState := klidsDisabled
              else if odSelected in state
                      then DrawState := klidsSelected
              else if (odFocused in state) and (ItemIndex = Index)
                      then DrawState := klidsFocused
              else         DrawState := klidsNormal;

              case DrawState of
                 klidsNormal: Canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextNormal]);
                 klidsHot: Canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextHot]);
                 klidsSelected: Canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextSelected]);
                 klidsFocused: Canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextFocused]);
                 klidsDisabled: Canvas.Font.Assign(CurrentTheme.Fonts[ktfListItemTextDisabled]);
              end;
              CurrentTheme.ListDrawItem(klscListBox, Canvas, ListItemInfo(rect, DrawState), ThemeObject);
           end
      else begin
              if odSelected in state
                 then begin
                         FillRect(canvas, rect, clHighlight);
                         if odFocused in state
                            then DrawFocusRect(Canvas, rect, clBlack);
                         canvas.font.color := clHighlightText;
                      end
                 else if (odFocused in state) and (ItemIndex = Index)
                         then DrawFocusRect(Canvas, rect, clBlack);
           end;

   // Draw the key and staff
   canvas.pen.width := 1;
   canvas.pen.style := psSolid;
   canvas.pen.mode := pmCopy;
   canvas.pen.color := canvas.font.color;

   canvas.brush.Style := bsSolid;
   canvas.brush.color := color;
   LRect := rect;
   LRect.right := Trunc((lRect.bottom - lRect.top) * 1.6);
   InflateRect(LRect, -1, -1);
   canvas.Brush.Style := bsClear;

   canvas.font.style := [];
   DrawKeySignatureRect(canvas, lRect, TKey(index));

   canvas.font.name := CHORDS_FONT_NAME;
   canvas.font.height := (rect.bottom - rect.top) div 3;
   canvas.font.style := [fsBold];

   canvas.Brush.Style := bsClear;
   rect.left := LRect.Right + HORIZONTAL_KEY_OFFSET * 2;

   canvas.TextRect(rect, rect.Left,
     rect.Top + (rect.Bottom - rect.Top - canvas.TextHeight(Items[Index])) div 2,
     Items[Index]);
end;

end.
