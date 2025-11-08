unit uModeBox;

interface
uses
   StdCtrls
   ,Classes
   ,Types
   ,Messages
   ,Controls
   ,Graphics
   ,uMusicClasses
   ;


type
   TCustomModeBox = class(TCustomComboBox)
      private
         fNeedToPopulate : boolean;
         fOffset : integer;
         fMinOffset : integer;
         fScale : TScale;
         fScaleName : string;
         fDefaultDegreesColor : TColor;
         fColorDegrees : boolean;
         fShowCharacterTones : boolean;
         fShowModeNumber : boolean;
         procedure PopulateList;
         procedure ComputeOffset; virtual;
         procedure CMFontChanged(var Message: TMessage); message CM_FONTCHANGED;
         procedure SetColorDegrees(value : boolean);
         procedure SetDefaultDegreesColor(value : TColor);
         procedure SetScaleName(value : string);
         procedure SetMinOffset(value : integer);
         procedure SetCharacterTones(value : boolean);
         procedure SetShowModeNumber(value : boolean);
      protected
         procedure CreateWnd; override;
         procedure Select; override;
         procedure DrawItem(index : integer; rect : TRect; state : TOwnerDrawState); override;
         property ColorDegrees : boolean read fColorDegrees write SetColorDegrees;
         property DefaultDegreesColor : TColor read fDefaultDegreesColor write SetDefaultDegreesColor;
         property ScaleName : string read fScaleName write SetScaleName;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowCharacterTones : boolean read fShowCharacterTones write SetCharacterTones;
         property ShowModeNumber : boolean read fShowModeNumber write SetShowModeNumber;
      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
   end;

   TModeBox = class(TCustomModeBox)
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
         property ColorDegrees;
         property Constraints;
         property Ctl3D;
         property DropDownCount;
         property DefaultDegreesColor;
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
         property ScaleName;
         property ShowCharacterTones;
         property ShowModeNumber;
         property ShowHint;
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


constructor TCustomModeBox.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   fScale := TScale.Create;
   fScaleName := 'Major';   

   style := csOwnerDrawFixed;
   fNeedToPopulate := true;
   fDefaultDegreesColor := clNavy;
   fShowCharacterTones := true;
   fShowModeNumber := false;
end;

destructor TCustomModeBox.Destroy;
begin
   fScale.Free;
   inherited Destroy;
end;

procedure TCustomModeBox.CreateWnd;
begin
   inherited CreateWnd;

   if fNeedToPopulate
      then PopulateList;
end;

procedure TCustomModeBox.Select;
begin
   inherited Select;
end;

procedure TCustomModeBox.DrawItem(index : integer; rect : TRect; state : TOwnerDrawState);
var
   LRect: TRect;
   LBackground: TColor;
   scale : TScale;
   intervalIndex : integer;
   s : string;
   tm : TTextMetric;
   quality : THalfToneQuality;
   ch : char;
   degreeX, incX : integer;
begin
    canvas.FillRect(Rect);
    LBackground := canvas.Brush.Color;

    LRect := Rect;
    LRect.right := Trunc((lRect.bottom - lRect.top) * 1.8);
    InflateRect(LRect, -1, -1);


    canvas.font.name := font.name;
    canvas.font.height :=  font.height;
    canvas.brush.color := LBackground;

    if odSelected in state
       then canvas.font.color := clWhite
       else canvas.font.color := clBlack;
    canvas.TextOut(rect.left, rect.top + (ItemHeight - canvas.TextHeight('z')) div 2, Items[Index]);

    s := '';
    canvas.font.name := STAFF_FONT_NAME;
    canvas.font.size := Round(font.size * 1.7);
    GetTextMetrics(canvas.handle, tm);
    degreeX := rect.Left + fOffset;
    incX := Round(canvas.TextWidth(DEGREE_QUALITY_bb7) * 1.2);
    scale := TScale.Create;
    try

          globalScaleRepository.GetScale(scale, fScaleName, index + 1);
          for intervalIndex := 0 to scale.count do
             begin
                // Get quality char and color 
                quality := scale.degreeQuality[intervalIndex];
                if odSelected in state
                   then canvas.font.color := clWhite
                   else begin
                           if fColorDegrees
                              then canvas.font.color := GetQualityColor(quality)
                              else canvas.font.color := fDefaultDegreesColor;
                        end;
                ch := GetDegreeQualityChar(quality, scale.useFlatFifth, scale.useFlatSixth);

                // Character tones
                if fShowCharacterTones and (scale.degreeAccidental[intervalIndex] <> naNatural)
                   then canvas.font.style := [fsBold, fsUnderline]
                   else canvas.font.style := [fsBold];

                rect.left := degreeX - canvas.TextWidth(ch) div 2;
                canvas.TextRect(rect, rect.left, rect.top + (ItemHeight - tm.tmHeight) div 2 - 2, ch);
                Inc(degreeX, incX);
             end;
       finally
          scale.Free;
    end;
end;

procedure TCustomModeBox.CMFontChanged(var Message : TMessage);
begin
   canvas.font := font;
   ItemHeight := canvas.TextHeight('Z');
   ComputeOffset;
end;

procedure TCustomModeBox.SetColorDegrees(value : boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              Invalidate;
           end;
end;

procedure TCustomModeBox.SetDefaultDegreesColor(value : TColor);
begin
   if value <> fDefaultDegreesColor
      then begin
              fDefaultDegreesColor := value;
              Invalidate;
           end;
end;

procedure TCustomModeBox.SetScaleName(value : string);
begin
   if (value <> fScaleName) and globalScaleRepository.GetScale(fScale, value)
      then begin
              fScaleName := value;
              PopulateList;
           end;
end;

procedure TCustomModeBox.SetMinOffset(value : integer);
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

procedure TCustomModeBox.SetCharacterTones(value : boolean);
begin
   if value <> fShowCharacterTones
      then begin
              fShowCharacterTones := value;
              Invalidate;
           end;
end;

procedure TCustomModeBox.SetShowModeNumber(value : boolean);
begin
   if value <> fShowModeNumber
      then begin
              fShowModeNumber := value;
              PopulateList;
           end;
end;

procedure TCustomModeBox.PopulateList;
begin
   if HandleAllocated
      then begin
              globalScaleRepository.GetModes(fScaleName, items, fShowModeNumber);
              ItemIndex := 0;
              ComputeOffset;
              fNeedToPopulate := false;
           end
      else fNeedToPopulate := true;
end;

procedure TCustomModeBox.ComputeOffset;
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

end.
