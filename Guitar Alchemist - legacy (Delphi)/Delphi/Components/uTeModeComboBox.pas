unit uTeModeComboBox;

interface
uses
   StdCtrls
   ,Classes
   ,Controls
   ,Graphics
   ,Types
   ,Messages
   ,uMusicClasses
   ,te_controls
   ;


type
   TModeDegreesPosition = (mdpRight, mdpBottom, mdpNone);

   TTeCustomModeComboBox = class(TTeComboBox)
      private
         fComputeContent : boolean;
         fComputeMetrics : boolean;
         fFontSize : integer;
         fOffset : integer;
         fMinOffset : integer;
         fScale : TScale;
         fScaleName : string;
         fDefaultDegreesColor : TColor;
         fColorDegrees : boolean;
         fShowCharacterTones : boolean;
         fShowModeNumber : boolean;
         fDegreesPosition : TModeDegreesPosition;
         procedure PopulateList;
         procedure ComputeMetrics; virtual;
         procedure SetColorDegrees(value : boolean);
         procedure SetDefaultDegreesColor(value : TColor);
         procedure SetScaleName(value : string);
         procedure SetMinOffset(value : integer);
         procedure SetCharacterTones(value : boolean);
         procedure SetShowModeNumber(value : boolean);
         procedure SetDegreesPosition(value : TModeDegreesPosition);
         procedure SetFontSize(value : integer);
         procedure AdjustHeight;
      protected
         procedure CreateWnd; override;
         procedure Resize; override;
         procedure DrawItem(listBox : TTeListBox; canvas : TCanvas; index : integer; rect : TRect; state : TOwnerDrawState);

         property ColorDegrees : boolean read fColorDegrees write SetColorDegrees;
         property DefaultDegreesColor : TColor read fDefaultDegreesColor write SetDefaultDegreesColor;
         property DegreesPosition : TModeDegreesPosition read fDegreesPosition write SetDegreesPosition;
         property ScaleName : string read fScaleName write SetScaleName;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowCharacterTones : boolean read fShowCharacterTones write SetCharacterTones;
         property ShowModeNumber : boolean read fShowModeNumber write SetShowModeNumber;
         property FontSize : integer read fFontSize write SetFontSize;
      public
         constructor Create(aOwner : TComponent); override;
         function IsModeMinor : boolean;
   end;

   TTeModeComboBox = class(TTeCustomModeComboBox)
      published
         property ColorDegrees;      
         property DegreesPosition;
         property DefaultDegreesColor;
         property MinOffset;
         property ScaleName;
         property ShowCharacterTones;
         property ShowModeNumber;
         property FontSize;         
   end;

   TTeCustomModeListBox = class(TTeListBox)
      private
         fComputeContent : boolean;
         fComputeMetrics : boolean;
         fOffset : integer;
         fMinOffset : integer;
         fScale : TScale;
         fScaleName : string;
         fDefaultDegreesColor : TColor;
         fColorDegrees : boolean;
         fShowCharacterTones : boolean;
         fShowModeNumber : boolean;
         fDegreesPosition : TModeDegreesPosition;
         procedure PopulateList;
         procedure ComputeMetrics; virtual;
         procedure CMFontChanged(var Message: TMessage); message CM_FONTCHANGED;
         procedure SetColorDegrees(value : boolean);
         procedure SetDefaultDegreesColor(value : TColor);
         procedure SetScaleName(value : string);
         procedure SetMinOffset(value : integer);
         procedure SetCharacterTones(value : boolean);
         procedure SetShowModeNumber(value : boolean);
         procedure SetDegreesPosition(value : TModeDegreesPosition);
      protected
         procedure CreateWnd; override;
         procedure DrawItem(Canvas: TCanvas; Index: integer; ARect: TRect); override;

         property ColorDegrees : boolean read fColorDegrees write SetColorDegrees;
         property DefaultDegreesColor : TColor read fDefaultDegreesColor write SetDefaultDegreesColor;
         property DegreesPosition : TModeDegreesPosition read fDegreesPosition write SetDegreesPosition;
         property ScaleName : string read fScaleName write SetScaleName;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowCharacterTones : boolean read fShowCharacterTones write SetCharacterTones;
         property ShowModeNumber : boolean read fShowModeNumber write SetShowModeNumber;
      public
         constructor Create(aOwner : TComponent); override;
         function IsModeMinor : boolean;
   end;

   TTeModeListBox = class(TTeCustomModeListBox)
      published
         property Anchors;
         property BevelEdges;
         property BevelInner;
         property BevelKind;
         property BevelOuter;
         property BiDiMode;
         property Color;
         property ColorDegrees;
         property Constraints;
         property Ctl3D;
         property Enabled;
         property Font;
         property ItemIndex;
         property DegreesPosition;
         property DefaultDegreesColor;
         property MinOffset;
         property ParentBiDiMode;
         property ParentColor;
         property ParentCtl3D;
         property ParentFont;
         property ParentShowHint;
         property PopupMenu;
         property ScaleName;
         property ShowHint;
         property ShowModeNumber;
         property TabOrder;
         property TabStop;
         property Visible;
         property OnClick;
         property OnContextPopup;
         property OnDblClick;
         property OnDragDrop;
         property OnDragOver;
         property OnEndDock;
         property OnEndDrag;
         property OnEnter;
         property OnExit;
         property OnKeyDown;
         property OnKeyPress;
         property OnKeyUp;
         property OnStartDock;
         property OnStartDrag;
   end;

implementation

uses
   Windows
   ,SysUtils
   ,uMusicFontRoutines
   ,te_utils
   ,te_theme
   ;


constructor TTeCustomModeListBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   ListStyle := lbOwnerDrawFixed;

   fScale := TScale.Create;
   fScaleName := 'Major';

   fComputeContent := true;
   fComputeMetrics := true;
   fDefaultDegreesColor := clNavy;
   fShowCharacterTones := true;
   fShowModeNumber := false;
end;

procedure TTeCustomModeListBox.CreateWnd;
begin
   inherited CreateWnd;
   if fComputeContent
      then PopulateList;
   if fComputeMetrics
      then ComputeMetrics;
end;

procedure TTeCustomModeListBox.CMFontChanged(var Message: TMessage);
begin
   canvas.font := font;
   if fDegreesPosition in [mdpRight, mdpNone]
      then ItemHeight := canvas.TextHeight('Z')
      else ItemHeight := canvas.TextHeight('Z') * 2;
   ComputeMetrics;
end;

procedure TTeCustomModeListBox.DrawItem(Canvas: TCanvas; Index: integer; ARect: TRect);
var
   LBackground: TColor;
   scale : TScale;
   intervalIndex : integer;
   tm : TTextMetric;
   quality : THalfToneQuality;
   ch : char;
   incX : integer;
   x, y : integer;

   DrawState: TTeListItemDrawState;
begin

    canvas.font.name := font.name;
    canvas.font.height := font.height;

    canvas.TextOut(ARect.left, ARect.top, Items[Index]);

    if fDegreesPosition <> mdpNone
       then begin
               canvas.font.name := STAFF_FONT_NAME;
               canvas.font.size := Round(font.size * 1.6);
               canvas.brush.style := bsClear;
               GetTextMetrics(canvas.handle, tm);
               if fDegreesPosition = mdpRight
                  then begin
                          x := ARect.Left + fOffset;
                          y := ARect.top + (ItemHeight - tm.tmHeight) div 2 - 2
                       end
                  else begin
                          x := ARect.Left + 15;
                          y := ARect.top;
                       end;
               incX := Round(canvas.TextWidth(DEGREE_QUALITY_Sharp11) * 1.1);

               scale := TScale.Create;
               try

                     globalScaleRepository.GetScale(scale, fScaleName, index + 1);
                     for intervalIndex := 0 to scale.count do
                        begin
                           // Get quality char and color
                           quality := scale.degreeQuality[intervalIndex];
                           if not selected[index]
                              then begin
                                      if fColorDegrees
                                         then canvas.font.color := GetQualityColor(quality)
                                         else canvas.font.color := fDefaultDegreesColor;
                                   end;
                           ch := GetDegreeQualityChar(quality, scale.useFlatFifth, scale.useFlatSixth);

                           // Character tones
                           if fShowCharacterTones and (scale.degreeCharacterTone[intervalIndex] <> ctNone)
                              then begin
                                      if scale.degreeCharacterTone[intervalIndex] = ctPrimary
                                         then canvas.font.style := [fsBold, fsUnderline]
                                         else canvas.font.style := [fsBold, fsItalic]
                                   end
                              else canvas.font.style := [fsBold];

                           // Draw degree quality
                           canvas.TextOut(x - canvas.TextWidth(ch) div 2, y, ch);
                           Inc(x, incX);
                        end;
                  finally
                     scale.Free;
               end;
            end;   
end;


procedure TTeCustomModeListBox.PopulateList;
begin
   if HandleAllocated
      then begin
              globalScaleRepository.GetModes(fScaleName, items, fShowModeNumber);
              ItemIndex := 0;
              ComputeMetrics;
           end
      else fComputeContent := true;
end;

procedure TTeCustomModeListBox.ComputeMetrics;
var
   index : integer;
   textWidth : integer;
begin
   canvas.font := font;
   fOffset := 0;
   for index := 0 to items.count - 1 do
      begin
         textWidth := canvas.TextWidth(items[index]);
         if textWidth > fOffset
            then fOffset := textWidth;
      end;
   Inc(fOffset, 10);
   if fMinOffset > fOffset
      then fOffset := fMinOffset;

   fComputeMetrics := false;      
end;

procedure TTeCustomModeListBox.SetScaleName(value : string);
begin
   if (value <> fScaleName) and globalScaleRepository.GetScale(fScale, value)
      then begin
              fScaleName := value;
              PopulateList;
           end;
end;

procedure TTeCustomModeListBox.SetMinOffset(value : integer);
begin
   if value <> fMinOffset
      then begin
              fMinOffset := value;
              ComputeMetrics;
              if value > fOffset
                 then fOffset := value;
              Invalidate;
           end;
end;

procedure TTeCustomModeListBox.SetDegreesPosition(value : TModeDegreesPosition);
begin
   if value <> fDegreesPosition
      then begin
              fDegreesPosition := value;
              fComputeMetrics := true;
              Perform(CM_FONTCHANGED, 0, 0);
              RecreateWnd;
           end;
end;


procedure TTeCustomModeListBox.SetCharacterTones(value: boolean);
begin
   if value <> fShowCharacterTones
      then begin
              fShowCharacterTones := value;
              Invalidate;
           end;
end;

procedure TTeCustomModeListBox.SetColorDegrees(value: boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              Invalidate;
           end;
end;

procedure TTeCustomModeListBox.SetDefaultDegreesColor(value: TColor);
begin
   if value <> fDefaultDegreesColor
      then begin
              fDefaultDegreesColor := value;
              if not fColorDegrees
                 then Invalidate;
           end;
end;

procedure TTeCustomModeListBox.SetShowModeNumber(value: boolean);
begin
   if value <> fShowModeNumber
      then begin
              fShowModeNumber := value;
              PopulateList;
           end;
end;

function TTeCustomModeListBox.IsModeMinor : boolean;
var
   scale : TScale;
begin
   scale := TScale.Create;
   try

         if globalScaleRepository.GetScale(scale, fScaleName, ItemIndex + 1)
            then result := scale.isMinor
            else result := false;

      finally
         scale.Free;
   end;
end;

{ TTeCustomModeComboBox }

procedure TTeCustomModeComboBox.ComputeMetrics;
var
   index : integer;
   textWidth : integer;
begin
   canvas.font := font;
   if fFontSize <> -1
      then canvas.font.size := fFontSize;
   fOffset := 0;
   for index := 0 to items.count - 1 do
      begin
         textWidth := canvas.TextWidth(items[index]);
         if textWidth > fOffset
            then fOffset := textWidth;
      end;
   Inc(fOffset, canvas.TextWidth('Z') * 3);
   if fMinOffset > fOffset
      then fOffset := fMinOffset;
end;

constructor TTeCustomModeComboBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   ControlStyle := ControlStyle + [csOpaque];
   AutoSize := false;
   ReadOnly := true;
   DropDownCount := 7;
   OnDrawItem := DrawItem;

   fScale := TScale.Create;
   fScaleName := 'Major';
   fFontSize := -1;
   fComputeContent := true;
   fComputeMetrics := true;
   fDefaultDegreesColor := clNavy;
   fShowCharacterTones := true;
   fShowModeNumber := false;
end;

procedure TTeCustomModeComboBox.CreateWnd;
begin
   inherited CreateWnd;

   ComboStyle := kcsDropDownList;
   ListStyle := lbOwnerDrawFixed;
   AdjustHeight;

   if fComputeContent
      then PopulateList;
   if fComputeMetrics
      then ComputeMetrics;
end;

procedure TTeCustomModeComboBox.Resize;
begin
   inherited;
   font.height := -height div 3;
   Invalidate;
end;

procedure TTeCustomModeComboBox.DrawItem(listBox : TTeListBox; canvas : TCanvas; index : integer; rect : TRect; state : TOwnerDrawState);
var
   LBackground: TColor;
   scale : TScale;
   intervalIndex : integer;
   tm : TTextMetric;
   quality : THalfToneQuality;
   s : string;
   ch : char;
   incX : integer;
   x, y : integer;

   textFontHeight : integer;
   DrawState: TTeListItemDrawState;
   skinFont : TFont;
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

   // Draw the mode name
   s := Items[Index];
   canvas.TextRect(rect, rect.left, rect.top, s);
   textFontHeight := canvas.TextHeight('Z');

   // Draw the mode degrees
    if fDegreesPosition <> mdpNone
       then begin
               canvas.font.name := STAFF_FONT_NAME;
               canvas.font.height := -Round(textFontHeight * 1.4);
               canvas.brush.style := bsClear;
               if fDegreesPosition = mdpRight
                  then begin
                          x := rect.Left + fOffset;
                          y := rect.top + (ItemHeight - tm.tmHeight) div 2 - 2
                       end
                  else begin
                         GetTextMetrics(canvas.handle, tm);
                          x := rect.Left + 15;
                          y := rect.top + textFontHeight - tm.tmDescent;
                       end;
               incX := Round(canvas.TextWidth(DEGREE_QUALITY_bb7) * 1.1);

               scale := TScale.Create;
               try

                     globalScaleRepository.GetScale(scale, fScaleName, index + 1);
                     for intervalIndex := 0 to scale.count do
                        begin
                           // Get quality char and color
                           quality := scale.degreeQuality[intervalIndex];
                           if not (odSelected in state)
                              then begin
                                      if fColorDegrees
                                         then canvas.font.color := GetQualityColor(quality)
                                         else canvas.font.color := fDefaultDegreesColor;
                                   end;
                           ch := GetDegreeQualityChar(quality, scale.useFlatFifth, scale.useFlatSixth);

                           // Character tones
                           if fShowCharacterTones and (scale.degreeCharacterTone[intervalIndex] <> ctNone)
                              then begin
                                      if scale.degreeCharacterTone[intervalIndex] = ctPrimary
                                         then canvas.font.style := [fsBold, fsUnderline]
                                         else canvas.font.style := [fsBold, fsItalic]
                                   end
                              else canvas.font.style := [fsBold];

                           // Draw degree quality
                           canvas.TextOut(x - canvas.TextWidth(ch) div 2, y, ch);
                           Inc(x, incX);
                        end;
                  finally
                     scale.Free;
               end;
            end;   
end;

function TTeCustomModeComboBox.IsModeMinor: boolean;
var
   scale : TScale;
begin
   scale := TScale.Create;
   try

         if globalScaleRepository.GetScale(scale, fScaleName, ItemIndex + 1)
            then result := scale.isMinor
            else result := false;

      finally
         scale.Free;
   end;
end;

procedure TTeCustomModeComboBox.PopulateList;
begin
   if HandleAllocated
      then begin
              globalScaleRepository.GetModes(fScaleName, items, fShowModeNumber);
              ItemIndex := 0;
              ComputeMetrics;
           end
      else fComputeContent := true;
end;

procedure TTeCustomModeComboBox.SetCharacterTones(value: boolean);
begin
   if value <> fShowCharacterTones
      then begin
              fShowCharacterTones := value;
              Invalidate;
           end;
end;

procedure TTeCustomModeComboBox.SetColorDegrees(value: boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              Invalidate;
           end;
end;

procedure TTeCustomModeComboBox.SetDefaultDegreesColor(value: TColor);
begin
   if value <> fDefaultDegreesColor
      then begin
              fDefaultDegreesColor := value;
              if not fColorDegrees
                 then Invalidate;
           end;
end;

procedure TTeCustomModeComboBox.SetDegreesPosition(value: TModeDegreesPosition);
begin
   if value <> fDegreesPosition
      then begin
              fDegreesPosition := value;
              fComputeMetrics := true;
              Perform(CM_FONTCHANGED, 0, 0);
              RecreateWnd;
           end;
end;

procedure TTeCustomModeComboBox.SetFontSize(value: integer);
begin
   if value <> fFontSize
      then begin
              fFontSize := value;
              AdjustHeight;
           end;
end;

procedure TTeCustomModeComboBox.AdjustHeight;
var
   fontHeight : integer;
begin
   if fFontSize <> -1
      then canvas.font.size := fFontSize;
   fontHeight := canvas.TextHeight('Z');
   if fDegreesPosition in [mdpRight, mdpNone]
      then ItemHeight := fontHeight
      else ItemHeight := fontHeight * 2;
   height := ItemHeight + (BorderWidth + BevelWidth + 1) * 2;
   font.height := -height;
   ComputeMetrics;
end;

procedure TTeCustomModeComboBox.SetMinOffset(value: integer);
begin
   if value <> fMinOffset
      then begin
              fMinOffset := value;
              ComputeMetrics;
              if value > fOffset
                 then fOffset := value;
              Invalidate;
           end;
end;

procedure TTeCustomModeComboBox.SetScaleName(value: string);
begin
   if (value <> fScaleName) and globalScaleRepository.GetScale(fScale, value)
      then begin
              fScaleName := value;
              PopulateList;
           end;
end;

procedure TTeCustomModeComboBox.SetShowModeNumber(value: boolean);
begin
   if value <> fShowModeNumber
      then begin
              fShowModeNumber := value;
              PopulateList;
           end;
end;

end.

