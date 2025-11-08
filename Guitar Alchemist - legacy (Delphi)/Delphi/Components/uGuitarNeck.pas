unit uGuitarNeck;

interface

uses
  Windows, Messages, SysUtils, Classes, Controls;

type
  TGuitarNeck = class(TGraphicControl)
     private
     protected
        procedure Paint; override;
     public
        constructor Create;
        destructor Destroy; override;
  end;


implementation

uses
   Graphics
   ;

{ TGuitarNeck }

constructor TGuitarNeck.Create;
begin
   SetBounds(0, 0, 600, 150);
end;

destructor TGuitarNeck.Destroy;
begin

   inherited;
end;

procedure TGuitarNeck.Paint;
begin
   inherited;

   // Clear background
   canvas.brush.color := clWhite;
   canvas.brush.style := bsSolid;
   canvas.FillRect(Rect(0, 0, width, height));
   canvas.brush.style := bsClear;
   canvas.pen.width := 1;
   canvas.pen.color := clBlack;
   canvas.pen.style := psSolid;
   canvas.pen.mode := pmCopy;
end;

end.




