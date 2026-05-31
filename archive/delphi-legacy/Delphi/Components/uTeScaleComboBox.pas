unit uTeScaleComboBox;

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
   TScaleIntervalsPosition = (sipRight, sipBottom, sipNone);

   TTeCustomScaleComboBox = class(TTeComboBox)
      private
         fComputeContent : boolean;
         fComputeMetrics : boolean;
         fFontSize : integer;
         fOffset : integer;
         fMinOffset : integer;
         fIntervalsColor : TColor;
         fIntervalsPosition : TScaleIntervalsPosition;
         fShowHarmonizableOnly : boolean;
         procedure PopulateList;
         procedure ComputeMetrics; virtual;
         function GetScaleName: string;
         procedure SetScaleName(value : string);
         procedure SetMinOffset(value : integer);
         procedure SetIntervalsColor(value : TColor);
         procedure SetIntervalsPosition(value : TScaleIntervalsPosition);
         procedure SetShowHarmonizableOnly(value : boolean);
         procedure SetFontSize(value : integer);         
         procedure AdjustHeight;
      protected
         procedure CreateWnd; override;
         procedure Resize; override;
         procedure DrawItem(listBox : TTeListBox; canvas : TCanvas; index : integer; rect : TRect; state : TOwnerDrawState);
         property SelectedScaleName : string read GetScaleName write SetScaleName;
         property IntervalsColor : TColor read fIntervalsColor write SetIntervalsColor;
         property IntervalsPosition : TScaleIntervalsPosition read fIntervalsPosition write SetIntervalsPosition;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowHarmonizableOnly : boolean read fShowHarmonizableOnly write SetShowHarmonizableOnly;
         property fontSize : integer read fFontSize write SetFontSize;
      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
   end;

   TTeScaleComboBox = class(TTeCustomScaleComboBox)
      protected
      published
         property SelectedScaleName;
         property IntervalsColor;
         property IntervalsPosition;
         property MinOffset;
         property ShowHarmonizableOnly;
         property FontSize;
   end;

   TTeCustomScaleList = class(TTeListBox)
      private
         fComputeContent : boolean;
         fComputeMetrics : boolean;
         fOffset : integer;
         fMinOffset : integer;
         fIntervalsColor : TColor;
         fShowHarmonizableOnly : boolean;
         fIntervalsPosition : TScaleIntervalsPosition;
         procedure PopulateList; virtual;
         procedure ComputeMetrics; virtual;
         procedure CMFontChanged(var Message: TMessage); message CM_FONTCHANGED;
         function GetScaleName: string;
         procedure SetScaleName(value : string);
         procedure SetMinOffset(value : integer);
         procedure SetIntervalsColor(value : TColor);
         procedure SetShowHarmonizableOnly(value : boolean);
         procedure SetIntervalsPosition(value : TScaleIntervalsPosition);
      protected
         procedure CreateWnd; override;
         procedure DrawItem(Canvas: TCanvas; Index: integer; ARect: TRect); override;

         property SelectedScaleName : string read GetScaleName write SetScaleName;
         property IntervalsColor : TColor read fIntervalsColor write SetIntervalsColor;
         property IntervalsPosition : TScaleIntervalsPosition read fIntervalsPosition write SetIntervalsPosition;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowHarmonizableOnly : boolean read fShowHarmonizableOnly write SetShowHarmonizableOnly;
      public
         constructor Create(aOwner : TComponent); override;
   end;

   TTeScaleList = class(TTeCustomScaleList)
      published
         property IntervalsPosition;
         property MinOffset;
         property SelectedScaleName;
         property ShowHarmonizableOnly;
   end;

implementation

uses
   Windows
   ,SysUtils
   ,uMusicFontRoutines
   ,te_utils
   ,te_theme
   ;


constructor TTeCustomScaleList.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   ListStyle := lbOwnerDrawFixed;

   fComputeContent := true;
   fComputeMetrics := true;
   fShowHarmonizableOnly := false;
   fIntervalsPosition := sipRight;

   fIntervalsColor := clNavy;
end;

procedure TTeCustomScaleList.CreateWnd;
begin
   inherited CreateWnd;

   if fComputeContent
      then PopulateList;
   if fComputeMetrics
      then ComputeMetrics;
end;

procedure TTeCustomScaleList.CMFontChanged(var Message: TMessage);
begin
   canvas.font := font;
   if fIntervalsPosition in [sipRight, sipNone]
      then ItemHeight := canvas.TextHeight('Z')
      else ItemHeight := canvas.TextHeight('Z') * 2;
   ComputeMetrics;
end;

procedure TTeCustomScaleList.DrawItem(Canvas: TCanvas; Index: integer; ARect: TRect);
var
   LBackground: TColor;
   scale : TScale;
   intervalIndex : integer;
   s : string;
   tm : TTextMetric;
   ch : char;
   incX : integer;
   x, y : integer;
begin
   s := LowerCase(Items[Index]);
   if Length(s) > 0
      then s[1] := UpCase(s[1]);
   canvas.TextRect(ARect, ARect.left, ARect.top, s);

   // Draw then scale intervals
   if fIntervalsPosition <> sipNone
      then begin
              canvas.font.name := STAFF_FONT_NAME;
              canvas.font.size := Round(font.size * 1.6);
              canvas.brush.style := bsClear;
              if not Selected[index]
                 then canvas.font.color := fIntervalsColor;
              GetTextMetrics(canvas.handle, tm);
              if fIntervalsPosition = sipRight
                 then begin
                         x := ARect.Left + fOffset - canvas.TextWidth(ch) div 2 + 2;
                         y := ARect.top + (ItemHeight - tm.tmHeight) div 2 - 2;
                      end
                 else begin
                         x := ARect.left + 15;
                         y := ARect.top;
                      end;
              incX := Round(canvas.TextWidth(DEGREE_QUALITY_Sharp11) * 1.1);

              canvas.font.style := [fsBold];
              scale := TScale.Create;
              try

                    globalScaleRepository.GetScale(scale, items[index]);
                    for intervalIndex := 0 to scale.count do
                       begin
                          ch := GetIntervalChar(scale.degreeInterval[intervalIndex]);
                          canvas.TextRect(ARect, x, y, ch);
                          Inc(x, incX);
                       end;
                 finally
                    scale.Free;
              end;
           end;
end;

procedure TTeCustomScaleList.PopulateList;
var
   allScales : TStringList;
   index : integer;
   scale : TScale;
begin
   if HandleAllocated
      then begin
              items.BeginUpdate;
              try
                    if fShowHarmonizableOnly
                       then begin
                               items.Clear;
                               allScales := TStringList.Create;
                               scale := TScale.Create;
                               try
                                     globalScaleRepository.GetNames(allScales);
                                     for index := 0 to allScales.count - 1 do
                                        begin
                                           globalScaleRepository.GetScale(scale, allScales[index]);
                                           if scale.canHarmonize
                                              then items.Add(allScales[index]);
                                        end;

                                  finally
                                     allScales.Free;
                                     scale.Free;
                               end;
                            end
                       else globalScaleRepository.GetNames(items);

                    ItemIndex := 0;
                    ComputeMetrics;
                    fComputeContent := false;

                 finally
                    items.EndUpdate;
              end;
              fComputeContent := false;
           end
      else fComputeContent := true;
end;



procedure TTeCustomScaleList.ComputeMetrics;
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

function TTeCustomScaleList.GetScaleName : string;
begin
   if ItemIndex = -1
      then begin
              if Items.count > 0
                 then result := Items[itemIndex]
                 else result := '';
           end
      else result := Items[itemIndex];
end;

procedure TTeCustomScaleList.SetScaleName(value : string);
var
   index : integer;
begin
   index := Items.IndexOf(value);
   if index <> -1
      then ItemIndex := index;
end;

procedure TTeCustomScaleList.SetMinOffset(value : integer);
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

procedure TTeCustomScaleList.SetIntervalsColor(value : TColor);
begin
   if value <> fIntervalsColor
      then begin
              fIntervalsColor := value;
              Invalidate;
           end;
end;

procedure TTeCustomScaleList.SetShowHarmonizableOnly(value : boolean);
begin
   if value <> fShowHarmonizableOnly
      then begin
              fShowHarmonizableOnly := value;
              fComputeContent := true;
              RecreateWnd;
           end;
end;

procedure TTeCustomScaleList.SetIntervalsPosition(value : TScaleIntervalsPosition);
begin
   if value <> fIntervalsPosition
      then begin
              fIntervalsPosition := value;
              fComputeMetrics := true;
              Perform(CM_FONTCHANGED, 0, 0);
              RecreateWnd;
           end;
end;

{ TTeCustomScaleComboBox }

procedure TTeCustomScaleComboBox.ComputeMetrics;
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

constructor TTeCustomScaleComboBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   ControlStyle := ControlStyle + [csOpaque, csDoubleClicks];
   AutoSize := false;
   ReadOnly := true;
   DropDownCount := 6;
//   OnDrawItem := DrawItem;

   fFontSize := -1;
   fComputeContent := true;
   fShowHarmonizableOnly := false;
   fIntervalsColor := clNavy;
   fIntervalsPosition := sipRight;
end;

destructor TTeCustomScaleComboBox.Destroy;
begin
   inherited Destroy;
end;

procedure TTeCustomScaleComboBox.CreateWnd;
begin
   inherited CreateWnd;

   ComboStyle := kcsDropDownList;
   ListStyle := lbOwnerDrawFixed;
   AdjustHeight;

   if fComputeContent
      then PopulateList;
end;

procedure TTeCustomScaleComboBox.Resize;
begin
   inherited;
   font.height := -height div 3;
   Invalidate;
end;

procedure TTeCustomScaleComboBox.DrawItem(listBox : TTeListBox; canvas : TCanvas; index : integer; rect : TRect; state : TOwnerDrawState);
var
   DrawState: TTeListItemDrawState;

   LBackground: TColor;
   scale : TScale;
   intervalIndex : integer;
   s : string;
   ch : char;
   incX : integer;
   x, y : integer;

   textFontHeight : integer;
   skinFont : TFont;
   tm : TextMetric;
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


   // Draw the scale name
   s := LowerCase(Items[Index]);
   if Length(s) > 0
      then s[1] := UpCase(s[1]);
   canvas.TextRect(rect, rect.left, rect.top, s);
   textFontHeight := canvas.TextHeight('Z');

   // Draw then scale intervals
   if fIntervalsPosition <> sipNone
      then begin
              canvas.font.name := STAFF_FONT_NAME;
              canvas.font.height := -Round(textFontHeight * 1.4);
              canvas.brush.style := bsClear;
              if not (odSelected in state)
                 then canvas.font.color := fIntervalsColor;
              if fIntervalsPosition = sipRight
                 then begin
                         x := rect.Left + fOffset - canvas.TextWidth(ch) div 2 + 2;
                         y := rect.top + (ItemHeight - canvas.TextHeight('Z')) div 2 - 2;
                      end
                 else begin
                         GetTextMetrics(canvas.handle, tm);                 
                         x := rect.left + 15;
                         y := rect.top + textFontHeight - tm.tmDescent;
                      end;
              incX := Round(canvas.TextWidth(DEGREE_QUALITY_Sharp11) * 1.1);

              canvas.font.style := [fsBold];
              scale := TScale.Create;
              try

                    globalScaleRepository.GetScale(scale, items[index]);
                    for intervalIndex := 0 to scale.count do
                       begin
                          ch := GetIntervalChar(scale.degreeInterval[intervalIndex]);
                          canvas.TextRect(rect, x, y, ch);
                          Inc(x, incX);
                       end;
                 finally
                    scale.Free;
              end;
           end;
end;


function TTeCustomScaleComboBox.GetScaleName: string;
begin
   if ItemIndex = -1
      then result := ''
      else result := Items[itemIndex];
end;

procedure TTeCustomScaleComboBox.PopulateList;
var
   allScales : TStringList;
   index : integer;
   scale : TScale;
begin
   if HandleAllocated
      then begin
              items.BeginUpdate;
              try
                    items.Clear;              
                    if fShowHarmonizableOnly
                       then begin
                               allScales := TStringList.Create;
                               scale := TScale.Create;
                               try
                                     globalScaleRepository.GetNames(allScales);
                                     for index := 0 to allScales.count - 1 do
                                        begin
                                           globalScaleRepository.GetScale(scale, allScales[index]);
                                           if scale.canHarmonize
                                              then items.Add(allScales[index]);
                                        end;

                                  finally
                                     allScales.Free;
                                     scale.Free;
                               end;
                            end
                       else globalScaleRepository.GetNames(items);

                    ItemIndex := 0;
                    ComputeMetrics;
                    fComputeContent := false;

                 finally
                    items.EndUpdate;
              end;
              fComputeContent := false;
           end
      else fComputeContent := true;
end;

procedure TTeCustomScaleComboBox.SetIntervalsColor(value: TColor);
begin
   if value <> fIntervalsColor
      then begin
              fIntervalsColor := value;
              Invalidate;
           end;
end;

procedure TTeCustomScaleComboBox.SetIntervalsPosition(value : TScaleIntervalsPosition);
begin
   if value <> fIntervalsPosition
      then begin
              fIntervalsPosition := value;
              fComputeMetrics := true;
              Perform(CM_FONTCHANGED, 0, 0);
              RecreateWnd;
           end;
end;

procedure TTeCustomScaleComboBox.SetMinOffset(value: integer);
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

procedure TTeCustomScaleComboBox.SetScaleName(value: string);
var
   index : integer;
begin
   index := Items.IndexOf(value);
   if index <> -1
      then ItemIndex := index;
end;

procedure TTeCustomScaleComboBox.SetShowHarmonizableOnly(value: boolean);
begin
   if value <> fShowHarmonizableOnly
      then begin
              fShowHarmonizableOnly := value;
              PopulateList;
           end;
end;

procedure TTeCustomScaleComboBox.AdjustHeight;
var
   fontHeight : integer;
begin
   if fFontSize <> -1
      then canvas.font.size := fFontSize;
   fontHeight := canvas.TextHeight('Z');
   if fIntervalsPosition in [sipRight, sipNone]
      then ItemHeight := fontHeight
      else ItemHeight := fontHeight * 2;
   height := ItemHeight + (BorderWidth + BevelWidth + 1) * 2;
   font.height := -height;
   ComputeMetrics;
end;

procedure TTeCustomScaleComboBox.SetFontSize(value : integer);
begin
   if value <> fFontSize
      then begin
              fFontSize := value;
              AdjustHeight;
           end;
end;

end.
