unit uNumPicker;

interface

uses
   Classes
   ,Controls
   ,uMusicClasses
   ;

type
   TNumPicker = class(TCustomControl)
      private
         fScaleName : string;
         fScaleMode : integer;
         fItemIndex : integer;
         procedure SetScaleMode(const value : integer);
         procedure SetScaleName(const value : string);
         procedure SetItemIndex(const value : integer);
      protected
         procedure Paint; override;
         procedure MouseMove(shift : TShiftState; x, y : integer); override;
      public
         constructor Create(aOwner : TComponent); override;
         destructor Destroy; override;
      published
         property ScaleName : string read fScaleName write SetScaleName;
         property ScaleMode : integer read fScaleMode write SetScaleMode;
         property ItemIndex : integer read fItemIndex write SetItemIndex;
   end;

implementation

{ TNumPicker }

uses
   Graphics
   ,Types
   ,uMusicFontRoutines
   ;

constructor TNumPicker.Create(aOwner : TComponent);
begin
   inherited;

   controlStyle := [csOpaque, csCaptureMouse, csClickEvents];
   doubleBuffered := true;   

   SetBounds(0, 0, 500, 16);
   fScaleName := 'Major';
   fScaleMode := 1;
   fItemIndex := -1;   
end;

destructor TNumPicker.Destroy;
begin

  inherited;
end;

procedure TNumPicker.Paint;
var
   scale : TScale;
   index : integer;
   rootNote : shortint;
   diatonicRootNote : shortint;
   degreeNote : smallint;
   degreeQuality : smallint;
   degreeAccidental : smallint;
   degreeColor : TColor;   
   ch : char;
   x, y : integer;
   rect : TRect;   
begin
   inherited;

   canvas.brush.color := clWhite;
   canvas.FillRect(ClientRect);

   // Draw the degrees
   canvas.pen.color := clBlack;
   canvas.pen.width := 1;
   canvas.font.name := STAFF_FONT_NAME;
   canvas.font.size := 24;
   canvas.font.color := clBlack;
   canvas.font.style := [];

   x := 32;
   y := -20;

   scale := TScale.Create;
   try
      globalScaleRepository.GetScale(scale, fScaleName);
      rootNote := 0;
      for index := 0 to fScaleMode - 2 do
         rootNote := (rootNote + scale.degreeInterval[index]) mod 12;
      diatonicRootNote := DIATONIC_TONE[rootNote];
      degreeNote := rootNote;

      if itemIndex <> -1
         then begin
                 canvas.brush.color := clSkyBlue;
                 rect.top := 0;
                 rect.left := itemIndex * 64;
                 rect.bottom := height;
                 rect.right := rect.left + 64;
                 canvas.FillRect(rect);
              end;

      canvas.brush.color := clWhite;
      canvas.brush.style := bsClear;      
      for index := 0 to scale.count do
         begin
            // Compute the quality for the current degree
            degreeQuality := (DIATONIC_TONE[degreeNote] - diatonicRootNote + 7) mod 7;
            degreeAccidental := DEGREE_ACCIDENTAL[degreeNote] - KEY_ACCIDENTAL[rootNote, DIATONIC_TONE[degreeNote]];
            degreeColor := GetQualityColor(THalfToneQuality(CHOMATIC_TONE[degreeQuality] + degreeAccidental));
            if index = itemIndex
               then canvas.font.color := clWhite
               else canvas.font.color := clBlack;

            // Draw the degree accidental
            if degreeAccidental <> 0
               then begin
                       ch := #0;
                       case degreeAccidental of
                          -2: ch := QUALITY_DIMINISHED;
                          -1: ch := QUALITY_FLAT;
                          1: ch := QUALITY_SHARP;
                          2: ch := QUALITY_AUGMENTED;
                          else Assert(false, 'Invalid accidental');
                       end;
                       canvas.TextOut(x - canvas.TextWidth(ch), y, ch);
                    end;

                    // Draw the degree quality
                    ch := GetChordNumericRomanChar(degreeQuality);
                    canvas.TextOut(x, y, ch);

                    // Pick up next degree note
                    degreeNote := (degreeNote + scale.degreeInterval[fScaleMode + index - 1]) mod 12;
                    Inc(x, 64);
                 end;


   finally
      scale.Free;
   end;
end;

procedure TNumPicker.MouseMove(shift : TShiftState; x, y : integer);
begin
   itemIndex := x div 64;

   inherited MouseMove(shift, x, y);
end;

procedure TNumPicker.SetScaleMode(const value : integer);
begin
   if value <> fScaleMode
      then begin
              fScaleMode := value;
              Invalidate;
           end;
end;

procedure TNumPicker.SetScaleName(const value : string);
begin
   if value <> fScaleName
      then begin
              fScaleName := value;
              Invalidate;
           end;
end;

procedure TNumPicker.SetItemIndex(const value : integer);
begin
   if value <> fItemIndex
      then begin
              if value < 0
                 then fItemIndex := -1
              else if value > 6
                 then fItemIndex := -1;
              fItemIndex := value;
              Invalidate;
           end;
end;

end.
