unit uLayout;

interface

uses
   Controls
   ,Buttons
   ,Classes
   ,Graphics
   ,Types
   ;


type
   TCustomLayout = class(TCustomControl)
      // Internal offsets info
      private
         // Offset options
         fUseLeftOffset : boolean;
         fUseRightOffset : boolean;
         fUseTopOffset : boolean;
         fUseBottomOffset : boolean;
         fUseLeftPropOffset : boolean;
         fUseRightPropOffset : boolean;
         fUseTopPropOffset : boolean;
         fUseBottomPropOffset : boolean;

         // Internal flags
         fLeftPropOffsetComputed : boolean;
         fRightPropOffsetComputed : boolean;
         fTopPropOffsetComputed : boolean;
         fBottomPropOffsetComputed : boolean;

         // Regular offsets
         fLeftOffset : integer;
         fTopOffset : integer;
         fRightOffset : integer;
         fBottomOffset : integer;

         // Proportional offsets
         fLeftPropOffset : integer;
         fTopPropOffset : integer;
         fRightPropOffset : integer;
         fBottomPropOffset : integer;

         // Proportional percentages and coefficients
         fLeftPropOffsetPercentage : extended;
         fTopPropOffsetPercentage : extended;
         fRightPropOffsetPercentage : extended;
         fBottomPropOffsetPercentage : extended;

         fLeftPropOffsetCoeff : extended;
         fTopPropOffsetCoeff : extended;
         fRightPropOffsetCoeff : extended;
         fBottomPropOffsetCoeff : extended;

         // Access functions
         function GetActualLeftOffset : integer;
         function GetActualRightOffset : integer;
         function GetActualTopOffset : integer;
         function GetActualBottomOffset : integer;
         function GetActualLeftPropOffset : integer;
         function GetActualRightPropOffset : integer;
         function GetActualBottomPropOffset : integer;
         function GetActualTopPropOffset : integer;
         function GetActualLeftPropOffsetPercentage : extended;
         function GetActualRightPropOffsetPercentage : extended;
         function GetActualTopPropOffsetPercentage : extended;
         function GetActualBottomPropOffsetPercentage : extended;

         procedure SetLeftOffset(value : integer);
         procedure SetRightOffset(value : integer);
         procedure SetTopOffset(value : integer);
         procedure SetBottomOffset(value : integer);
         procedure SetLeftPropOffset(value : integer);
         procedure SetRightPropOffset(value : integer);
         procedure SetTopPropOffset(value : integer);
         procedure SetBottomPropOffset(value : integer);
         procedure SetLeftPropOffsetPercentage(value : extended);
         procedure SetRightPropOffsetPercentage(value : extended);
         procedure SetTopPropOffsetPercentage(value : extended);
         procedure SetBottomPropOffsetPercentage(value : extended);

         procedure SetUseLeftOffset(value : boolean);
         procedure SetUseRightOffset(value : boolean);
         procedure SetUseTopOffset(value : boolean);
         procedure SetUseBottomOffset(value : boolean);
         procedure SetUseLeftPropOffset(value : boolean);
         procedure SetUseRightPropOffset(value : boolean);
         procedure SetUseTopPropOffset(value : boolean);
         procedure SetUseBottomPropOffset(value : boolean);

         procedure SetLeftPropOffsetCoeff(value : extended);
         procedure SetRightPropOffsetCoeff(value : extended);
         procedure SetTopPropOffsetCoeff(value : extended);
         procedure SetBottomPropOffsetCoeff(value : extended);


      // Offset properties
      protected
         // Fixed offsets
         property leftOffset : integer read fLeftOffset write SetLeftOffset;
         property rightOffset : integer read fRightOffset write SetRightOffset;
         property topOffset : integer read fTopOffset write SetTopOffset;
         property bottomOffset : integer read fBottomOffset write SetBottomOffset;

         property useLeftOffset : boolean read fUseLeftOffset write SetUseLeftOffset;
         property useRightOffset : boolean read fUseRightOffset write SetUseRightOffset;
         property useTopOffset : boolean read fUseTopOffset write SetUseTopOffset;
         property useBottomOffset : boolean read fUseBottomOffset write SetUseBottomOffset;

         property actualLeftOffset : integer read GetActualLeftOffset;
         property actualRightOffset : integer read GetActualRightOffset;
         property actualTopOffset : integer read GetActualTopOffset;
         property actualBottomOffset : integer read GetActualBottomOffset;


         // Proportional offsets
         property leftPropOffset : integer read fLeftPropOffset write SetLeftPropOffset;
         property rightPropOffset : integer read fRightPropOffset write SetRightPropOffset;
         property topPropOffset : integer read fTopPropOffset write SetTopPropOffset;
         property bottomPropOffset : integer read fBottomPropOffset write SetBottomPropOffset;

         property leftPropOffsetPercentage : extended read fLeftPropOffsetPercentage write SetLeftPropOffsetPercentage;
         property rightPropOffsetPercentage : extended read fRightPropOffsetPercentage write SetRightPropOffsetPercentage;
         property topPropOffsetPercentage : extended read fTopPropOffsetPercentage write SetTopPropOffsetPercentage;
         property bottomPropOffsetPercentage : extended read fBottomPropOffsetPercentage write SetBottomPropOffsetPercentage;

         property useLeftPropOffset : boolean read fUseLeftPropOffset write SetUseLeftPropOffset;
         property useRightPropOffset : boolean read fUseRightPropOffset write SetUseRightPropOffset;
         property useTopPropOffset : boolean read fUseTopPropOffset write SetUseTopPropOffset;
         property useBottomPropOffset : boolean read fUseBottomPropOffset write SetUseBottomPropOffset;

         property actualLeftPropOffset : integer read GetActualLeftPropOffset;
         property actualRightPropOffset : integer read GetActualRightPropOffset;
         property actualTopPropOffset : integer read GetActualTopPropOffset;
         property actualBottomPropOffset : integer read GetActualBottomPropOffset;

         property actualLeftPropOffsetPercentage : extended read GetActualLeftPropOffsetPercentage;
         property actualRightPropOffsetPercentage : extended read GetActualRightPropOffsetPercentage;
         property actualTopPropOffsetPercentage : extended read GetActualTopPropOffsetPercentage;
         property actualBottomPropOffsetPercentage : extended read GetActualBottomPropOffsetPercentage;

         property leftPropOffsetCoeff : extended read fleftPropOffsetCoeff write SetLeftPropOffsetCoeff;
         property rightPropOffsetCoeff : extended read fRightPropOffsetCoeff write SetRightPropOffsetCoeff;
         property topPropOffsetCoeff : extended read fTopPropOffsetCoeff write SetTopPropOffsetCoeff;
         property bottomPropOffsetCoeff : extended read fBottomPropOffsetCoeff write SetBottomPropOffsetCoeff;



      // Metrics
      private
         fLayoutWidth : integer;
         fLayoutHeight : integer;
         fOffsetsChanged : boolean;
         fNoReinitMetrics : boolean;
      protected
         fComputingMetrics : boolean;      
         fMetricsChanged : boolean;
         procedure ComputeMetrics; virtual;
         procedure MetricsChanged; virtual;
         procedure OffsetsChanged; virtual;
         function GetLayoutRect : TRect; virtual;
         function GetFixedLayoutRect : TRect; virtual;
         function GetPropLayoutRect : TRect; virtual;
         function GetLayoutLeft : integer; virtual;
         function GetLayoutRight : integer; virtual;
         function GetLayoutTop : integer; virtual;
         function GetLayoutBottom : integer; virtual;
         function GetLayoutLeftOffset : integer; virtual;
         function GetLayoutRightOffset : integer; virtual;
         function GetLayoutTopOffset : integer; virtual;
         function GetLayoutBottomOffset : integer; virtual;
         function CanResize(var newWidth, newHeight : integer) : boolean; override;
         property computingMetrics : boolean read fComputingMetrics;
         property offsetsHaveChanged : boolean read fOffsetsChanged write fOffsetsChanged;
      public
         property layoutRect : TRect read GetLayoutRect;
         property fixedLayoutRect : TRect read GetFixedLayoutRect;
         property propLayoutRect : TRect read GetPropLayoutRect;
         property layoutLeft : integer read GetLayoutLeft;
         property layoutRight : integer read GetLayoutRight;
         property layoutTop : integer read GetLayoutTop;
         property layoutBottom : integer read GetLayoutBottom;
         property layoutWidth : integer read fLayoutWidth;
         property layoutHeight : integer read fLayoutHeight;
         property layoutLeftOffset : integer read GetLayoutLeftOffset;
         property layoutRightOffset : integer read GetLayoutRightOffset;
         property layoutTopOffset : integer read GetLayoutTopOffset;
         property layoutBottomOffset : integer read GetLayoutBottomOffset;


      // Painting and constructor
      protected
         procedure CreateWnd; override;
         procedure Paint; override;
      public
         constructor Create(aOwner : TComponent); override;
   end;

   TTestLayout = class(TCustomLayout)
      protected
         procedure Paint; override;
      published
         property useLeftOffset;
         property useRightOffset;
         property useTopOffset;
         property useBottomOffset;
         property useLeftPropOffset;
         property useRightPropOffset;
         property useTopPropOffset;
         property useBottomPropOffset;

         property leftOffset;
         property rightOffset;
         property topOffset;
         property bottomOffset;
         property leftPropOffset;
         property rightPropOffset;
         property topPropOffset;
         property bottomPropOffset;
         property leftPropOffsetPercentage;
         property rightPropOffsetPercentage;
         property topPropOffsetPercentage;
         property bottomPropOffsetPercentage;

         property width;
         property height;
   end;


implementation

{ TCustomLayout }

constructor TCustomLayout.Create(aOwner: TComponent);
begin
   inherited Create(aOwner);

   controlStyle := [csOpaque, csCaptureMouse, csClickEvents, csSetCaption];

   fLeftOffset := 0;
   fTopOffset := 0;
   fRightOffset := 0;
   fBottomOffset := 0;

   fLeftPropOffset := 0;
   fTopPropOffset := 0;
   fRightPropOffset := 0;
   fBottomPropOffset := 0;

   fLeftPropOffsetPercentage := 0;
   fTopPropOffsetPercentage := 0;
   fRightPropOffsetPercentage := 0;
   fBottomPropOffsetPercentage := 0;

   fLeftPropOffsetCoeff := 100;
   fTopPropOffsetCoeff := 100;
   fRightPropOffsetCoeff := 100;
   fBottomPropOffsetCoeff := 100;

   fLeftPropOffsetComputed := false;
   fRightPropOffsetComputed := false;
   fTopPropOffsetComputed := false;
   fBottomPropOffsetComputed := false;

   fUseLeftOffset := true;
   fUseRightOffset := true;
   fUseTopOffset := true;
   fUseBottomOffset := true;
   fUseLeftPropOffset := true;
   fUseRightPropOffset := true;
   fUseTopPropOffset := true;
   fUseBottomPropOffset := true;

   SetBounds(0, 0, 100, 50);
   DoubleBuffered := csDesigning in ComponentState;

   fLayoutWidth := width;
   fLayoutHeight := height;

   fMetricsChanged := true;
end;

procedure TCustomLayout.ComputeMetrics;
var
   n, d : extended;
begin
   // Compute layout width and height
   n := width - actualLeftOffset - actualRightOffset
        - actualLeftPropOffset * Ord(fLeftPropOffsetComputed)
        - actualRightPropOffset * Ord(fRightPropOffsetComputed);
   d := 1 +
        (actualLeftPropOffsetPercentage * Ord(not fLeftPropOffsetComputed)
         + actualRightPropOffsetPercentage * Ord(not fRightPropOffsetComputed)) / 100;
   fLayoutWidth := Round(n/d);

   n := height - actualTopOffset - actualBottomOffset
        - actualTopPropOffset * Ord(fTopPropOffsetComputed)
        - actualBottomPropOffset * Ord(fBottomPropOffsetComputed);
   d := 1 +
        (actualTopPropOffsetPercentage * Ord(not fTopPropOffsetComputed)
         + actualBottomPropOffsetPercentage * Ord(not fBottomPropOffsetComputed)) / 100;
   fLayoutHeight := Round(n/d);

   // Compute proportional offsets if needed
   if not fLeftPropOffsetComputed
      then fLeftPropOffset := Round(fLayoutWidth * fLeftPropOffsetPercentage / 100);
   if not fRightPropOffsetComputed
      then fRightPropOffset := Round(fLayoutWidth * fRightPropOffsetPercentage / 100);
   if not fTopPropOffsetComputed
      then fTopPropOffset := Round(fLayoutHeight * fTopPropOffsetPercentage / 100);
   if not fBottomPropOffsetComputed
      then fBottomPropOffset := Round(fLayoutHeight * fBottomPropOffsetPercentage / 100);
end;

procedure TCustomLayout.MetricsChanged;
begin
   fMetricsChanged := true;
   Invalidate;
end;

procedure TCustomLayout.OffsetsChanged;
begin
   fOffsetsChanged := true;
   MetricsChanged;
end;

function TCustomLayout.GetLayoutRect : TRect;
begin
   result.left := actualLeftOffset + actualLeftPropOffset;
   result.right := width - actualRightOffset - actualRightPropOffset;
   result.top := actualTopOffset + actualTopPropOffset;
   result.bottom := height - actualBottomOffset - actualBottomPropOffset;
end;

function TCustomLayout.GetFixedLayoutRect : TRect;
begin
   result.left := actualLeftOffset;
   result.right := width - actualRightOffset;
   result.top := actualTopOffset;
   result.bottom := height - actualBottomOffset;
end;

function TCustomLayout.GetPropLayoutRect : TRect;
begin
   result.left := actualLeftPropOffset;
   result.right := width - actualRightPropOffset;
   result.top := actualTopPropOffset;
   result.bottom := height - actualBottomPropOffset;
end;

function TCustomLayout.GetLayoutLeft : integer;
begin
   result := layoutLeftOffset;
end;

function TCustomLayout.GetLayoutRight : integer;
begin
   result := width - layoutRightOffset;
end;

function TCustomLayout.GetLayoutTop : integer;
begin
   result := layoutTopOffset;
end;

function TCustomLayout.GetLayoutBottom : integer;
begin
   result := height - layoutBottomOffset;
end;

function TCustomLayout.GetLayoutLeftOffset : integer;
begin
   result := actualLeftOffset + actualLeftPropOffset;
end;

function TCustomLayout.GetLayoutRightOffset : integer;
begin
   result := actualRightOffset + actualRightPropOffset;
end;

function TCustomLayout.GetLayoutTopOffset : integer;
begin
   result := actualTopOffset + actualTopPropOffset;
end;

function TCustomLayout.GetLayoutBottomOffset : integer;
begin
   result := actualBottomOffset + actualBottomPropOffset;
end;

function TCustomLayout.GetActualLeftOffset : integer;
begin
   result := fLeftOffset * Ord(fUseLeftOffset);
end;

function TCustomLayout.GetActualRightOffset : integer;
begin
   result := fRightOffset * Ord(fUseRightOffset);
end;

function TCustomLayout.GetActualTopOffset : integer;
begin
   result := fTopOffset * Ord(fUseTopOffset);
end;

function TCustomLayout.GetActualBottomOffset : integer;
begin
   result := fBottomOffset * Ord(fUseBottomOffset)
end;

function TCustomLayout.GetActualLeftPropOffset : integer;
begin
   result := Round(fLeftPropOffset * Ord(fUseLeftPropOffset) * fLeftPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualRightPropOffset : integer;
begin
   result := Round(fRightPropOffset * Ord(fUseRightPropOffset) * fRightPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualTopPropOffset : integer;
begin
   result := Round(fTopPropOffset * Ord(fUseTopPropOffset) * fTopPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualBottomPropOffset:  integer;
begin
   result := Round(fBottomPropOffset * Ord(fUseBottomPropOffset) * fBottomPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualLeftPropOffsetPercentage : extended;
begin
   result := Round(fLeftPropOffsetPercentage * Ord(fUseLeftPropOffset) * fLeftPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualRightPropOffsetPercentage : extended;
begin
   result := Round(fRightPropOffsetPercentage * Ord(fUseRightPropOffset) * fRightPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualTopPropOffsetPercentage : extended;
begin
   result := Round(fTopPropOffsetPercentage * Ord(fUseTopPropOffset) * fTopPropOffsetCoeff / 100);
end;

function TCustomLayout.GetActualBottomPropOffsetPercentage : extended;
begin
   result := Round(fBottomPropOffsetPercentage * Ord(fUseBottomPropOffset) * fBottomPropOffsetCoeff / 100);
end;

procedure TCustomLayout.CreateWnd;
begin
   inherited CreateWnd;
end;

procedure TCustomLayout.Paint;
begin
   inherited Paint;

   // Maybe apply metrics changes
   if fMetricsChanged
      then begin
              fComputingMetrics := true;
              fNoReinitMetrics := false;
              try
                    ComputeMetrics;
                 finally

                    // Re-initialize metrics and offset flags
                    if not fNoReinitMetrics
                       then begin
                               fComputingMetrics := false;
                               fMetricsChanged := false;
                               fOffsetsChanged := false;
                            end;

                    fLeftPropOffsetComputed := false;
                    fRightPropOffsetComputed := false;
                    fTopPropOffsetComputed := false;
                    fBottomPropOffsetComputed := false;
              end;
           end;
end;

procedure TCustomLayout.SetUseLeftOffset(value : boolean);
begin
   if value <> fUseLeftOffset
      then begin
              fUseLeftOffset := value;
              fOffsetsChanged := true;
              if value
                 then width := width + fLeftOffset
                 else width := width - fLeftOffset;
           end;
end;

procedure TCustomLayout.SetUseRightOffset(value : boolean);
begin
   if value <> fUseRightOffset
      then begin
              fUseRightOffset := value;
              fOffsetsChanged := true;
              if value
                 then width := width + fRightOffset
                 else width := width - fRightOffset;
           end;
end;

procedure TCustomLayout.SetUseTopOffset(value : boolean);
begin
   if value <> fUseTopOffset
      then begin
              fUseTopOffset := value;
              fOffsetsChanged := true;
              if value
                 then height := height + fTopOffset
                 else height := height - fTopOffset;
           end;
end;

procedure TCustomLayout.SetUseBottomOffset(value : boolean);
begin
   if value <> fUseBottomOffset
      then begin
              fUseBottomOffset := value;
              fOffsetsChanged := true;
              if value
                 then height := height + fBottomOffset
                 else height := height - fBottomOffset;
           end;
end;

procedure TCustomLayout.SetUseLeftPropOffset(value : boolean);
begin
   if value <> fUseLeftPropOffset
      then begin
              fUseLeftPropOffset := value;
              fLeftPropOffsetComputed := true;              
              fOffsetsChanged := true;
              if value
                 then width := width + fLeftPropOffset
                 else width := width - fLeftPropOffset;
           end;
end;

procedure TCustomLayout.SetUseRightPropOffset(value : boolean);
begin
   if value <> fUseRightPropOffset
      then begin
              fUseRightPropOffset := value;
              fRightPropOffsetComputed := true;
              fOffsetsChanged := true;
              if value
                 then width := width + fRightPropOffset
                 else width := width - fRightPropOffset;
           end;
end;

procedure TCustomLayout.SetUseTopPropOffset(value : boolean);
begin
   if value <> fUseTopPropOffset
      then begin
              fUseTopPropOffset := value;
              fTopPropOffsetComputed := true;
              fOffsetsChanged := true;
              if value
                 then height := height + fTopPropOffset
                 else height := height - fTopPropOffset;
           end;
end;

procedure TCustomLayout.SetUseBottomPropOffset(value : boolean);
begin
   if value <> fUseBottomPropOffset
      then begin
              fUseBottomPropOffset := value;
              fBottomPropOffsetComputed := true;
              fOffsetsChanged := true;
              if value
                 then height := height + fBottomPropOffset
                 else height := height - fBottomPropOffset;
           end;
end;

procedure TCustomLayout.SetLeftPropOffsetCoeff(value : extended);
var
   oldValue : extended;
begin
   if value <> fLeftPropOffsetCoeff
      then begin
              oldValue := fLeftPropOffsetCoeff;
              fLeftPropOffsetCoeff := value;
              if fUseleftOffset
                 then begin
                         fNoReinitMetrics := true;
                         width := width - Round((oldValue - value) * fLeftPropOffset / 100);
                      end;
           end;
end;

procedure TCustomLayout.SetRightPropOffsetCoeff(value : extended);
var
   oldValue : extended;
begin
   if value <> fRightPropOffsetCoeff
      then begin
              oldValue := fRightPropOffsetCoeff;
              fRightPropOffsetCoeff := value;
              if fUseRightOffset
                 then begin
                         fNoReinitMetrics := true;
                         width := width - Round((oldValue - value) * fRightPropOffset / 100);
                      end;
           end;
end;

procedure TCustomLayout.SetTopPropOffsetCoeff(value : extended);
var
   oldValue : extended;
begin
   if value <> fTopPropOffsetCoeff
      then begin
              oldValue := fTopPropOffsetCoeff;
              fTopPropOffsetCoeff := value;
              if fUseTopOffset
                 then begin
                         fNoReinitMetrics := true;
                         fMetricsChanged := true;
                         height := height - Round((oldValue - value) * fTopPropOffset / 100);
                      end;
           end;
end;

procedure TCustomLayout.SetBottomPropOffsetCoeff(value : extended);
var
   oldValue : extended;
begin
   if value <> fBottomPropOffsetCoeff
      then begin
              oldValue := fBottomPropOffsetCoeff;
              fBottomPropOffsetCoeff := value;
              if fUseBottomPropOffset
                 then begin
                         fNoReinitMetrics := true;
                         fMetricsChanged := true;
                         height := height - Round((oldValue - value) * fBottomPropOffset / 100);
                      end;
           end;
end;

procedure TCustomLayout.SetLeftOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fLeftOffset
      then begin
              oldValue := fLeftOffset;
              fLeftOffset := value;
              if fUseRightOffset and (not (csReading in componentState))
                 then width := width + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetRightOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fRightOffset
      then begin
              oldValue := fRightOffset;
              fRightOffset := value;
              if fUseRightOffset and (not (csReading in componentState))
                 then width := width + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetTopOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fTopOffset
      then begin
              oldValue := fTopOffset;
              fTopOffset := value;
              if fUseTopOffset and (not (csReading in componentState))
                 then height := height + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetBottomOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fBottomOffset
      then begin
              oldValue := fBottomOffset;
              fBottomOffset := value;
              fOffsetsChanged := true;
              if fUseBottomOffset and (not (csReading in componentState))
                 then height := height + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;


procedure TCustomLayout.SetLeftPropOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fLeftPropOffset
      then begin
              oldValue := fLeftPropOffset;
              fLeftPropOffset := value;
              fLeftPropOffsetPercentage := 100 * (value / fLayoutWidth);
              fLeftPropOffsetComputed := true;
              fOffsetsChanged := true;
              if fUseLeftPropOffset and (not (csReading in componentState))
                 then width := width + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetRightPropOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fRightPropOffset
      then begin
              oldValue := fRightPropOffset;
              fRightPropOffset := value;
              fRightPropOffsetPercentage := 100 * (value / fLayoutWidth);
              fLeftPropOffsetComputed := true;
              fOffsetsChanged := true;
              if fUseRightPropOffset and (not (csReading in componentState))
                 then width := width + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetTopPropOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fTopPropOffset
      then begin
              oldValue := fTopPropOffset;
              fTopPropOffset := value;
              fTopPropOffsetPercentage := 100 * (value / fLayoutHeight);
              fTopPropOffsetComputed := true;
              fOffsetsChanged := true;
              if fUseTopPropOffset and (not (csReading in componentState))
                 then height := height + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetBottomPropOffset(value : integer);
var
   oldValue : integer;
begin
   if value <> fBottomPropOffset
      then begin
              oldValue := fBottomPropOffset;
              fBottomPropOffset := value;
              fBottomPropOffsetPercentage := 100 * (value / fLayoutHeight);
              fBottomPropOffsetComputed := true;
              fOffsetsChanged := true;
              if fUseBottomPropOffset and (not (csReading in componentState))
                 then height := height + value - oldValue;
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetLeftPropOffsetPercentage(value : extended);
var
   oldValue : extended;
begin
   if value <> fLeftPropOffsetPercentage
      then begin
              oldValue := fLeftPropOffsetPercentage;
              fLeftPropOffsetPercentage := value;
              fOffsetsChanged := true;
              if fUseLeftPropOffset and (not (csReading in componentState))
                 then width := width + Round(fLayoutWidth * ((value - oldValue) / 100));
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetRightPropOffsetPercentage(value : extended);
var
   oldValue : extended;
begin
   if value <> fRightPropOffsetPercentage
      then begin
              oldValue := fRightPropOffsetPercentage;
              fRightPropOffsetPercentage := value;
              fOffsetsChanged := true;
              if fUseRightPropOffset and (not (csReading in componentState))
                 then width := width + Round(fLayoutWidth * ((value - oldValue) / 100));
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetTopPropOffsetPercentage(value : extended);
var
   oldValue : extended;
begin
   if value <> fTopPropOffsetPercentage
      then begin
              oldValue := fTopPropOffsetPercentage;
              fTopPropOffsetPercentage := value;
              fOffsetsChanged := true;
              if fUseTopPropOffset and (not (csReading in componentState))
                 then height := height + Round(fLayoutHeight * ((value - oldValue) / 100));
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

procedure TCustomLayout.SetBottomPropOffsetPercentage(value : extended);
var
   oldValue : extended;
begin
   if value <> fBottomPropOffsetPercentage
      then begin
              oldValue := fBottomPropOffsetPercentage;
              fBottomPropOffsetPercentage := value;
              fOffsetsChanged := true;
              if fUseBottomPropOffset and (not (csReading in componentState))
                 then height := height + Round(fLayoutHeight * ((value - oldValue) / 100));
              if csDesigning in ComponentState
                 then Invalidate;
           end;
end;

function TCustomLayout.CanResize(var newWidth, newHeight : integer) : boolean;
begin
   result := inherited CanResize(newWidth, newHeight);
   fMetricsChanged := result;
end;


{ TTestLayout }

procedure TTestLayout.Paint;
begin
   inherited;
   canvas.brush.color := clWhite;
   canvas.brush.style := bsSolid;
   canvas.FillRect(ClientRect);

   canvas.brush.style := bsClear;
   canvas.pen.Color := clGreen;
   canvas.Rectangle(layoutRect);

   canvas.pen.Color := clBlue;
   canvas.Rectangle(fixedLayoutRect);

   canvas.pen.Color := clRed;
   canvas.Rectangle(propLayoutRect);
end;

end.

