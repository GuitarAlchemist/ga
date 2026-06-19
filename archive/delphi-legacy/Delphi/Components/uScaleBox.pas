unit uScaleBox;

interface
uses
   StdCtrls
   ,Classes
   ,Controls
   ,Graphics
   ,Types
   ,Messages
   ,uMusicClasses
   ;


type
   TCustomScaleBox = class(TCustomComboBox)
      private
         fNeedToPopulate : boolean;
         fOffset : integer;
         fMinOffset : integer;
         fIntervalsColor : TColor;
         fShowHarmonizableOnly : boolean;         
         procedure PopulateList;
         procedure ComputeOffset; virtual;
         procedure CMFontChanged(var Message: TMessage); message CM_FONTCHANGED;
         function GetScaleName: string;
         procedure SetScaleName(value : string);
         procedure SetMinOffset(value : integer);
         procedure SetIntervalsColor(value : TColor);
         procedure SetShowHarmonizableOnly(value : boolean);
      protected
         procedure CreateWnd; override;
         procedure Select; override;
         procedure DrawItem(index : integer; rect : TRect; state : TOwnerDrawState); override;
         property SelectedScaleName : string read GetScaleName write SetScaleName;
         property IntervalsColor : TColor read fIntervalsColor write SetIntervalsColor;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowHarmonizableOnly : boolean read fShowHarmonizableOnly write SetShowHarmonizableOnly;
      public
         constructor Create(aOwner : TComponent); override;
   end;

   TScaleBox = class(TCustomScaleBox)
      published
         property AutoComplete;
         property AutoDropDown;
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
         property Font;
         property ItemIndex;
         property MinOffset;
         property ParentBiDiMode;
         property ParentColor;
         property ParentCtl3D;
         property ParentFont;
         property ParentShowHint;
         property PopupMenu;
         property SelectedScaleName;         
         property ShowHint;
         property ShowHarmonizableOnly;         
         property TabOrder;
         property TabStop;
         property Visible;
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
   Windows
   ,SysUtils
   ,uMusicFontRoutines
   ;


constructor TCustomScaleBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   doubleBuffered := true;
   style := csOwnerDrawFixed;
   fNeedToPopulate := true;
   fShowHarmonizableOnly := false;   

   fIntervalsColor := clNavy;
end;

procedure TCustomScaleBox.CreateWnd;
begin
   inherited CreateWnd;

   if fNeedToPopulate
      then PopulateList;
end;

procedure TCustomScaleBox.Select;
begin
   inherited Select;
end;

procedure TCustomScaleBox.DrawItem(index : integer; rect : TRect; state : TOwnerDrawState);
var
   LRect: TRect;
   LBackground: TColor;
   scale : TScale;
   intervalIndex : integer;
   s : string;
   tm : TTextMetric;
   ch : char;
   degreeX, incX : integer;   
begin
    canvas.FillRect(Rect);
    LBackground := canvas.Brush.Color;

    LRect := Rect;
    LRect.right := Trunc((lRect.bottom - lRect.top) * 1.8);
    InflateRect(LRect, -1, -1);


    canvas.font := font;
    canvas.brush.color := LBackground;

    if odSelected in state
       then canvas.font.color := clWhite
       else canvas.font.color := clBlack;
    s := LowerCase(Items[Index]);
    if Length(s) > 0
       then s[1] := UpCase(s[1]);
    canvas.TextOut(rect.left, rect.top, s);

    s := '';
    canvas.font.name := STAFF_FONT_NAME;
    canvas.font.size := Round(font.size * 1.7);
    if odSelected in state
       then canvas.font.color := clWhite
       else canvas.font.color := fIntervalsColor;
    GetTextMetrics(canvas.handle, tm);
    degreeX := rect.Left + fOffset;
    incX := Round(canvas.TextWidth(DEGREE_QUALITY_bb7) * 1.2);
    scale := TScale.Create;
    try

          globalScaleRepository.GetScale(scale, items[index]);
          for intervalIndex := 0 to scale.count do
             begin
                ch := GetIntervalChar(scale.degreeInterval[intervalIndex]);
                rect.left := degreeX - canvas.TextWidth(ch) div 2;
                canvas.TextRect(rect, rect.left, rect.top + (ItemHeight - tm.tmHeight) div 2 - 2, ch);
                Inc(degreeX, incX);
             end;

          if scale.canHarmonize
             then s := s + ' (H)';    
       finally
          scale.Free;
    end;

    if not (odSelected in state)
       then canvas.Font.Color := clNavy;
    canvas.font.style := [fsBold];
    canvas.TextOut(rect.left + fOffset, rect.top, s);
end;

procedure TCustomScaleBox.PopulateList;
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
                    ComputeOffset;
                    fNeedToPopulate := false;

                 finally
                    items.EndUpdate;
              end;
              fNeedToPopulate := false;
           end
      else fNeedToPopulate := true;
end;



procedure TCustomScaleBox.ComputeOffset;
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
end;

procedure TCustomScaleBox.CMFontChanged(var Message: TMessage);
begin
   canvas.font := font;
   ItemHeight := canvas.TextHeight('Z');
   ComputeOffset;
end;

function TCustomScaleBox.GetScaleName : string;
begin
   if ItemIndex = -1
      then result := ''
      else result := Items[itemIndex];
end;

procedure TCustomScaleBox.SetScaleName(value : string);
var
   index : integer;
begin
   index := Items.IndexOf(value);
   if index <> -1
      then ItemIndex := index;
end;

procedure TCustomScaleBox.SetMinOffset(value : integer);
begin
   if value <> fMinOffset
      then begin
              fMinOffset := value;
              ComputeOffset;
              if value > fOffset
                 then fOffset := value;
              Invalidate;
           end;
end;

procedure TCustomScaleBox.SetIntervalsColor(value : TColor);
begin
   if value <> fIntervalsColor
      then begin
              fIntervalsColor := value;
              Invalidate;
           end;
end;

procedure TCustomScaleBox.SetShowHarmonizableOnly(value : boolean);
begin
   if value <> fShowHarmonizableOnly
      then begin
              fShowHarmonizableOnly := value;
              PopulateList;
           end;
end;

end.
