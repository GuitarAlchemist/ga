unit uAutoSizeContainer;

interface

uses
   Classes
   ,Controls
   ;

type
   TAutoSizeContainer = class(TGraphicControl)
      protected
         procedure Paint; override; 
         procedure Resize; override;
      public
         constructor Create(AOwner : TComponent); override;
   end;

implementation

uses
   Graphics
   ;

{ TAutoSizeContainer }

constructor TAutoSizeContainer.Create(AOwner : TComponent);
begin
   inherited Create(AOwner);
   controlStyle := [csAcceptsControls, csCaptureMouse, csClickEvents,
                    csSetCaption, csDoubleClicks, csReplicatable];
end;

procedure TAutoSizeContainer.Paint;
const
   XOR_COLOR = $00FFD8CE;
begin
   if csDesigning in ComponentState
      then with canvas do
              begin
                 pen.style := psDot;
                 pen.mode := pmXor;
                 pen.color := XOR_COLOR;
                 brush.style := bsClear;
                 Rectangle(0, 0, clientWidth, clientHeight);
              end;
end;

procedure TAutoSizeContainer.Resize;
begin
   if parent.componentCount = 1
      then begin
              parent.SetBounds(parent.left, parent.Top, width, height);
              inherited Resize;
           end;
end;

end.
 