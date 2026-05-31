unit uScalePattern;

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
   TScalePattern = class(TCustomControl)
      private
         fMetricsComputed : boolean;
         fTopOffset : integer;
         fBottomOffset : integer;
         fLeftOffset : integer;
         fRightOffset : integer;
         fUsefulTop : integer;
         fUsefulHeight : integer;
         fUsefulWidth : integer;
         fStrDistance : integer;
         fFretDistance : integer;
         fNutOffset : integer;
         procedure ComputeMetrics;
      protected
         procedure Paint; override;
      public
         constructor Create(aOwner : TComponent); override;
   end;


implementation

uses
   Windows
   ,SysUtils
   ,uMusicFontRoutines
   ;

{ TScalePattern }

procedure TScalePattern.ComputeMetrics;
const
   TOP_OFFSET_PERCENTAGE = 10;
   BOTTOM_OFFSET_PERCENTAGE = 5;
begin
   fTopOffset := Round(height * TOP_OFFSET_PERCENTAGE / 100);
   fBottomOffset := Round(height * BOTTOM_OFFSET_PERCENTAGE / 100);
   fUsefulTop := fTopOffset;

   fUsefulHeight := height - fTopOffset - fBottomOffset;
   fStrDistance := Round(fUsefulHeight/6);
   fNutOffset := Round(fStrDistance * 1.65);

   fLeftOffset := fStrDistance div 2;
   fRightOffset := fLeftOffset;
   fUsefulWidth := width - fLeftOffset - fRightOffset;
end;

constructor TScalePattern.Create(aOwner: TComponent);
begin
   inherited;
   fMetricsComputed := false;
end;

procedure TScalePattern.Paint;
var
   x, y : integer;
   str, fret : integer;
   fretCount : integer;
begin
   inherited;

   // Inits
   if not fMetricsComputed
      then try
              ComputeMetrics;
           finally
              fMetricsComputed := true;
           end;

   // Paint the background
   canvas.brush.color := clWhite;
   canvas.FillRect(ClientRect);
   fretCount := 5;

   // Paint the grid
   for fret := 0 to fretCount do
      begin
         x := fLeftOffset + fret * fUsefulWidth div fretCount;
         canvas.MoveTo(x, fTopOffset);
         canvas.LineTo(x, fTopOffset + fUsefulHeight);
      end;
   for str := 0 to 5 do
      begin
         y := fTopOffset + str * fUsefulHeight div 5;
         canvas.MoveTo(fLeftOffset, y);
         canvas.LineTo(fLeftOffset + fUsefulWidth, y);
      end;
end;

end.
