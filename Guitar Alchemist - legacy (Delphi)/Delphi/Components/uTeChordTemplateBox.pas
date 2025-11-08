unit uTeChordTemplateBox;

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
   TChordTemplateDegreesPosition = (tdpRight, tdpBottom, tdpNone);

   TTeChordTemplateListChange = procedure (chordTemplate : TChordTemplate) of object;

   TTeChordTemplateList = class(TTeListBox)
      private
         fComputeContent : boolean;
         fComputeMetrics : boolean;
         fOffset : integer;
         fMinOffset : integer;
         fScale : TScale;
         fScaleName : string;
         fScaleMode : integer;
         fDefaultDegreesColor : TColor;
         fColorDegrees : boolean;
         fShowChordKind : boolean;
         fShowCharacterTones : boolean;
         fDegreesPosition : TChordTemplateDegreesPosition;
         fChordKingImageList : TImageList;
         fOnChange : TTeChordTemplateListChange;
         fScaleRoot : THalfTone;
         fRememberItemIndex : boolean;
         procedure DoDrawItem(Control: TWinControl; Canvas: TCanvas; Index: Integer;
                              Rect: TRect; State: TOwnerDrawState);
         procedure PopulateList;
         procedure ComputeMetrics; virtual;
         procedure CMFontChanged(var Message: TMessage); message CM_FONTCHANGED;
         procedure SetColorDegrees(value : boolean);
         procedure SetDefaultDegreesColor(value : TColor);
         procedure SetScaleRoot(value: THalfTone);
         procedure SetScaleName(value : string);
         procedure SetScaleMode(value : integer);
         procedure SetMinOffset(value : integer);
         procedure SetShowChordKind(value : boolean);
         procedure SetCharacterTones(value : boolean);
         procedure SetDegreesPosition(value : TChordTemplateDegreesPosition);
         procedure SetChordKindImageList(value : TImageList);
         procedure ChangeChordKindImageList(sender : TObject); virtual;
      protected
         procedure CreateWnd; override;
         procedure Paint; override;
         procedure Click; override;
         procedure Change; dynamic;

      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
         procedure Loaded; override;
         function GetSelectedChordTemplate : TChordTemplate;
         function GetChordName(chordTemplate : TChordTemplate) : string;         
         procedure PlaySelectedTemplate;

      published
         property ColorDegrees : boolean read fColorDegrees write SetColorDegrees;
         property DefaultDegreesColor : TColor read fDefaultDegreesColor write SetDefaultDegreesColor;
         property DegreesPosition : TChordTemplateDegreesPosition read fDegreesPosition write SetDegreesPosition;
         property ScaleRoot : THalfTone read fScaleRoot write SetScaleRoot;
         property ScaleName : string read fScaleName write SetScaleName;
         property ScaleMode : integer read fScaleMode write SetScaleMode;
         property MinOffset : integer read fMinOffset write SetMinOffset;
         property ShowChordKind : boolean read fShowChordKind write SetShowChordKind;
         property ShowCharacterTones : boolean read fShowCharacterTones write SetCharacterTones;
         property ChordKindImageList : TImageList read fChordKingImageList write SetChordKindImageList;
         property OnChange : TTeChordTemplateListChange read fOnChange write fOnChange;

   end;

implementation

uses
   Windows
   ,SysUtils
   ,uMusicFontRoutines
   ,uMidi
   ;


constructor TTeChordTemplateList.Create(aOwner: TComponent);
begin
   inherited Create(AOwner);
   ListStyle := lbOwnerDrawFixed;

   fScale := TScale.Create;
   fScaleName := 'Major';
   fScaleMode := 1;
   fRememberItemIndex := false;

   fComputeContent := true;
   fComputeMetrics := true;
   fDefaultDegreesColor := clNavy;
   fShowChordKind := true;
   fShowCharacterTones := true;

   fChordKingImageList := nil;

   fOnChange := nil;

   OnDrawItem := DoDrawItem;
end;

destructor TTeChordTemplateList.Destroy;
begin
   if fChordKingImageList <> nil
      then fChordKingImageList.onChange := nil;
   fOnChange := nil;
   fScale.Free;
   inherited;
end;

procedure TTeChordTemplateList.CreateWnd;
begin
   inherited CreateWnd;
   if fComputeContent
      then PopulateList;
   if fComputeMetrics
      then ComputeMetrics;
end;

procedure TTeChordTemplateList.Loaded;
begin
   inherited Loaded;
   if fComputeContent
      then PopulateList;
   if fComputeMetrics
      then ComputeMetrics;
end;

procedure TTeChordTemplateList.CMFontChanged(var Message: TMessage);
begin
   canvas.font := font;
   if fDegreesPosition in [tdpRight, tdpNone]
      then ItemHeight := canvas.TextHeight('Z')
      else ItemHeight := canvas.TextHeight('Z') * 2;
   ComputeMetrics;
end;

procedure TTeChordTemplateList.DoDrawItem(Control: TWinControl; Canvas: TCanvas; Index: Integer;
                                          Rect: TRect; State: TOwnerDrawState);
var
   intervalIndex : integer;
   tm : TTextMetric;
   chordTemplate : TChordTemplate;
   quality : THalfToneQuality;
   ch : char;
   incX : integer;
   x, y : integer;
   chordName : string;
   skinColor : TColor;
   bitmap : graphics.TBitmap;
begin
    inherited;

    // Retrieve the current chord template
    Assert(fScale.matchingChords.objects[index] is TChordTemplate);
    chordTemplate := TChordTemplate(fScale.matchingChords.objects[index]);

    // Draw the chord name
    skinColor := canvas.font.color;
    chordName := GetChordName(chordTemplate);
    canvas.font.name := CHORDS_FONT_NAME;
    canvas.font.height := -ItemHeight;
    canvas.brush.style := bsClear;
    GetTextMetrics(canvas.handle, tm);
    if chordTemplate.containsOnlyStackedThirds
       then canvas.font.style := [fsBold]
       else canvas.font.style := [];
    canvas.TextOut(Rect.left, Rect.top + (ItemHeight - tm.tmHeight) div 2 - 2, chordName);

    if fDegreesPosition <> tdpNone
       then begin
               // Draw degrees
               canvas.font.name := STAFF_FONT_NAME;
               if fDegreesPosition = tdpBottom
                  then canvas.font.height := -Round(ItemHeight * 1.3 / 2)
                  else canvas.font.height := -Round(ItemHeight * 1.3);
               canvas.font.style := [fsBold];
               GetTextMetrics(canvas.handle, tm);
               if fDegreesPosition = tdpRight
                  then begin
                          x := Rect.Left + fOffset;
                          y := Rect.top + (ItemHeight - tm.tmHeight) div 2 - 2
                       end
                  else begin
                          x := Rect.Left + 15;
                          y := Rect.top;
                       end;
               incX := Round(canvas.TextWidth(DEGREE_QUALITY_b13) * 1.3);

               // Draw chord kind
               if fShowChordKind and (fChordKingImageList <> nil)
                  then begin
                          bitmap := graphics.TBitmap.Create;
                          try
                             bitmap.transparentMode := tmAuto;
                             bitmap.transparent := true;
                             fChordKingImageList.GetBitmap(Ord(chordTemplate.kind), bitmap);
                             canvas.Draw(x, y + ItemHeight - (ItemHeight - bitmap.Height) div 2, bitmap);
                          finally
                             bitmap.Free;
                          end;
                          Inc(x, Round(incX * 2));                          
                       end;

               canvas.brush.style := bsClear;
               for intervalIndex := 0 to chordTemplate.chordDegreesCount - 1 do
                  begin
                     // Get quality char and color
                     quality := chordTemplate.chordDegrees[intervalIndex];
                     if not selected[index]
                        then begin
                                if fColorDegrees
                                   then canvas.font.color := GetQualityColor(quality)
                                   else canvas.font.color := fDefaultDegreesColor;
                             end
                        else canvas.font.color := skinColor;
                     ch := GetChordQualityChar(quality);

                     // Alterations
                     if IsAlteration(quality)
                        then canvas.font.style := [fsUnderline]
                        else canvas.font.style := [];

                     // Character tones
{
                     if fShowCharacterTones
                        then begin
                                characterTone := fScale.QualityCharacterTone(quality);
                                     if characterTone = ctPrimary
                                        then canvas.font.style := [fsBold, fsUnderline]
                                else if characterTone = ctSecondary
                                        then canvas.font.style := [fsBold, fsItalic]
                                else         canvas.font.style := [fsBold];
                             end
                        else
}                        

                     // Draw degree quality
                     canvas.TextOut(x - canvas.TextWidth(ch) div 2, y, ch);
                     Inc(x, incX);
                  end;
            end;   
end;

procedure TTeChordTemplateList.Paint;
begin
   if fComputeContent
      then PopulateList;
   if fComputeMetrics
      then ComputeMetrics;
   inherited;
end;

procedure TTeChordTemplateList.Click;
begin
   Change;
   inherited Click;
end;

procedure TTeChordTemplateList.Change;
begin
   if Assigned(fOnChange)
      then OnChange(GetSelectedChordTemplate);
end;

procedure TTeChordTemplateList.PopulateList;
var
   oldItemIndex : integer;
begin
   if not(csReading in ComponentState)
      then begin
              if HandleAllocated
                 then begin
                         oldItemIndex := ItemIndex;
                         Items.Clear;
                         if globalScaleRepository.GetScale(fScale, fScaleName, fScaleMode)
                            then Items.Assign(fScale.matchingChords);
                         fComputeContent := false;
                         if fRememberItemIndex
                            then begin
                                    ItemIndex := oldItemIndex;
                                    fRememberItemIndex := false;
                                 end
                            else ItemIndex := 0;
                         ComputeMetrics;
                      end
                 else fComputeContent := true;
           end;
end;

procedure TTeChordTemplateList.ComputeMetrics;
var
   index : integer;
   textWidth : integer;
   chordTemplate : TChordTemplate;   
   chordName : string;
begin
   if not(csReading in ComponentState)
      then begin
              canvas.font := font;
              canvas.font.name := CHORDS_FONT_NAME;
              canvas.font.height := -ItemHeight;
              canvas.font.style := [fsBold];
              fOffset := 0;
              for index := 0 to items.count - 1 do
                 begin
                    Assert(fScale.matchingChords.objects[index] is TChordTemplate);
                    chordTemplate := TChordTemplate(fScale.matchingChords.objects[index]);
                    chordName := GetChordName(chordTemplate);
                    textWidth := canvas.TextWidth(chordName);
                    if textWidth > fOffset
                       then fOffset := textWidth;
                 end;
              Inc(fOffset, 10);
              if fMinOffset > fOffset
                 then fOffset := fMinOffset;

              fComputeMetrics := false;
           end;   
end;

procedure TTeChordTemplateList.SetScaleName(value : string);
begin
   if (value <> fScaleName) and globalScaleRepository.GetScale(fScale, value)
      then begin
              fScaleName := value;
              fComputeContent := true;
              Invalidate;
           end;
end;

procedure TTeChordTemplateList.SetScaleMode(value : integer);
begin
   if value <> fScaleMode
      then begin
              fScaleMode := value;
              fComputeContent := true;
              Invalidate;
           end;
end;

procedure TTeChordTemplateList.SetMinOffset(value : integer);
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

procedure TTeChordTemplateList.SetDegreesPosition(value : TChordTemplateDegreesPosition);
begin
   if value <> fDegreesPosition
      then begin
              fDegreesPosition := value;
              fComputeMetrics := true;
              Perform(CM_FONTCHANGED, 0, 0);
              RecreateWnd;
           end;
end;

procedure TTeChordTemplateList.SetChordKindImageList(value : TImageList);
begin
   if value <> fChordKingImageList
      then begin
              fChordKingImageList := value;
              if fChordKingImageList <> nil
                 then fChordKingImageList.onChange := ChangeChordKindImageList;
              Invalidate;
           end;
end;

function TTeChordTemplateList.GetChordName(chordTemplate : TChordTemplate) : string;
begin
   if chordTemplate <> nil
      then begin
              if HALFTONE_IS_SHARP_KEY[fScaleRoot]
                 then result := HALFTONE_NAME_SHARP[fScaleRoot]
                 else result := HALFTONE_NAME_FLAT[fScaleRoot];
              result := result + ' ' + chordTemplate.chordName;
           end
      else result := '';
end;

procedure TTeChordTemplateList.ChangeChordKindImageList(sender : TObject);
begin
   if not (csDestroying in ComponentState)
      then Invalidate;
end;

procedure TTeChordTemplateList.SetShowChordKind(value : boolean);
begin
   if value <> fShowChordKind
      then begin
              fShowChordKind := value;
              Invalidate;
           end;
end;

procedure TTeChordTemplateList.SetCharacterTones(value: boolean);
begin
   if value <> fShowCharacterTones
      then begin
              fShowCharacterTones := value;
              Invalidate;
           end;
end;

procedure TTeChordTemplateList.SetColorDegrees(value: boolean);
begin
   if value <> fColorDegrees
      then begin
              fColorDegrees := value;
              Invalidate;
           end;
end;

procedure TTeChordTemplateList.SetDefaultDegreesColor(value: TColor);
begin
   if value <> fDefaultDegreesColor
      then begin
              fDefaultDegreesColor := value;
              if not fColorDegrees
                 then Invalidate;
           end;
end;

function TTeChordTemplateList.GetSelectedChordTemplate : TChordTemplate;
begin
   if itemIndex = -1
      then result := nil
      else begin
              Assert(fScale.matchingChords.objects[itemIndex] is TChordTemplate);
              result := TChordTemplate(fScale.matchingChords.objects[itemIndex]);
           end;
end;

procedure TTeChordTemplateList.PlaySelectedTemplate;
begin
   globalMidi.PlayChordTemplate(GetSelectedChordTemplate, fScaleRoot);
end;

procedure TTeChordTemplateList.SetScaleRoot(value: THalfTone);
begin
   if value <> fScaleRoot
      then begin
              fScaleRoot := value;
              fComputeContent := true;
              fRememberItemIndex := true;
              Invalidate;
           end;
end;

end.

